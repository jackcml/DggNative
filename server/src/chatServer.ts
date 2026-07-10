import { WebSocket, WebSocketServer } from "ws";
import type { AddressInfo } from "node:net";
import {
  ChatMessagePayload,
  ErrorPayload,
  FrameType,
  NamesPayload,
  NativeErrorCode,
  PresencePayload,
  User,
  createUser,
  formatFrame,
  parseFrame,
  readChatData,
  readHelloNick,
} from "./protocol.js";

// Valid native commands are currently at most a few KiB: HELLO has a 32-character
// nick and MSG has 512 Unicode characters. 16 KiB also leaves compatibility room
// for the currently accepted DGG-shaped MSG without permitting unbounded frames.
export const MaxPayloadBytes = 16 * 1024;

const DebugReason = {
  PayloadTooLarge: "PAYLOAD_TOO_LARGE",
} as const;

export type ChatServerOptions = {
  host?: string;
  port: number;
  historyLimit?: number;
  onError?: (error: Error) => void;
  onDebug?: (event: InboundRejection) => void;
};

export type InboundRejection = {
  event: "inbound_rejected";
  reason: string;
  frameType?: string;
};

class InboundError extends Error {
  constructor(
    readonly code: NativeErrorCode,
    message: string,
    readonly terminal: boolean,
    readonly frameType?: string,
  ) {
    super(message);
  }
}

type Session = {
  socket: WebSocket;
  user: User;
};

export class ChatServer {
  private readonly wss: WebSocketServer;
  private readonly sessions = new Map<WebSocket, Session>();
  private readonly nicks = new Map<string, Session>();
  private readonly history: string[] = [];
  private readonly historyLimit: number;
  private nextUserId = 1;

  constructor(private readonly options: ChatServerOptions) {
    this.historyLimit = options.historyLimit ?? 500;
    if (!Number.isFinite(this.historyLimit) || !Number.isInteger(this.historyLimit) || this.historyLimit < 0) {
      throw new Error("historyLimit must be a finite, non-negative integer.");
    }

    this.wss = new WebSocketServer({
      host: options.host ?? "127.0.0.1",
      port: options.port,
      maxPayload: MaxPayloadBytes,
    });
    this.wss.on("error", (error) => {
      console.error("WebSocket server error:", error);
      this.options.onError?.(error);
    });
  }

  start(): void {
    this.wss.on("connection", (socket) => this.handleConnection(socket));
    this.wss.on("listening", () => {
      const address = this.wss.address();
      const port = typeof address === "object" && address !== null ? address.port : this.options.port;
      const host = this.options.host ?? "127.0.0.1";
      console.log(`DGG-compatible chat server listening on ws://${host}:${port}`);
    });
  }

  async listeningPort(): Promise<number> {
    const currentAddress = this.wss.address();
    if (currentAddress) return (currentAddress as AddressInfo).port;

    await new Promise<void>((resolve, reject) => {
      this.wss.once("listening", resolve);
      this.wss.once("error", reject);
    });
    return (this.wss.address() as AddressInfo).port;
  }

  close(): Promise<void> {
    for (const client of this.wss.clients) {
      this.safeClose(client, 1001, "SERVER_SHUTDOWN");
    }

    return new Promise((resolve, reject) => {
      this.wss.close((error) => {
        if (error) reject(error);
        else resolve();
      });
    });
  }

  private handleConnection(socket: WebSocket): void {
    socket.on("message", (data, isBinary) => {
      if (isBinary) {
        this.reject(socket, new InboundError(NativeErrorCode.InvalidFrame, "Binary frames are not supported.", true));
        return;
      }

      try {
        this.handleFrame(socket, data.toString("utf8"));
      } catch (error) {
        this.reject(
          socket,
          error instanceof InboundError
            ? error
            : new InboundError(NativeErrorCode.InvalidFrame, "The frame is invalid.", true),
        );
      }
    });

    socket.on("close", () => this.handleClose(socket));
    socket.on("error", (error) => {
      if ((error as NodeJS.ErrnoException).code === "WS_ERR_UNSUPPORTED_MESSAGE_LENGTH") {
        this.debugRejection(DebugReason.PayloadTooLarge);
        return;
      }
      this.options.onError?.(error);
    });
  }

