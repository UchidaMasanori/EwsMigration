namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

/// <summary>
/// <see cref="InverterOptionKwSelector"/>(=Fysk01_ChkInvKw_OP)の移植テスト。
/// </summary>
public sealed class InverterOptionKwSelectorTests
{
    private static InverterOptionConstant C(double kw, string name)
        => new("       ", kw, name);

    [Fact]
    public void 入力kw以上となる最初の行の品名を返す()
    {
        InverterOptionConstant[] c = [C(5.0, "A"), C(7.5, "B"), C(11.0, "C")];

        string? r = InverterOptionKwSelector.SelectProductName(c, 6.0);

        Assert.Equal("B", r);
    }

    [Fact]
    public void 該当なしはnullを返す()
    {
        InverterOptionConstant[] c = [C(5.0, "A"), C(7.5, "B")];

        string? r = InverterOptionKwSelector.SelectProductName(c, 20.0);

        Assert.Null(r);
    }

    [Fact]
    public void 入力kwとkwが等しい境界ではその品名を返す()
    {
        InverterOptionConstant[] c = [C(5.0, "A"), C(7.5, "B")];

        string? r = InverterOptionKwSelector.SelectProductName(c, 5.0);

        Assert.Equal("A", r);
    }

    [Fact]
    public void 複数該当でも最初の行の品名を返す()
    {
        InverterOptionConstant[] c = [C(5.0, "A"), C(7.5, "B"), C(9.0, "C")];

        string? r = InverterOptionKwSelector.SelectProductName(c, 3.0);

        Assert.Equal("A", r);
    }

    [Fact]
    public void 空リストはnullを返す()
    {
        string? r = InverterOptionKwSelector.SelectProductName([], 3.0);

        Assert.Null(r);
    }
}
