using System.Text.Json;
using System.Text.Json.Serialization;
using DggNative.Models;
using DggNative.Serialization;

namespace DggNative.Tests;

internal class FlattenMe
{
    public required string Foo { get; set; }
    public required string Bar { get; set; }
}

[JsonConverter(typeof(JsonFlattenConverter<SerializeMe>))]
internal class SerializeMe
{
    [JsonFlatten]
    public required FlattenMe Nested { get; init; }
    
    public required int Baz { get; init; }
}

public class JsonFlattenConverterTest
{
    [Fact]
    public void SerializationTest()
    {
        var obj = new SerializeMe
        {
            Nested = new FlattenMe
            {
                Foo = "foo",
                Bar = "bar",
            },
            Baz = 123,
        };
        const string expected = "{\"Foo\":\"foo\",\"Bar\":\"bar\",\"Baz\":123}";
        var actual = JsonSerializer.Serialize(obj);
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void DeserializationTest()
    {
        const string json = "{\"Foo\":\"foo\",\"Bar\":\"bar\",\"Baz\":123}";
        var expected = new SerializeMe
        {
            Nested = new FlattenMe
            {
                Foo = "foo",
                Bar = "bar",
            },
            Baz = 123,
        };
        var actual = JsonSerializer.Deserialize<SerializeMe>(json);
        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public void DeserializationTestChatMessage()
    {
        const string json =
            """{"id":161078,"nick":"aerv","roles":["USER"],"features":[],"createdDate":"2022-08-22T21:25:36Z","watching":{"platform":"kick","id":"pizzaw"},"subscription":null,"timestamp":1763073293250,"data":"looking pokies"}""";
        var expected = new ChatMessage()
        {
            User = new User()
            {
                Id = 161078,
                Nick = "aerv",
                Roles = ["USER"],
                Features = [],
                CreatedDate = "2022-08-22T21:25:36Z",
                Watching = new Watching()
                {
                    Platform = "kick",
                    Id = "pizzaw"
                }
            },
            Data = "looking pokies",
            Timestamp = 1763073293250
        };
        
        var actual = JsonSerializer.Deserialize<ChatMessage>(json);
        Assert.Equivalent(expected, actual);
    }
    
    [Fact]
    public void SerializationTestChatMessage()
    {
        var obj = new ChatMessage()
        {
            User = new User()
            {
                Id = 161078,
                Nick = "aerv",
                Roles = ["USER"],
                Features = [],
                CreatedDate = "2022-08-22T21:25:36Z",
                Watching = new Watching()
                {
                    Platform = "kick",
                    Id = "pizzaw"
                }
            },
            Data = "looking pokies",
            Timestamp = 1763073293250
        };
        const string expected =
            """{"id":161078,"nick":"aerv","roles":["USER"],"features":[],"createdDate":"2022-08-22T21:25:36Z","watching":{"platform":"kick","id":"pizzaw"},"subscription":null,"timestamp":1763073293250,"data":"looking pokies"}""";
        
        var actual = JsonSerializer.Serialize(obj);
        Assert.Equal(expected, actual);
    }
}
