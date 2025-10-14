use crate::chat::Msg;
use crate::frontend::Frontend;

use colored::Colorize;

pub struct Tui {}
impl Frontend for Tui {
    fn new_msg(&self, msg: Msg) {
        println!("{}: {}", msg.nick.bold(), msg.data);
    }
}
