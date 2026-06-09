import assert from "node:assert/strict";
import test from "node:test";
import { createUser, formatFrame, parseFrame, readChatData, readHelloNick } from "./protocol.js";

test("formats and parses TYPE json frames", () => {
  const frame = formatFrame("MSG", { data: "hello" });
  assert.equal(frame, 'MSG {"data":"hello"}');
  assert.deepEqual(parseFrame(frame), { type: "MSG", payload: { data: "hello" } });
});

test("validates hello nick claims", () => {
  assert.equal(readHelloNick({ nick: "jack_1" }), "jack_1");
  assert.throws(() => readHelloNick({ nick: "bad nick" }), /Nick must/);
});

test("reads bounded chat message data", () => {
  assert.equal(readChatData({ data: " hello " }), "hello");
  assert.throws(() => readChatData({ data: "" }), /cannot be empty/);
  assert.throws(() => readChatData({ data: "x".repeat(513) }), /cannot exceed/);
});

test("creates dgg-shaped anonymous users", () => {
  assert.deepEqual(createUser(7, "test", new Date("2026-06-09T12:00:00.000Z")), {
    id: 7,
    nick: "test",
    roles: ["USER"],
    features: [],
    createdDate: "2026-06-09T12:00:00.000Z",
    watching: null,
    subscription: null,
  });
});
