using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="PropertyStringTrimmer"/>(【C原典】PropTrimSpace)の単体テスト。
/// </summary>
public class PropertyStringTrimmerTests
{
    [Theory]
    [InlineData("  ABC  ", "ABC")]
    [InlineData("ABC   ", "ABC")]
    [InlineData("   ABC", "ABC")]
    [InlineData("ABC", "ABC")]
    [InlineData("  A B C  ", "A B C")]     // 内部の半角スペースは残す
    [InlineData("     ", "")]              // 全て半角スペースなら空
    [InlineData("", "")]
    public void TrimSpaces_前後の半角スペースを除去する(string input, string expected)
    {
        Assert.Equal(expected, PropertyStringTrimmer.TrimSpaces(input));
    }

    [Fact]
    public void TrimSpaces_全角スペースやタブは除去しない()
    {
        Assert.Equal("\tX　", PropertyStringTrimmer.TrimSpaces("\tX　 "));
    }

    [Fact]
    public void TrimSpaces_nullは空文字()
    {
        Assert.Equal("", PropertyStringTrimmer.TrimSpaces(null));
    }
}
