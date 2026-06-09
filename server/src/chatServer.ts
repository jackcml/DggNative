import { WebSocket, WebSocketServer } from "ws";
import {
  ChatMessagePayload,
  FrameType,
  NamesPayload,
  PresencePayload,
  User,
  createUser,
  formatFrame,
  parseFrame,
  readChatData,
  readHelloNick,
} from "./protocol.js";

export type ChatServerOptions = {
  host?: string;
  port: number;
  historyLimit?: number;
  onError?: (error: Error) => void;
};

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
    this.wss = new WebSocketServer({ host: options.host ?? "127.0.0.1", port: options.port });
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

  close(): Promise<void> {
    for (const client of this.wss.clients) {
      client.close(1001, "Server shutting down.");
    }

    return new Promise((resolve, reject) => {
      this.wss.close((error) => {
        if (error) reject(error);
        else resolve();
      });
    });
  }

  private handleConnection(socket: WebSocket): void {
    socket.on("message", (data) => {
      try {
        this.handleFrame(socket, data.toString("utf8"));
      } catch (error) {
        const message = error instanceof Error ? error.message : "Invalid frame.";
        socket.close(1008, message);
      }
    });

    socket.on("close", () => this.handleClose(socket));
    socket.on("error", (error) => {
      console.error("WebSocket error:", error);
    });
  }

  private handleFrame(socket: WebSocket, rawFrame: string): void {
    const frame = parseFrame(rawFrame);

    if (frame.type === "HELLO") {
      this.handleHello(socket, frame.payload);
      return;
    }

    const session = this.sessions.get(socket);
    if (!session) {
      throw new Error("Send HELLO before other frames.");
    }

    switch (frame.type) {
      case "MSG":
        this.handleMessage(session, frame.payload);
        break;
      default:
        throw new Error(`Unsupported inbound frame type: ${frame.type}.`);
    }
  }

  private handleHello(socket: WebSocket, payload: unknown): void {
    if (this.sessions.has(socket)) {
      throw new Error("HELLO was already accepted for this connection.");
    }

    const nick = readHelloNick(payload);
    const normalizedNick = nick.toLowerCase();
    if (this.nicks.has(normalizedNick)) {
      throw new Error(`Nick is already in use: ${nick}.`);
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

  private createNamesPayload(): NamesPayload {
    return {
      connectioncount: this.sessions.size,
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
