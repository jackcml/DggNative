const WebSocket = require("ws");
const fs = require("fs");
const path = require("path");

class MockChatServer {
  constructor(port = 8080) {
    this.port = port;
    this.wss = null;
    this.messagesOnConnect = [];
    this.messagesRepeated = [];
    this.broadcastInterval = null;
    this.currentMessageIndex = 0;

    this.loadMessages();
  }

  loadMessages() {
    try {
      // Load messages that should be sent on connection
      const onConnectPath = path.join(__dirname, "messages_on_connect.json");
      if (fs.existsSync(onConnectPath)) {
        const onConnectData = fs.readFileSync(onConnectPath, "utf8");
        this.messagesOnConnect = JSON.parse(onConnectData);
      }

      // Load messages that should be repeated
      const repeatedPath = path.join(__dirname, "messages_repeated.json");
      if (fs.existsSync(repeatedPath)) {
        const repeatedData = fs.readFileSync(repeatedPath, "utf8");
        this.messagesRepeated = JSON.parse(repeatedData);
      }

      console.log(
        `Loaded ${this.messagesOnConnect.length} connection messages`
      );
      console.log(`Loaded ${this.messagesRepeated.length} repeated messages`);
    } catch (error) {
      console.error("Error loading messages:", error);
    }
  }

  start() {
    this.wss = new WebSocket.Server({ port: this.port });

    this.wss.on("connection", (ws) => {
      console.log("New client connected");

      // Send initial messages on connection
      this.sendInitialMessages(ws);

      // Handle incoming messages (for future extensibility)
      ws.on("message", (message) => {
        this.handleIncomingMessage(ws, message);
      });

      // Handle client disconnection
      ws.on("close", () => {
        console.log("Client disconnected");
      });

      // Handle errors
      ws.on("error", (error) => {
        console.error("WebSocket error:", error);
      });
    });

    // Start broadcasting repeated messages
    this.startBroadcasting();

    console.log(`Mock chat server started on port ${this.port}`);
  }

  sendInitialMessages(ws) {
    // Send all messages from messages_on_connect.json
    this.messagesOnConnect.forEach((message, index) => {
      setTimeout(() => {
        if (ws.readyState === WebSocket.OPEN) {
          ws.send(message);
        }
      }, index * 100); // Small delay between messages
    });
  }

  startBroadcasting() {
    // Calculate timing based on timestamp differences in the messages
    if (this.messagesRepeated.length === 0) return;

    // Start broadcasting after initial messages would be sent
    setTimeout(() => {
      this.broadcastNextMessage();
    }, this.messagesOnConnect.length * 100 + 1000);
  }

  broadcastNextMessage() {
    if (this.messagesRepeated.length === 0) return;

    const message = this.messagesRepeated[this.currentMessageIndex];

    // Broadcast to all connected clients
    this.wss.clients.forEach((client) => {
      if (client.readyState === WebSocket.OPEN) {
        client.send(message);
      }
    });

    // Calculate delay until next message based on timestamps
    let nextDelay = 1000; // Default 1 second

    if (this.currentMessageIndex < this.messagesRepeated.length - 1) {
      try {
        // Extract timestamp from current and next message
        const currentMsg = this.parseMessage(message);
        const nextMsg = this.parseMessage(
          this.messagesRepeated[this.currentMessageIndex + 1]
        );

        if (
          currentMsg &&
          nextMsg &&
          currentMsg.timestamp &&
          nextMsg.timestamp
        ) {
          // Calculate time difference, but cap it to reasonable values
          const timeDiff = Math.min(
            nextMsg.timestamp - currentMsg.timestamp,
            5000
          );
          nextDelay = Math.max(timeDiff, 100); // Minimum 100ms delay
        }
      } catch (error) {
        console.error("Error calculating message delay:", error);
      }
    }

    // Move to next message or loop back to beginning
    this.currentMessageIndex =
      (this.currentMessageIndex + 1) % this.messagesRepeated.length;

    // Schedule next message
    setTimeout(() => {
      this.broadcastNextMessage();
    }, nextDelay);
  }

  parseMessage(messageString) {
    try {
      // Parse message format like "MSG {\"id\":123,...}"
      const match = messageString.match(/^(\w+)\s+(.+)$/);
      if (match) {
        const [, type, jsonStr] = match;
        const data = JSON.parse(jsonStr);
        return { type, ...data };
      }
    } catch (error) {
      console.error("Error parsing message:", error);
    }
    return null;
  }

  handleIncomingMessage(ws, message) {
    // For future extensibility - currently just log incoming messages
    console.log("Received message:", message.toString());

    // Placeholder for future message handling logic
    // Could be extended to respond to specific client commands
  }

  stop() {
    if (this.broadcastInterval) {
      clearInterval(this.broadcastInterval);
    }

    if (this.wss) {
      this.wss.close();
    }

    console.log("Mock chat server stopped");
  }
}

// Start the server if this file is run directly
if (require.main === module) {
  const server = new MockChatServer(8080);

  // Handle graceful shutdown
  process.on("SIGINT", () => {
    console.log("\nShutting down server...");
    server.stop();
    process.exit(0);
  });

  server.start();
}

module.exports = MockChatServer;
