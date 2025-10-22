use serde::{Deserialize, Serialize};
use serde_json::Value;
use std::collections::VecDeque;

use crate::Frontend;

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

#[derive(Serialize, Deserialize, Clone)]
pub struct Watching {
    pub platform: String,
    pub id: String,
}

#[derive(Serialize, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct User {
    pub id: f64,
    pub nick: String,
    pub roles: Vec<String>,
    pub features: Vec<String>,
    pub created_date: String,
    pub watching: Option<Watching>,
}

#[derive(Serialize, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct Msg {
    #[serde(flatten)]
    pub user: User,
    pub timestamp: f64,
    pub data: String,
}

pub struct Chat {
    user: Option<User>,
    pin: Option<Msg>,
    debug: bool,
    history_len: usize,
    history: VecDeque<Msg>,
}

impl Chat {
    pub fn new(debug: bool, history_len: usize) -> Self {
        Chat {
            user: None,
            pin: None,
            debug,
            history_len,
            history: VecDeque::new(),
        }
    }

    fn history_add(&mut self, msg: Msg) {
        if self.history.len() >= self.history_len {
            self.history.pop_front();
        }
        self.history.push_back(msg);
    }

    pub async fn recieve_msg(
        &mut self,
        msg_type: MessageType,
        json: Value,
        frontend: &impl Frontend,
    ) {
        match msg_type {
            /* Sent on connection open */
            MessageType::Me => {
                /* If not logged in, value of ME is null. */
                if json.is_null() {
                    self.user = None;
                } else {
                    self.user = match serde_json::from_value::<User>(json) {
                        Ok(me) => Some(me),
                        Err(e) => {
                            eprintln!("Malformed JSON for ME: {}", e);
                            None
                        }
                    };
                }
                frontend.connected_as(&self.user);
            }
            MessageType::History => {
                let back_history: Vec<String> = match serde_json::from_value(json) {
                    Ok(x) => x,
                    Err(e) => {
                        eprintln!("Malformed JSON for HISTORY: {}", e);
                        return;
                    }
                };

                for msg_str in back_history {
                    // Parse each historical message string and process it
                    if let Some((msg_type, json_data)) = parse_message_string(&msg_str) {
                        // Recursion in async fn requires boxing
                        Box::pin(self.recieve_msg(msg_type, json_data, frontend)).await;
                    }
                }
            }
            MessageType::Pin => {
                // TODO: How are pins removed? Would we recieve `PIN null`?
                let msg = match serde_json::from_value::<Msg>(json) {
                    Ok(msg) => msg,
                    Err(e) => {
                        eprintln!("Malformed JSON for PIN: {}", e);
                        return;
                    }
                };
                self.pin = Some(msg.clone());
                frontend.new_pin(&msg);
            }
            MessageType::Msg => {
                let msg = match serde_json::from_value::<Msg>(json) {
                    Ok(msg) => msg,
                    Err(e) => {
                        eprintln!("Malformed JSON for MSG: {}", e);
                        return;
                    }
                };
                self.history_add(msg.clone());
                frontend.new_msg(msg);
            }
            _ => {
                if self.debug {
                    eprintln!("recieve_msg not yet implemented for type: {:?}", msg_type);
                }
            }
        }
    }
}

/// Parses a message string into (MessageType, JSON Value)
/// Returns None if parsing fails
pub fn parse_message_string(message_str: &str) -> Option<(MessageType, Value)> {
    // Split message into type and JSON data
    let space_pos = match message_str.find(' ') {
        Some(pos) => pos,
        None => {
            eprintln!("Invalid message format.");
            eprintln!("Raw message: {}", message_str);
            return None;
        }
    };

    // Parse message type to enum
    let msg_type_str = &message_str[..space_pos];
    let msg_type = match MessageType::from_str(msg_type_str) {
        Some(msg_type) => msg_type,
        None => {
            eprintln!("Unknown message type: {}", msg_type_str);
            eprintln!("Raw message: {}", message_str);
            return None;
        }
    };

    // Parse JSON data
    let json_str = &message_str[space_pos + 1..];
    let json_data = match serde_json::from_str::<Value>(json_str) {
        Ok(data) => data,
        Err(e) => {
            eprintln!("Failed to parse JSON: {}", e);
            eprintln!("Raw message: {}", message_str);
            return None;
        }
    };

    Some((msg_type, json_data))
}
