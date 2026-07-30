using Ews.Analysis;
using Ews.Domain.Circuits;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 回路設計エリアからの回路内容記述取得(Fysk11_FYDF805_KkGet / _Mae / _Ato)の移植検証。
///
/// 【C原典】Fysk11.c(toku/sekkei/src)。桁位置(keta)をバイト位置として kairoar を走査し、
/// 対象機器/1 個前/1 個後の記述を切り出す。区切りは ',' と '--'。
/// </summary>
public sealed class CircuitDescriptionAreaTests
{
    private static CircuitDescriptionArea Area(params (int line, string text, char cmd)[] rows)
    {
        var lines = rows
            .Select(r => new CircuitDescriptionLine
            {
                LineNumber = r.line,
                CircuitText = r.text,
                Command = r.cmd,
            })
            .ToList();
        return new CircuitDescriptionArea(lines);
    }

    [Theory]
    [InlineData("003", "MCB")]
    [InlineData("007", "WL")]
    [InlineData("010", "LED")]
    public void KkGet_桁位置の対象機器記述を切り出す(string column, string expected)
    {
        CircuitDescriptionArea area = Area((5, "P,MCB,WL,LED,", ' '));

        Assert.Equal(expected, area.GetDescriptionAt("005", column));
    }

    [Fact]
    public void KkGet_区切りが無ければ末尾まで返す_C忠実に空白込み()
    {
        CircuitDescriptionArea area = Area((5, "WL", ' '));

        string result = area.GetDescriptionAt("005", "001");

        Assert.StartsWith("WL", result);
        Assert.Equal("WL", result.TrimEnd());
    }

    [Fact]
    public void KkGet_削除行はスキップする()
    {
        CircuitDescriptionArea area = Area(
            (5, "DELLINE", 'D'),
            (5, "P,KEEP,", ' '));

        Assert.Equal("KEEP", area.GetDescriptionAt("005", "003"));
    }

    [Fact]
    public void KkGet_桁が1行を超える場合は次行へ折り返す()
    {
        CircuitDescriptionArea area = Area(
            (5, "DUMMY,", ' '),
            (6, "P,MULTI,", ' '));

        // 桁 203 = 200 + 3 → 行 +1(6 行目)・桁 3。
        Assert.Equal("MULTI", area.GetDescriptionAt("005", "203"));
    }

    [Fact]
    public void KkGet_該当行が無ければ空を返す()
    {
        CircuitDescriptionArea area = Area((5, "P,WL,", ' '));

        Assert.Equal(string.Empty, area.GetDescriptionAt("009", "003"));
    }

    [Theory]
    [InlineData("AAA--WL", "006")]   // '--' 区切り
    [InlineData("AAA,WL", "005")]    // ',' 区切り
    public void KkGetMae_1個前の記述を切り出す(string text, string column)
    {
        CircuitDescriptionArea area = Area((5, text, ' '));

        Assert.Equal("AAA", area.GetPrecedingDescription("005", column));
    }

    [Fact]
    public void KkGetMae_前に記述が無ければ空を返す()
    {
        CircuitDescriptionArea area = Area((5, "WL", ' '));

        Assert.Equal(string.Empty, area.GetPrecedingDescription("005", "001"));
    }

    [Fact]
    public void KkGetAto_ハイフン区切りの1個後を切り出す()
    {
        CircuitDescriptionArea area = Area((5, "WL--LED,", ' '));

        Assert.Equal("LED", area.GetFollowingDescription("005", "001"));
    }

    [Fact]
    public void KkGetAto_カンマ区切りの1個後を切り出す()
    {
        CircuitDescriptionArea area = Area((5, "F,MCB,", ' '));

        Assert.Equal("MCB", area.GetFollowingDescription("005", "001"));
    }

    [Fact]
    public void KkGetAto_末尾区切りが無ければC忠実に空白込みで返す()
    {
        CircuitDescriptionArea area = Area((5, "WL--LED", ' '));

        string result = area.GetFollowingDescription("005", "001");

        Assert.StartsWith("LED", result);
        Assert.Equal("LED", result.TrimEnd());
    }

    [Fact]
    public void KkGetAto_カンマとハイフン両方ありカンマが先()
    {
        CircuitDescriptionArea area = Area((5, "F,A--B", ' '));

        Assert.Equal("A", area.GetFollowingDescription("005", "001"));
    }

    [Fact]
    public void KkGetAto_カンマとハイフン両方ありハイフンが先()
    {
        CircuitDescriptionArea area = Area((5, "F--A,B", ' '));

        Assert.Equal("A", area.GetFollowingDescription("005", "001"));
    }

    [Fact]
    public void KkGetAto_後に記述が無ければ空を返す()
    {
        CircuitDescriptionArea area = Area((5, "WL", ' '));

        Assert.Equal(string.Empty, area.GetFollowingDescription("005", "001"));
    }
}
