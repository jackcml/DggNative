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

Connections begin as guests. Guests receive live `JOIN`, `QUIT`, and `MSG`
events but cannot publish chat or claim a place in the named-user list until a
`HELLO` succeeds.

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

Application errors use a structured frame rather than overloading WebSocket
close descriptions:

```text
ERROR {"code":"IDENTIFICATION_REQUIRED","message":"Choose a nickname before sending chat."}
```

The Native V1 error registry and current client behavior are:

| Code | Client behavior |
|---|---|
| `NICK_IN_USE` | Reject the current identification attempt, clear only that attempted nick, and allow another claim. |
| `IDENTIFICATION_REQUIRED` | Remain connected as a guest and preserve the unsent composer text. |
| `INVALID_FRAME` | Treat a terminal malformed connection as retryable; do not interpret it as a nick conflict. |
| `INVALID_MESSAGE` | Remain in the current session and show the message-level failure. |
| `INPUT_TOO_LARGE` | Preserve input and report that it exceeds the transport limit. |
| `RATE_LIMITED` | Preserve input and report the temporary refusal. |
| `SERVER_SHUTDOWN` / `INTERNAL_ERROR` | Clear the active identity and enter the normal retry lifecycle. |

## Current Limitations

- No durable accounts or persisted history.
- No moderation commands.
- No rate limiting beyond rejecting empty messages and messages over 512 characters.
- The current DggNative client still needs a follow-up change to send `HELLO` and allow local nickname login without Destiny.gg cookies.
