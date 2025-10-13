use std::env;

use futures_util::{pin_mut, StreamExt};
use rustls::RootCertStore;
use rustls_native_certs::load_native_certs;
use tokio::io::AsyncWriteExt;
use tokio::signal;
use tokio_tungstenite::{connect_async_tls_with_config, Connector};

#[tokio::main]
async fn main() {
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
    let ws_to_stdout = {
        read.for_each(|message| async {
            let data = message.unwrap().into_data();
            tokio::io::stdout().write_all(&data).await.unwrap();
        })
    };

    let ctrl_c = signal::ctrl_c();
    pin_mut!(ws_to_stdout, ctrl_c);

    tokio::select! {
        _ = ws_to_stdout => {
            println!("Disconnected.");
        },
        _ = ctrl_c => {
            println!("Recieved Ctrl+C, disconnecting...");
        },
    }
}
