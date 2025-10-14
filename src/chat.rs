use std::env;

use serde::{Deserialize, Serialize};
use serde_json::Value;

#[derive(Debug)]
pub enum MessageType {
    Ping,
    Connecting,
    Me,
    Open,
    Dispatch,
    Close,
    Names,
    History,
    Pin,
    Quit,
    Msg,
    Mute,
    Unmute,
    Ban,
    Unban,
    Err,
    SocketError,
    SubOnly,
    Broadcast,
    Reload,
    PrivMsgSent,
    PrivMsg,
    PollStart,
    PollStop,
    VoteCast,
    Subscription,
    GiftSub,
    MassGift,
    Donation,
    UpdateUser,
    AddPhrase,
    RemovePhrase,
    Death,
    PaidEvents,
    Join,
}

impl MessageType {
    pub fn from_str(s: &str) -> Option<Self> {
        match s {
            "PING" => Some(MessageType::Ping),
            "CONNECTING" => Some(MessageType::Connecting),
            "ME" => Some(MessageType::Me),
            "OPEN" => Some(MessageType::Open),
            "DISPATCH" => Some(MessageType::Dispatch),
            "CLOSE" => Some(MessageType::Close),
            "NAMES" => Some(MessageType::Names),
            "HISTORY" => Some(MessageType::History),
            "PIN" => Some(MessageType::Pin),
            "QUIT" => Some(MessageType::Quit),
            "MSG" => Some(MessageType::Msg),
            "MUTE" => Some(MessageType::Mute),
            "UNMUTE" => Some(MessageType::Unmute),
            "BAN" => Some(MessageType::Ban),
            "UNBAN" => Some(MessageType::Unban),
            "ERR" => Some(MessageType::Err),
            "SOCKETERROR" => Some(MessageType::SocketError),
            "SUBONLY" => Some(MessageType::SubOnly),
            "BROADCAST" => Some(MessageType::Broadcast),
            "RELOAD" => Some(MessageType::Reload),
            "PRIVMSGSENT" => Some(MessageType::PrivMsgSent),
            "PRIVMSG" => Some(MessageType::PrivMsg),
            "POLLSTART" => Some(MessageType::PollStart),
            "POLLSTOP" => Some(MessageType::PollStop),
            "VOTECAST" => Some(MessageType::VoteCast),
            "SUBSCRIPTION" => Some(MessageType::Subscription),
            "GIFTSUB" => Some(MessageType::GiftSub),
            "MASSGIFT" => Some(MessageType::MassGift),
            "DONATION" => Some(MessageType::Donation),
            "UPDATEUSER" => Some(MessageType::UpdateUser),
            "ADDPHRASE" => Some(MessageType::AddPhrase),
            "REMOVEPHRASE" => Some(MessageType::RemovePhrase),
            "DEATH" => Some(MessageType::Death),
            "PAIDEVENTS" => Some(MessageType::PaidEvents),
            "JOIN" => Some(MessageType::Join),
            _ => None,
        }
    }
}

#[derive(Serialize, Deserialize)]
struct Watching {
    platform: String,
    id: String,
}

#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct Msg {
    id: f64,
    nick: String,
    roles: Vec<String>,
    features: Vec<String>,
    created_date: String,
    watching: Watching,
    timestamp: f64,
    data: String,
}

pub struct Chat {}
impl Chat {
    pub fn new() -> Self {
        Chat {}
    }

    pub async fn recieve_msg(&self, msg_type: MessageType, json: Value) {
        match msg_type {
            MessageType::Msg => {
                let msg = match serde_json::from_value::<Msg>(json) {
                    Ok(msg) => msg,
                    Err(e) => {
                        eprintln!("Malformed JSON for MSG: {}", e);
                        return;
                    }
                };
                println!("{}: {}", msg.nick, msg.data);
            }
            _ => {
                if env::var("DEBUG").is_ok() {
                    eprintln!("recieve_msg not yet implemented for type: {:?}", msg_type);
                }
            }
        }
    }
}
