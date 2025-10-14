use crate::chat::Msg;

pub trait Frontend {
    fn new_msg(&self, msg: Msg);
}
