export type FrameType =
  | "HELLO"
  | "MSG"
  | "JOIN"
  | "QUIT"
  | "UPDATEUSER"
  | "HISTORY"
  | "ME"
  | "NAMES";

export type User = {
  id: number;
  nick: string;
  roles: string[];
  features: string[];
  createdDate: string;
  watching: Watching | null;
  subscription: Subscription | null;
};

export type Watching = {
  platform: string;
  id: string;
};

export type Subscription = {
  tier: number;
  source: string;
};

export type ChatMessagePayload = User & {
  timestamp: number;
  data: string;
};

export type PresencePayload = User & {
  timestamp: number;
};

export type NamesPayload = {
  connectioncount: number;
  users: User[];
};

export type ParsedFrame = {
  type: string;
  payload: unknown;
};

const NickPattern = /^[A-Za-z0-9_][A-Za-z0-9_-]{0,31}$/;

export function formatFrame(type: FrameType, payload: unknown): string {
  return `${type} ${JSON.stringify(payload)}`;
}

export function parseFrame(input: string): ParsedFrame {
  const spaceIndex = input.indexOf(" ");
  if (spaceIndex <= 0) {
    throw new Error("Frame must be shaped as TYPE <json>.");
  }

  const type = input.slice(0, spaceIndex);
  const json = input.slice(spaceIndex + 1);
  if (!type || !json) {
    throw new Error("Frame type and JSON payload are required.");
  }

  return { type, payload: JSON.parse(json) };
}

export function readHelloNick(payload: unknown): string {
  if (!isRecord(payload) || typeof payload.nick !== "string") {
    throw new Error("HELLO payload must include a nick string.");
  }

  const nick = payload.nick.trim();
  if (!NickPattern.test(nick)) {
    throw new Error("Nick must be 1-32 chars and use letters, numbers, underscores, or hyphens.");
  }

  return nick;
}

export function readChatData(payload: unknown): string {
  if (!isRecord(payload) || typeof payload.data !== "string") {
    throw new Error("MSG payload must include a data string.");
  }

  const data = payload.data.trim();
  if (data.length === 0) {
    throw new Error("MSG data cannot be empty.");
  }

  if (data.length > 512) {
    throw new Error("MSG data cannot exceed 512 characters.");
  }

  return data;
}

export function createUser(id: number, nick: string, now = new Date()): User {
  return {
    id,
    nick,
    roles: ["USER"],
    features: [],
    createdDate: now.toISOString(),
    watching: null,
    subscription: null,
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
