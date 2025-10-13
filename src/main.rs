mod chat;

use std::env;

use chat::{Chat, MessageType};
use futures_util::{pin_mut, StreamExt};
use rustls::RootCertStore;
use rustls_native_certs::load_native_certs;
use serde_json::Value;
use tokio::signal;
use tokio_tungstenite::{connect_async_tls_with_config, tungstenite::protocol::Message, Connector};

#[tokio::main]
async fn main() {
    let chat = &Chat::new();

    let url = env::var("DGG_WS_URL").unwrap_or_else(|_| "wss://chat.destiny.gg/ws".to_string());

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

    let (ws_stream, _) = connect_async_tls_with_config(&url, None, false, Some(tls_connector))
        .await
        .expect("Failed to connect");
    println!("WebSocket handshake has been successfully completed");

    let (_, read) = ws_stream.split();
    let ws_to_message_handler = read.for_each(|msg| message_handler(msg, chat));

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
) {
    let data = match message {
        Ok(msg) => msg.into_data(),
        Err(e) => {
            eprintln!("WebSocket error: {}", e);
            return;
        }
    };
    let message_str = String::from_utf8_lossy(&data);

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
    chat.recieve_msg(msg_type, json_data).await;
}
