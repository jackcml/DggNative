import assert from "node:assert/strict";
import test from "node:test";
import { WebSocket } from "ws";
import { ChatServer, MaxPayloadBytes } from "./chatServer.js";

type CloseResult = { code: number; reason: string };

test("rejects malformed real-socket input without making the server unhealthy", async (t) => {
  const debugEvents: unknown[] = [];
  const server = new ChatServer({ port: 0, onDebug: (event) => debugEvents.push(event) });
  server.start();
  t.after(() => server.close());
  const url = `ws://127.0.0.1:${await server.listeningPort()}`;

  const cases: Array<{ name: string; frame: string | Buffer; code: number; reason?: string }> = [
    { name: "long frame type", frame: `${"X".repeat(200)} {}`, code: 1008, reason: "INVALID_FRAME" },
    { name: "invalid JSON", frame: "HELLO {", code: 1008, reason: "INVALID_FRAME" },
    { name: "missing type", frame: ' {"nick":"x"}', code: 1008, reason: "INVALID_FRAME" },
    { name: "missing payload", frame: "HELLO ", code: 1008, reason: "INVALID_FRAME" },
    { name: "unknown type", frame: "BOGUS {}", code: 1008, reason: "UNKNOWN_FRAME_TYPE" },
    { name: "binary frame", frame: Buffer.from('HELLO {"nick":"binary"}'), code: 1008, reason: "BINARY_NOT_SUPPORTED" },
    { name: "oversized frame", frame: `MSG ${"x".repeat(MaxPayloadBytes)}`, code: 1009 },
  ];

  for (const entry of cases) {
    await t.test(entry.name, async () => {
      const result = await sendAndClose(url, entry.frame);
      assert.equal(result.code, entry.code);
      if (entry.reason) assert.equal(result.reason, entry.reason);
      await assertServerHealthy(url, entry.name.replaceAll(" ", "_"));
    });
  }

  assert.equal(debugEvents.length, cases.length);
});

test("rejects repeated HELLO and keeps accepting new sessions", async (t) => {
  const server = new ChatServer({ port: 0 });
  server.start();
  t.after(() => server.close());
  const url = `ws://127.0.0.1:${await server.listeningPort()}`;
  const socket = await connect(url);

  socket.send('HELLO {"nick":"first"}');
  await waitForMessage(socket, "ME ");
  socket.send('HELLO {"nick":"second"}');
  assert.deepEqual(await waitForClose(socket), { code: 1008, reason: "HELLO_ALREADY_ACCEPTED" });
  await assertServerHealthy(url, "after_repeat");
});

test("validates historyLimit before opening the server", () => {
  for (const historyLimit of [-1, 1.5, Number.NaN, Number.POSITIVE_INFINITY]) {
    assert.throws(() => new ChatServer({ port: 0, historyLimit }), /finite, non-negative integer/);
  }
});

test("arbitrary bounded input cannot escape the connection boundary", async (t) => {
  const server = new ChatServer({ port: 0 });
  server.start();
  t.after(() => server.close());
  const url = `ws://127.0.0.1:${await server.listeningPort()}`;
  let state = 0x5eed1234;

  for (let sample = 0; sample < 64; sample++) {
    const length = nextRandom() % 1024;
    const bytes = Buffer.allocUnsafe(length);
    for (let index = 0; index < length; index++) bytes[index] = nextRandom() & 0xff;

    const socket = await connect(url);
    socket.send(sample % 4 === 0 ? bytes : bytes.toString("utf8"));
    await settleSocket(socket);
  }

  await assertServerHealthy(url, "after_fuzz");

  function nextRandom(): number {
    state ^= state << 13;
    state ^= state >>> 17;
    state ^= state << 5;
    return state >>> 0;
  }
});

async function assertServerHealthy(url: string, nickSuffix: string): Promise<void> {
  const socket = await connect(url);
  socket.send(`HELLO ${JSON.stringify({ nick: `health_${nickSuffix}`.slice(0, 32) })}`);
  const message = await waitForMessage(socket, "ME ");
  assert.match(message, /^ME /);
  socket.close();
  await waitForClose(socket);
}

async function sendAndClose(url: string, frame: string | Buffer): Promise<CloseResult> {
  const socket = await connect(url);
  socket.send(frame);
  return waitForClose(socket);
}

function connect(url: string): Promise<WebSocket> {
  return new Promise((resolve, reject) => {
    const socket = new WebSocket(url);
    socket.once("open", () => resolve(socket));
    socket.once("error", reject);
  });
}

function waitForMessage(socket: WebSocket, prefix: string): Promise<string> {
  return new Promise((resolve, reject) => {
    const onMessage = (data: Buffer) => {
      const message = data.toString("utf8");
      if (message.startsWith(prefix)) {
        cleanup();
        resolve(message);
      }
    };
    const onError = (error: Error) => { cleanup(); reject(error); };
    const cleanup = () => {
      socket.off("message", onMessage);
      socket.off("error", onError);
    };
    socket.on("message", onMessage);
    socket.on("error", onError);
  });
}

function waitForClose(socket: WebSocket): Promise<CloseResult> {
  return new Promise((resolve) => {
    socket.once("close", (code, reason) => resolve({ code, reason: reason.toString("utf8") }));
  });
}

async function settleSocket(socket: WebSocket): Promise<void> {
  await Promise.race([waitForClose(socket), new Promise<void>((resolve) => setTimeout(resolve, 10))]);
  if (socket.readyState !== WebSocket.CLOSED) socket.terminate();
}
