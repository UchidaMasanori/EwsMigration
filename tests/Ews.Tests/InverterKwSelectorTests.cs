namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

/// <summary>
/// <see cref="InverterKwSelector"/>(=Fysk01_ChkInvKw)の移植テスト。
/// </summary>
public sealed class InverterKwSelectorTests
{
    private static InverterConstant C(string type0, double kw)
        => new([type0, "", "", "", "", "", ""], kw);

    private static string[] DType(string slot1)
        => ["", slot1, "", "", "", "", ""];

    [Fact]
    public void 一致タイプで入力kw以上となる最初のkwを返す()
    {
        InverterConstant[] c = [C("INV1", 5.5), C("INV1", 7.5), C("INV1", 11.0)];

        double r = InverterKwSelector.SelectKw(c, 6.0, DType("INV1"));

        Assert.Equal(7.5, r);
    }

    [Fact]
    public void 入力kwが全行を超える場合は0を返す()
    {
        InverterConstant[] c = [C("INV1", 5.5), C("INV1", 7.5)];

        double r = InverterKwSelector.SelectKw(c, 20.0, DType("INV1"));

        Assert.Equal(0.0, r);
    }

    [Fact]
    public void タイプ不一致のみなら0を返す()
    {
        InverterConstant[] c = [C("INV1", 5.5), C("INV1", 7.5)];

        double r = InverterKwSelector.SelectKw(c, 6.0, DType("XXX"));

        Assert.Equal(0.0, r);
    }

    [Fact]
    public void タイプ帯を通り過ぎたら打切り後続の一致は無視する()
    {
        InverterConstant[] c = [C("INV1", 5.5), C("OTH", 7.5), C("INV1", 7.0)];

        double r = InverterKwSelector.SelectKw(c, 6.0, DType("INV1"));

        Assert.Equal(0.0, r);
    }

    [Fact]
    public void 入力kwとkwが等しい境界ではそのkwを返す()
    {
        InverterConstant[] c = [C("INV1", 5.5)];

        double r = InverterKwSelector.SelectKw(c, 5.5, DType("INV1"));

        Assert.Equal(5.5, r);
    }

    [Fact]
    public void 先頭がタイプ不一致でも打切らず後続の一致を採用する()
    {
        InverterConstant[] c = [C("OTH", 5.0), C("INV1", 7.0)];

        double r = InverterKwSelector.SelectKw(c, 6.0, DType("INV1"));

        Assert.Equal(7.0, r);
    }
}
