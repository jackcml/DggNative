using System.Collections.Frozen;
using DggNative.Rendering;

namespace DggNative.Tests;

public class EmoteTokenizerTest
{
    [Fact]
    public void Tokenize_ReturnsSingleTextPart_WhenThereAreNoEmotes()
    {
        var parts = EmoteTokenizer.Tokenize("hello world", BuildCatalog());

        var part = Assert.Single(parts);
        Assert.False(part.IsEmote);
        Assert.Equal("hello world", part.Text);
    }

    [Fact]
    public void Tokenize_ReplacesWhitespaceDelimitedTokens()
    {
        var parts = EmoteTokenizer.Tokenize("cozy  Abathur\tAware", BuildCatalog("Abathur", "Aware"));

        Assert.Collection(parts,
            part =>
            {
                Assert.False(part.IsEmote);
                Assert.Equal("cozy  ", part.Text);
            },
            part =>
            {
                Assert.True(part.IsEmote);
                Assert.Equal("Abathur", part.Text);
                Assert.Equal("https://example.test/Abathur.png", part.ImageUrl);
            },
            part =>
            {
                Assert.False(part.IsEmote);
                Assert.Equal("\t", part.Text);
            },
            part =>
            {
                Assert.True(part.IsEmote);
                Assert.Equal("Aware", part.Text);
                Assert.Equal("https://example.test/Aware.png", part.ImageUrl);
            });
    }

    [Fact]
    public void Tokenize_MatchesMessageEdges()
    {
        var parts = EmoteTokenizer.Tokenize("Abathur hi Aware", BuildCatalog("Abathur", "Aware"));

        Assert.Collection(parts,
            part =>
            {
                Assert.True(part.IsEmote);
                Assert.Equal("Abathur", part.Text);
            },
            part =>
            {
                Assert.False(part.IsEmote);
                Assert.Equal(" hi ", part.Text);
            },
            part =>
            {
                Assert.True(part.IsEmote);
                Assert.Equal("Aware", part.Text);
            });
    }

    [Fact]
    public void Tokenize_DoesNotMatch_WhenTokenTouchesPunctuation()
    {
        var parts = EmoteTokenizer.Tokenize("Abathur! buddy-nutcheck.", BuildCatalog("Abathur", "buddy-nutcheck"));

        var part = Assert.Single(parts);
        Assert.False(part.IsEmote);
        Assert.Equal("Abathur! buddy-nutcheck.", part.Text);
    }

    private static FrozenDictionary<string, string> BuildCatalog(params string[] names)
    {
        return names
            .ToDictionary(static name => name, static name => $"https://example.test/{name}.png", StringComparer.Ordinal)
            .ToFrozenDictionary(StringComparer.Ordinal);
    }
}
