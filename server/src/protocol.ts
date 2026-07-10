import { z } from "zod";

export const FrameTypeSchema = z.enum([
  "HELLO",
  "MSG",
  "JOIN",
  "QUIT",
  "UPDATEUSER",
  "HISTORY",
  "ME",
  "NAMES",
]);

export type FrameType = z.infer<typeof FrameTypeSchema>;

export const WatchingSchema = z.object({
  platform: z.string(),
  id: z.string(),
});

export const SubscriptionSchema = z.object({
  tier: z.number(),
  source: z.string(),
});

export const UserSchema = z.object({
  id: z.number(),
  nick: z.string(),
  roles: z.array(z.string()),
  features: z.array(z.string()),
  createdDate: z.string(),
  watching: WatchingSchema.nullable(),
  subscription: SubscriptionSchema.nullable(),
});

export type User = z.infer<typeof UserSchema>;
export type Watching = z.infer<typeof WatchingSchema>;
export type Subscription = z.infer<typeof SubscriptionSchema>;

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

export const MaxFrameTypeLength = 16;

const NickPattern = /^[A-Za-z0-9_][A-Za-z0-9_-]{0,31}$/;

export const HelloPayloadSchema = z.object({
  nick: z
    .string({ error: "HELLO payload must include a nick string." })
    .trim()
    .regex(NickPattern, "Nick must be 1-32 chars and use letters, numbers, underscores, or hyphens."),
});

export const ChatPayloadSchema = z.object({
  data: z
    .string({ error: "MSG payload must include a data string." })
    .trim()
    .min(1, "MSG data cannot be empty.")
    .max(512, "MSG data cannot exceed 512 characters."),
});

export function formatFrame(type: FrameType, payload: unknown): string {
  return `${type} ${JSON.stringify(payload)}`;
}

export function parseFrame(input: string): ParsedFrame {
  const spaceIndex = input.indexOf(" ");
  if (spaceIndex <= 0) {
    throw new Error("Frame must be shaped as TYPE <json>.");
  }

  const type = input.slice(0, spaceIndex);
  if (type.length > MaxFrameTypeLength) {
    throw new Error(`Frame type cannot exceed ${MaxFrameTypeLength} characters.`);
  }

  const json = input.slice(spaceIndex + 1);
  if (!type || !json) {
    throw new Error("Frame type and JSON payload are required.");
  }

  return { type, payload: JSON.parse(json) };
}

export function readHelloNick(payload: unknown): string {
  return parsePayload(HelloPayloadSchema, payload, "HELLO").nick;
}

export function readChatData(payload: unknown): string {
  return parsePayload(ChatPayloadSchema, payload, "MSG").data;
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

function parsePayload<Schema extends z.ZodType>(
  schema: Schema,
  payload: unknown,
  frameType: FrameType,
): z.infer<Schema> {
  const result = schema.safeParse(payload);
  if (!result.success) {
    const issue = result.error.issues[0];
    throw new Error(issue?.message ?? `Invalid ${frameType} payload.`);
  }

  return result.data;
}
