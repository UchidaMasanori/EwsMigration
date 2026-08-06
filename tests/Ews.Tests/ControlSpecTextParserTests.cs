using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlSpecTextParser"/>(【C原典】Fyss1k.c の制御仕様記述テキスト抽出関数群)の単体テスト。
/// </summary>
public sealed class ControlSpecTextParserTests
{
    [Theory]
    [InlineData("A B C", "ABC")]
    [InlineData("  x ", "x")]
    [InlineData("MC 3", "MC3")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SpaceNeguri_半角スペースを除去する(string? input, string expected)
    {
        Assert.Equal(expected, ControlSpecTextParser.SpaceNeguri(input));
    }

    [Fact]
    public void SetBtwnData_startからendまでを含めて取り出し入力は不変()
    {
        int ret = ControlSpecTextParser.SetBtwnData("abcPT12)xyz", out string output, "PT", ")");

        Assert.Equal(0, ret);
        Assert.Equal("PT12)", output);
    }

    [Fact]
    public void SetBtwnData_startが無ければ1で空を返す()
    {
        int ret = ControlSpecTextParser.SetBtwnData("abcxyz", out string output, "PT", ")");

        Assert.Equal(1, ret);
        Assert.Equal("", output);
    }

    [Fact]
    public void SetBtwnData_endが無ければ2で空を返す()
    {
        int ret = ControlSpecTextParser.SetBtwnData("abcPT12", out string output, "PT", ")");

        Assert.Equal(2, ret);
        Assert.Equal("", output);
    }

    [Fact]
    public void GetBtwnData_startとendの間を取り出し入力から除去する()
    {
        string input = "abc(x)def";
        int ret = ControlSpecTextParser.GetBtwnData(ref input, out string output, "(", ")");

        Assert.Equal(0, ret);
        Assert.Equal("x", output);
        Assert.Equal("abcdef", input);
    }

    [Fact]
    public void GetBtwnData_中身が空なら1を返しstartとendは除去される()
    {
        string input = "abc()def";
        int ret = ControlSpecTextParser.GetBtwnData(ref input, out string output, "(", ")");

        Assert.Equal(1, ret);
        Assert.Equal("", output);
        Assert.Equal("abcdef", input);
    }

    [Fact]
    public void GetAtCharData_先頭からend文字まで取り出し入力から除去する()
    {
        string input = "abc,def";
        int ret = ControlSpecTextParser.GetAtCharData(ref input, out string output, ',');

        Assert.Equal(0, ret);
        Assert.Equal("abc", output);
        Assert.Equal("def", input);
    }

    [Fact]
    public void GetAtCharData_end文字が無ければ1で入力全体を返し入力は不変()
    {
        string input = "abc";
        int ret = ControlSpecTextParser.GetAtCharData(ref input, out string output, ',');

        Assert.Equal(1, ret);
        Assert.Equal("abc", output);
        Assert.Equal("abc", input);
    }

    [Fact]
    public void GetIntrData_括弧の外のカンマまでを取り出し入力から除去する()
    {
        string input = "AB<C(1,2),D>EE";
        string output = ControlSpecTextParser.GetIntrData(ref input);

        Assert.Equal("<C(1,2)", output);
        Assert.Equal("AB,D>EE", input);
    }

    [Fact]
    public void GetIntrData_括弧の外にカンマが無ければ末尾まで取り出す()
    {
        string input = "X<A(1)B>";
        string output = ControlSpecTextParser.GetIntrData(ref input);

        Assert.Equal("<A(1)B>", output);
        Assert.Equal("X", input);
    }

    [Theory]
    [InlineData("MC123", "MC")]
    [InlineData("PT(1)abc", "PT(1)")]
    [InlineData("123", "")]
    [InlineData("RSW*2", "RSW")]
    public void GetSgkkYoyaku_先頭の予約語を取り出す(string input, string expected)
    {
        Assert.Equal(expected, ControlSpecTextParser.GetSgkkYoyaku(input));
    }

    [Theory]
    [InlineData("MC*3", 3)]
    [InlineData("MC", 1)]
    [InlineData("MC*", 0)]
    [InlineData("MC*12ab", 12)]
    public void GetSgkkKosu_アスタリスク後の数字を個数として返す(string input, int expected)
    {
        Assert.Equal(expected, ControlSpecTextParser.GetSgkkKosu(input));
    }
}
