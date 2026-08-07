namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

/// <summary>
/// <see cref="InverterKwSelector.SelectKwByParameter"/>(=Fysk01_ChkInvKwPara)の移植テスト。
/// </summary>
public sealed class InverterKwParaSelectorTests
{
    private static InverterConstant C(double kw, params string[] slots)
    {
        string[] types = ["", "", "", "", "", "", ""];
        for (int i = 0; i < slots.Length && i < 7; i++)
        {
            types[i] = slots[i];
        }
        return new InverterConstant(types, kw);
    }

    private static string[] DType(params string[] slots)
    {
        string[] t = ["", "", "", "", "", "", ""];
        for (int i = 0; i < slots.Length && i < 7; i++)
        {
            t[i] = slots[i];
        }
        return t;
    }

    [Fact]
    public void 全スロット一致で入力kw以上の行のkwと選択タイプを返す()
    {
        InverterConstant[] c = [C(5.0, "T1", "T2", "T3")];

        InverterKwSelection r = InverterKwSelector.SelectKwByParameter(c, 3.0, DType("T1", "T2", "T3"));

        Assert.Equal(5.0, r.Kw);
        Assert.NotNull(r.SelectedType);
        Assert.Equal("T1     ", r.SelectedType![0]);
        Assert.Equal("T2     ", r.SelectedType![1]);
        Assert.Equal("T3     ", r.SelectedType![2]);
    }

    [Fact]
    public void 該当なしはkw0で選択タイプはnull()
    {
        InverterConstant[] c = [C(5.0, "T1", "T2", "T3")];

        InverterKwSelection r = InverterKwSelector.SelectKwByParameter(c, 99.0, DType("T1", "T2", "T3"));

        Assert.Equal(0.0, r.Kw);
        Assert.Null(r.SelectedType);
    }

    [Fact]
    public void タイプが全く一致しなければkw0でnull()
    {
        InverterConstant[] c = [C(5.0, "T1", "T2", "T3")];

        InverterKwSelection r = InverterKwSelector.SelectKwByParameter(c, 3.0, DType("XX", "YY", "ZZ"));

        Assert.Equal(0.0, r.Kw);
        Assert.Null(r.SelectedType);
    }

    [Fact]
    public void 外側ループは打切らず最も緩い一致段が優先される()
    {
        // row0 はスロット0のみ一致(緩い一致段 i=1 で採用)、row1 は全一致(厳しい段で採用)。
        // C原典は外側 for(i) を break しないため、最終段 i=1 の最初の一致 row0 が上書きして優先される。
        InverterConstant[] c = [C(8.0, "T1", "XX"), C(5.0, "T1", "T2", "T3")];

        InverterKwSelection r = InverterKwSelector.SelectKwByParameter(c, 3.0, DType("T1", "T2", "T3"));

        Assert.Equal(8.0, r.Kw);
        Assert.Equal("T1     ", r.SelectedType![0]);
        Assert.Equal("XX     ", r.SelectedType![1]);
    }

    [Fact]
    public void 各段で入力kw以上となる最初の行を採用する()
    {
        InverterConstant[] c = [C(2.0, "T1"), C(6.0, "T1"), C(9.0, "T1")];

        InverterKwSelection r = InverterKwSelector.SelectKwByParameter(c, 5.0, DType("T1"));

        Assert.Equal(6.0, r.Kw);
    }
}