  private handleFrame(socket: WebSocket, rawFrame: string): void {
    let frame;
    try {
      frame = parseFrame(rawFrame);
    } catch {
      throw new InboundError(NativeErrorCode.InvalidFrame, "The frame is invalid.", true);
    }

    if (frame.type !== "HELLO" && frame.type !== "MSG") {
      throw new InboundError(NativeErrorCode.InvalidFrame, "The frame type is not supported.", true, frame.type);
    }

    if (frame.type === "HELLO") {
      try {
        this.handleHello(socket, frame.payload);
      } catch (error) {
        if (error instanceof InboundError) throw error;
        throw new InboundError(NativeErrorCode.InvalidMessage, "The HELLO payload is invalid.", false, frame.type);
      }
      return;
    }

    const session = this.sessions.get(socket);
    if (!session) {
      throw new InboundError(
        NativeErrorCode.IdentificationRequired,
        "Choose a nickname before sending chat.",
        false,
        frame.type,
      );
    }

    try {
      this.handleMessage(session, frame.payload);
    } catch {
      throw new InboundError(NativeErrorCode.InvalidMessage, "The chat message is invalid.", false, frame.type);
    }
  }

  private handleHello(socket: WebSocket, payload: unknown): void {
    if (this.sessions.has(socket)) {
      throw new InboundError(
        NativeErrorCode.InvalidMessage,
        "This connection already has a nickname.",
        false,
        "HELLO",
      );
    }

    const nick = readHelloNick(payload);
    const normalizedNick = nick.toLowerCase();
    if (this.nicks.has(normalizedNick)) {
      throw new InboundError(
        NativeErrorCode.NickInUse,
        "That nickname is already in use.",
        false,
        "HELLO",
      );
    }

    const session: Session = {
      socket,
      user: createUser(this.nextUserId++, nick),
    };

    this.sessions.set(socket, session);
    this.nicks.set(normalizedNick, session);

    this.send(socket, "ME", session.user);
    this.send(socket, "NAMES", this.createNamesPayload());
    this.send(socket, "HISTORY", this.history);
    this.broadcast("JOIN", this.createPresencePayload(session.user));
  }

  private handleMessage(session: Session, payload: unknown): void {
    const data = readChatData(payload);
    const message: ChatMessagePayload = {
      ...session.user,
      timestamp: Date.now(),
      data,
    };
    const frame = formatFrame("MSG", message);

    this.history.push(frame);
    while (this.history.length > this.historyLimit) {
      this.history.shift();
    }

    this.broadcastFrame(frame);
  }

  private handleClose(socket: WebSocket): void {
    const session = this.sessions.get(socket);
    if (!session) return;

    this.sessions.delete(socket);
    this.nicks.delete(session.user.nick.toLowerCase());
    this.broadcast("QUIT", this.createPresencePayload(session.user));
  }

  private reject(socket: WebSocket, error: InboundError): void {
    this.debugRejection(error.code, error.frameType);
    const payload: ErrorPayload = { code: error.code, message: error.message };
    this.send(socket, "ERROR", payload);
    if (error.terminal) this.safeClose(socket, 1008, error.code);
  }

  private debugRejection(reason: string, frameType?: string): void {
    this.options.onDebug?.({
      event: "inbound_rejected",
      reason,
      // Keep diagnostics useful but bounded even for attacker-controlled types.
      ...(frameType ? { frameType: frameType.slice(0, 16) } : {}),
    });
  }

  private safeClose(socket: WebSocket, code: number, reason: string): void {
    try {
      socket.close(code, reason);
    } catch (error) {
      this.options.onError?.(error instanceof Error ? error : new Error("WebSocket close failed."));
      socket.terminate();
    }
  }

  private createNamesPayload(): NamesPayload {
    return {
      connectioncount: this.wss.clients.size,
      users: [...this.sessions.values()].map((session) => session.user),
    };
  }

  private createPresencePayload(user: User): PresencePayload {
    return {
      ...user,
      timestamp: Date.now(),
    };
  }

  private send(socket: WebSocket, type: FrameType, payload: unknown): void {
    if (socket.readyState === WebSocket.OPEN) {
      socket.send(formatFrame(type, payload));
    }
  }

  private broadcast(type: FrameType, payload: unknown): void {
    this.broadcastFrame(formatFrame(type, payload));
  }

  private broadcastFrame(frame: string): void {
    for (const client of this.wss.clients) {
      if (client.readyState === WebSocket.OPEN) {
        client.send(frame);
      }
    }
  }
}
