mod chat;
mod frontend;
mod tui;

use clap::Parser;
use futures_util::{pin_mut, StreamExt};
use rustls::RootCertStore;
use rustls_native_certs::load_native_certs;
use serde_json::Value;
use tokio::signal;
use tokio_tungstenite::{connect_async_tls_with_config, tungstenite::protocol::Message, Connector};

use crate::chat::{Chat, MessageType};
use crate::frontend::Frontend;

#[derive(Parser)]
#[command(version, about, long_about = None)]
struct Args {
    /// Frontend to use
    #[arg(short, long, default_value_t = String::from("tui"))]
    frontend: String,

    /// WebSocket URL to connect to
    #[arg(short, long, default_value_t = String::from("wss://chat.destiny.gg/ws"))]
    url: String,

    /// Enables debug printing
    #[arg(short, long)]
    debug: bool,
}

#[tokio::main]
async fn main() {
    let args = Args::parse();

    let chat = &Chat::new(args.debug);
    let frontend = match args.frontend.as_str() {
        "tui" => &tui::Tui {},
        other => {
            eprintln!("Unknown frontend '{}'.", other);
            return;
        }
    };

    let mut root_store = RootCertStore::empty();
    let native_certs = load_native_certs().certs;
    if native_certs.is_empty() {
        panic!("unable to load native certificates");
    }
    root_store.add_parsable_certificates(native_certs);

    let tls_connector = Connector::Rustls(std::sync::Arc::new(
        rustls::ClientConfig::builder()
            .with_root_certificates(root_store)
            .with_no_client_auth(),
    ));

    let (ws_stream, _) = connect_async_tls_with_config(&args.url, None, false, Some(tls_connector))
        .await
        .expect("Failed to connect");
    println!("WebSocket handshake has been successfully completed");

    let (_, read) = ws_stream.split();
    let ws_to_message_handler = read.for_each(|msg| message_handler(msg, chat, frontend));

    let ctrl_c = signal::ctrl_c();
    pin_mut!(ws_to_message_handler, ctrl_c);

    tokio::select! {
        _ = ws_to_message_handler => {
            println!("Disconnected.");
        },
        _ = ctrl_c => {
            println!("Recieved Ctrl+C, disconnecting...");
        },
    }
}

async fn message_handler(
    message: Result<Message, tokio_tungstenite::tungstenite::Error>,
    chat: &Chat,
    frontend: &impl Frontend,
) {
    let data = match message {
        Ok(Message::Text(text)) => text,
        Ok(_) => return, // We ignore non-textual messages since the chat protocol doesn't use them
        Err(e) => {
            eprintln!("WebSocket error: {}", e);
            return;
        }
    };
    let message_str = String::from_utf8_lossy(data.as_bytes());

    // Split message into type and JSON data
    let space_pos = match message_str.find(' ') {
        Some(pos) => pos,
        None => {
            eprintln!("Invalid message format.");
            eprintln!("Raw message: {}", message_str);
            return;
        }
    };

    // Parse message type to enum
    let msg_type_str = &message_str[..space_pos];
    let msg_type = match MessageType::from_str(msg_type_str) {
        Some(msg_type) => msg_type,
        None => {
            eprintln!("Unknown message type: {}", msg_type_str);
            eprintln!("Raw message: {}", message_str);
            return;
        }
    };

    // Parse JSON data
    let json_str = &message_str[space_pos + 1..];
    let json_data = match serde_json::from_str::<Value>(json_str) {
        Ok(data) => data,
        Err(e) => {
            eprintln!("Failed to parse JSON: {}", e);
            eprintln!("Raw message: {}", message_str);
            return;
        }
    };

    // Success case: pass JSON onto handler for given message type
    chat.recieve_msg(msg_type, json_data, frontend).await;
}
