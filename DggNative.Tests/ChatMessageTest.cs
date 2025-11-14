using DggNative.Models;
using Newtonsoft.Json.Converters;

namespace DggNative.Tests
{
    public class ChatMessageTest
    {
        [Fact]
        public void TestChatMessageDeserialization()
        {
            var expected = new ChatMessage(
                new User(
                    71655, "RaspberryPine", ["USER"], [], "2017-07-27T06:39:54Z",
                    new Embed("kick", "pizzaw"),
                    new Subscription(1, "destiny.gg")
                ),
                "looking",
                1763073290801
            );
            var json = "{\"id\":71655,\"nick\":\"RaspberryPine\",\"roles\":[\"USER\"],\"features\":[],\"createdDate\":\"2017-07-27T06:39:54Z\",\"watching\":{\"platform\":\"kick\",\"id\":\"pizzaw\"},\"subscription\":{\"tier\":1,\"source\":\"destiny.gg\"},\"timestamp\":1763073290801,\"data\":\"looking\"}";
            var actual = ChatMessage.Deserialize(json);
            Assert.Equivalent(expected, actual);
        }

        [Fact]
        public void TestChatMessageSerialization()
        {
            var expected = "{\"id\":71655,\"nick\":\"RaspberryPine\",\"roles\":[\"USER\"],\"features\":[],\"createdDate\":\"2017-07-27T06:39:54Z\",\"watching\":{\"platform\":\"kick\",\"id\":\"pizzaw\"},\"subscription\":{\"tier\":1,\"source\":\"destiny.gg\"},\"timestamp\":1763073290801,\"data\":\"looking\"}";
            var obj = new ChatMessage(
                new User(
                    71655, "RaspberryPine", ["USER"], [], "2017-07-27T06:39:54Z",
                    new Embed("kick", "pizzaw"),
                    new Subscription(1, "destiny.gg")
                ),
                "looking",
                1763073290801
            );
            var actual = obj.Serialize();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestChatMessageDeserialization_NullAndEmptyValues()
        {
            var expected = new ChatMessage(
                new User(71655, "RaspberryPine", [], [], "2017-07-27T06:39:54Z", null, null),
                "looking",
                1763073290801
            );
            var json = "{\"id\":71655,\"nick\":\"RaspberryPine\",\"roles\":[],\"features\":[],\"createdDate\":\"2017-07-27T06:39:54Z\",\"watching\":null,\"subscription\":null,\"timestamp\":1763073290801,\"data\":\"looking\"}";
            var actual = ChatMessage.Deserialize(json);
            Assert.Equivalent(expected, actual);
        }

        [Fact]
        public void TestChatMessageSerialization_NullAndEmptyValues()
        {
            var expected = "{\"id\":71655,\"nick\":\"RaspberryPine\",\"roles\":[],\"features\":[],\"createdDate\":\"2017-07-27T06:39:54Z\",\"watching\":null,\"subscription\":null,\"timestamp\":1763073290801,\"data\":\"looking\"}";
            var obj = new ChatMessage(
                new User(71655, "RaspberryPine", [], [], "2017-07-27T06:39:54Z", null, null),
                "looking",
                1763073290801
            );
            var actual = obj.Serialize();
            Assert.Equal(expected, actual);
        }
    }
}