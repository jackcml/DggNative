use crate::chat::{Msg, User};
use crate::frontend::Frontend;

use colored::Colorize;

pub struct Tui {
    pub debug: bool,
}
impl Frontend for Tui {
    fn connected_as(&self, maybe_user: &Option<User>) {
        if let Some(user) = maybe_user {
            println!("Connected as {}.", user.nick);
        } else {
            // TODO: Disable message input if not logged in
            if self.debug {
                eprintln!("Connected without auth.");
            }
        }
    }

    fn new_msg(&self, msg: Msg) {
        println!("{}: {}", msg.user.nick.bold(), msg.data);
    }
}
