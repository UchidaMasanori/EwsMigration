using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CompoSpaceCutter"/>(【C原典】cpspcutr.c の FyCpSpcutr)の単体テスト。
/// </summary>
public sealed class CompoSpaceCutterTests
{
    [Theory]
    [InlineData("  名  古 屋  ", "名  古 屋")]  // 先頭/末尾スペース除去・内部は保持
    [InlineData("abc", "abc")]                  // 変化なし
    [InlineData("  abc", "abc")]                // 先頭スペースのみ
    [InlineData("abc  ", "abc")]                // 末尾スペースのみ
    [InlineData("a b c", "a b c")]              // 内部スペース保持
    public void 前後の半角スペースを除去する(string input, string expected)
    {
        Assert.Equal(expected, CompoSpaceCutter.CutSpaces(input));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("     ", "")]      // 全て半角スペース
    public void 空または全空白は空文字を返す(string? input, string expected)
    {
        Assert.Equal(expected, CompoSpaceCutter.CutSpaces(input));
    }

    [Fact]
    public void 末尾の改行を除去する()
    {
        Assert.Equal("abc", CompoSpaceCutter.CutSpaces("abc\n"));
    }

    [Fact]
    public void 末尾の空白と改行の混在を除去する()
    {
        Assert.Equal("abc", CompoSpaceCutter.CutSpaces("abc \n \n"));
    }

    [Fact]
    public void 先頭の改行は除去しない()
    {
        // 【C原典】先頭ループは半角スペースのみ読み飛ばすため改行は保持される。
        Assert.Equal("\nabc", CompoSpaceCutter.CutSpaces("\nabc  "));
    }

    [Fact]
    public void 先頭スペース後が改行のみなら空文字を返す()
    {
        // 【C原典】start が改行を指し以降も改行のみだと e は NULL のままで空文字。
        Assert.Equal("", CompoSpaceCutter.CutSpaces("  \n\n"));
    }

    [Fact]
    public void 内部の改行は保持する()
    {
        Assert.Equal("a\nb", CompoSpaceCutter.CutSpaces("  a\nb  "));
    }
}
