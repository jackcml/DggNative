# DggNative Chat Server

A small standalone WebSocket chat server that speaks the same `TYPE <json>` frame shape used by Destiny.gg chat and the DggNative client.

This is intentionally minimal:
 - identity is IRC-style nickname claiming
 - state is in memory
 - message history lasts only until the process exits.

## Usage

Install dependencies:

```bash
npm install
```

Run in development:

```bash
npm run dev
```

Build and run:

```bash
npm run build
npm start
```

By default the server listens on `ws://127.0.0.1:8080`.
Set `HOST` or `PORT` to override it.
For a LAN/deployment bind, use `HOST=0.0.0.0`.

## Protocol

Frames are text WebSocket messages:

```text
TYPE <json>
```

A client must claim a nickname before sending chat:

```text
HELLO {"nick":"jack"}
```

The server validates that the nick is 1-32 characters, starts with a letter,
number, or underscore, and then contains only letters, numbers, underscores, or
hyphens. Nicknames are unique case-insensitively while connected.

After `HELLO`, the server sends:

```text
ME {...user}
NAMES {"connectioncount":1,"users":[...]}
HISTORY ["MSG {...}", ...]
JOIN {...user,"timestamp":...}
```

Clients send chat as:

```text
MSG {"data":"hello"}
```

For compatibility with the current client, a larger DGG-shaped `MSG` payload is
also accepted, but the server only trusts `data`. The outgoing message is
always authored from the server-side session:

```text
MSG {"id":1,"nick":"jack","roles":["USER"],"features":[],"createdDate":"...","watching":null,"subscription":null,"timestamp":...,"data":"hello"}
```

On disconnect, the server broadcasts:

```text
QUIT {...user,"timestamp":...}
```

## Current Limitations

- No durable accounts or persisted history.
- No moderation commands.
- No rate limiting beyond rejecting empty messages and messages over 512 characters.
- The current DggNative client still needs a follow-up change to send `HELLO` and allow local nickname login without Destiny.gg cookies.
