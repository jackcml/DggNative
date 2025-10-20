use crate::chat::{Msg, User};

pub trait Frontend {
    fn connected_as(&self, maybe_user: &Option<User>);
    fn new_msg(&self, msg: Msg);
    fn new_pin(&self, msg: &Msg);
}
