use serde::{Deserialize, Serialize};
use serde_json::Value;
use std::collections::{HashMap, VecDeque};

use crate::Frontend;

// MessageTypes that all clients can send:
// - MSG, PING, PONG, PRIVMSG, DIE, CASTVOTE
// User clients can send, but server will reply with ERR(nopermission):
// - MUTE, UNMUTE, BAN, UNBAN, SUBONLY, BROADCAST, RELOADUSERS, PIN
// Permission checked client side (ADMIN, MODERATOR, BOT):
// - STARTPOLL, STOPPOLL
// Permission checked client side (ADMIN, MODERATOR):
// - ADDPHRASE, REMOVEPHRASE
// - Action taken through GUI: REMOVEEVENT

#[derive(Debug)]
pub enum MessageType {
    // Types handled by available chat server implementation (last updated 2022)
    Join,        // User joins
    Quit,        // User leaves
    Broadcast,   // Special message type, highlighted
    Msg,         // Chat message
    Mute,        // Mute info, for self or another user
    Unmute,      // Unmute info, for self or another user
    Ban,         // Ban info, for self or another user
    Unban,       // Unban info, for self or another user
    SubOnly, // Indicates change in subonly mode: data.data == "on" if enabled, otherwise disabled
    Err,     // Communicates errors: toomanyconnections, banned, muted, bannedphrase, nopermission
    PrivMsg, // Private message recieved
    PrivMsgSent, // Private message sent successfully
    Names,   // Lists current connected users
    Ping,
    Pong,
    // Refresh: No longer used; see destinygg/chat-gui commit 1c7f9cf.

    // Other types observed
    Me,         // Provides current logged-in user
    History,    // History of recent chat messages
    Pin,        // Pinned message
    UpdateUser, // Provides updated information about a user by id (since username can change)

    // Other types handled by the current JS chat client
    Reload, // Forces client reload, likely used for live chat client updates, so may not apply to us.

    // Polling
    PollStart,
    PollStop,
    VoteCast,

    // Event bar
    Subscription,
    GiftSub,
    MassGift,
    Donation,
    PaidEvents, // Like History but for currently displayed paid events

    AddPhrase,    // Add a banned phrase to chat
    RemovePhrase, // Remove a banned phrase from chat

    Death, // Messages sent when users /die
}

impl MessageType {
    pub fn from_str(s: &str) -> Option<Self> {
        match s {
            "PING" => Some(MessageType::Ping),
            "PONG" => Some(MessageType::Pong),
            "ME" => Some(MessageType::Me),
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
    pub id: u64,
    pub nick: String,
    pub roles: Vec<String>,
    pub features: Vec<String>,
    pub created_date: String,
    pub watching: Option<Watching>,
}

#[derive(Serialize, Deserialize)]
pub struct Names {
    #[serde(rename = "connectioncount")]
    connection_count: usize,
    users: Vec<User>,
}

#[derive(Serialize, Deserialize)]
pub struct JoinOrQuit {
    #[serde(flatten)]
    pub user: User,
    timestamp: f64,
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
    users: HashMap<u64, User>,
}

impl Chat {
    pub fn new(debug: bool, history_len: usize) -> Self {
        Chat {
            user: None,
            pin: None,
            debug,
            history_len,
            history: VecDeque::new(),
            users: HashMap::new(),
        }
    }

    fn history_add(&mut self, msg: Msg) {
        if self.history.len() >= self.history_len {
            self.history.pop_front();
        }
        self.history.push_back(msg);
    }

    /// Helper method to deserialize JSON with consistent error handling
    fn deserialize_json<T: serde::de::DeserializeOwned>(
        &self,
        json: Value,
        msg_type: MessageType,
    ) -> Option<T> {
        match serde_json::from_value::<T>(json) {
            Ok(msg) => Some(msg),
            Err(e) => {
                eprintln!("Malformed JSON for {:?}: {}", msg_type, e);
                None
            }
        }
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
                    self.user = self.deserialize_json::<User>(json, msg_type);
                }
                frontend.connected_as(&self.user);
            }
            MessageType::History => {
                let Some(back_history) = self.deserialize_json::<Vec<String>>(json, msg_type)
                else {
                    return;
                };

                for msg_str in back_history {
                    // Parse each historical message string and process it
                    if let Some((msg_type, json_data)) = parse_message_string(&msg_str) {
                        // Recursion in async fn requires boxing
                        Box::pin(self.recieve_msg(msg_type, json_data, frontend)).await;
                    }
                }
            }
            MessageType::Names => {
                let Some(msg) = self.deserialize_json::<Names>(json, msg_type) else {
                    return;
                };
                // chat.users is currently unused, but will be useful for name autocomplete and bringing up user info
                self.users = msg.users.into_iter().map(|user| (user.id, user)).collect();
                if self.debug {
                    println!("Serving {} connections.", msg.connection_count);
                }
            }
            MessageType::Join => {
                let Some(msg) = self.deserialize_json::<JoinOrQuit>(json, msg_type) else {
                    return;
                };
                self.users.insert(msg.user.id, msg.user);
            }
            MessageType::UpdateUser => {
                let Some(msg) = self.deserialize_json::<User>(json, msg_type) else {
                    return;
                };
                self.users.insert(msg.id, msg);
            }
            MessageType::Quit => {
                let Some(msg) = self.deserialize_json::<JoinOrQuit>(json, msg_type) else {
                    return;
                };
                self.users.remove(&msg.user.id);
            }
            MessageType::Pin => {
                // TODO: How are pins removed? Would we recieve `PIN null`?
                let Some(msg) = self.deserialize_json::<Msg>(json, msg_type) else {
                    return;
                };
                self.pin = Some(msg.clone());
                frontend.new_pin(&msg);
            }
            MessageType::Msg => {
                let Some(msg) = self.deserialize_json::<Msg>(json, msg_type) else {
                    return;
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
