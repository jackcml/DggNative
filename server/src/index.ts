import { ChatServer } from "./chatServer.js";

const host = process.env.HOST ?? "127.0.0.1";
const port = readPort(process.env.PORT, 8080);
const server = new ChatServer({
  host,
  port,
  onError: () => {
    process.exitCode = 1;
  },
});

server.start();

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, async () => {
    console.log(`Received ${signal}; shutting down.`);
    await server.close();
    process.exit(0);
  });
}

function readPort(value: string | undefined, fallback: number): number {
  if (!value) return fallback;

  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 1 || parsed > 65535) {
    throw new Error(`Invalid PORT: ${value}`);
  }

  return parsed;
}
