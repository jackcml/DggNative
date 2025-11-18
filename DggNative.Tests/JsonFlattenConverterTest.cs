using System.Text.Json;
using System.Text.Json.Serialization;
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
}
