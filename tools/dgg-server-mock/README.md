# DGG Mock WebSocket Server

A mock WebSocket chat server that simulates the DGG (Destiny.gg) chat server interface for client application testing.

## Features

- Sends initial connection messages from `messages_on_connect.json`
- Repeatedly broadcasts messages from `messages_repeated.json` with realistic timing
- Echoes locally sent `MSG` frames back to connected clients for send-message testing
- Handles multiple client connections
- Extensible message handling for future enhancements
- Graceful shutdown handling

## Installation

1. Install dependencies:

```bash
npm install
```

## Usage

### Starting the Server

```bash
npm start
```

The server will start on `ws://localhost:8080` by default.

### For Development

```bash
npm run dev
```

This uses nodemon to automatically restart the server when files change.

## Message Format

The server expects message files in JSON format containing arrays of string messages. Each message follows the pattern:

```
MSG {JSON payload}
JOIN {JSON payload}
QUIT {JSON payload}
UPDATEUSER {JSON payload}
```

Example:

```
MSG {"id":123,"nick":"username","roles":["USER"],"features":[],"createdDate":"2023-01-01T00:00:00Z","watching":{"platform":"kick","id":"streamer"},"subscription":null,"timestamp":1234567890123,"data":"Hello world"}
```

## Message Timing

- Initial connection messages are sent with 100ms delays between them
- Repeated messages are broadcast based on timestamp differences in the original data
- Minimum delay of 100ms and maximum of 5 seconds between messages

## Client Connection

Connect your existing client application to `ws://localhost:8080`. The server will:

1. Immediately send all messages from `messages_on_connect.json`
2. Begin broadcasting messages from `messages_repeated.json` in a loop
3. Handle incoming messages, including rebroadcasting local `MSG` frames for send-message testing

When a client sends a chat frame like `MSG { ... }`, the mock server now rebroadcasts it as a normal `MSG` event so the local client can see its own sent messages in the chat stream without the real Destiny.gg backend.

## Extensibility

The server includes a `handleIncomingMessage` method that can be extended to respond to specific client commands. Currently, it logs all incoming messages for debugging purposes.

## Stopping the Server

Press `Ctrl+C` to gracefully shut down the server.
