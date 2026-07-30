using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 形状タイプ展開(<see cref="ShapeTypeExpander"/>)の移植検証。
/// 【C原典】Fysk01_Type_Check2(Fysk01.c:3224) + type_tbl2(fyrt819.h)。
/// </summary>
public sealed class ShapeTypeExpanderTests
{
    private static string[] Types(params string[] values)
    {
        string[] result = ["", "", "", "", "", "", ""];
        for (int i = 0; i < values.Length && i < 7; i++)
        {
            result[i] = values[i];
        }
        return result;
    }

    [Fact]
    public void 予約語がテーブルにないとタイプ位置1でdtype1を返す()
    {
        ShapeTypeExpansion result = ShapeTypeExpander.Expand("XX ", Types("A", "B"));

        Assert.Equal(1, result.TypeIndex);
        Assert.Single(result.ShapeTypes);
        Assert.Equal("B      ", result.ShapeTypes[0]);
    }

    [Fact]
    public void THRの空タイプは1A1Bと1Cに展開する()
    {
        ShapeTypeExpansion result = ShapeTypeExpander.Expand("THR ", Types("", ""));

        Assert.Equal(1, result.TypeIndex);
        Assert.Equal(2, result.ShapeTypes.Count);
        Assert.Equal("1A1B   ", result.ShapeTypes[0]);
        Assert.Equal("1C     ", result.ShapeTypes[1]);
    }

    [Fact]
    public void ELBのTLAはTLAとNTに展開する()
    {
        ShapeTypeExpansion result = ShapeTypeExpander.Expand("ELB ", Types("dummy", "TLA"));

        Assert.Equal(1, result.TypeIndex);
        Assert.Equal(2, result.ShapeTypes.Count);
        Assert.Equal("TLA    ", result.ShapeTypes[0]);
        Assert.Equal("NT     ", result.ShapeTypes[1]);
    }

    [Fact]
    public void ELBのタイプがsym不一致ならdtype1そのまま()
    {
        ShapeTypeExpansion result = ShapeTypeExpander.Expand("ELB ", Types("dummy", "ZZ"));

        Assert.Single(result.ShapeTypes);
        Assert.Equal("ZZ     ", result.ShapeTypes[0]);
    }

    [Fact]
    public void CTのKTは3タイプに展開する()
    {
        ShapeTypeExpansion result = ShapeTypeExpander.Expand("CT  ", Types("dummy", "KT"));

        Assert.Equal(3, result.ShapeTypes.Count);
        Assert.Equal("KT     ", result.ShapeTypes[0]);
        Assert.Equal("LT     ", result.ShapeTypes[1]);
        Assert.Equal("KE     ", result.ShapeTypes[2]);
    }

    [Fact]
    public void PBSはタイプ位置5でNOTHINGをWPに展開する()
    {
        ShapeTypeExpansion result = ShapeTypeExpander.Expand("PBS ", Types("", "", "", "", "", "NOTHING"));

        Assert.Equal(5, result.TypeIndex);
        Assert.Equal(2, result.ShapeTypes.Count);
        Assert.Equal("NOTHING", result.ShapeTypes[0]);
        Assert.Equal("WP     ", result.ShapeTypes[1]);
    }

    [Fact]
    public void STMの空タイプは4接点タイプに並べ替える()
    {
        ShapeTypeExpansion result = ShapeTypeExpander.Expand("STM ", Types("", ""));

        Assert.Equal(4, result.ShapeTypes.Count);
        Assert.Equal("FC     ", result.ShapeTypes[1]);
        Assert.Equal("1C     ", result.ShapeTypes[2]);
        Assert.Equal("2C     ", result.ShapeTypes[3]);
    }

    [Fact]
    public void STMの1Cは1C2CFCの順に並べ替える()
    {
        ShapeTypeExpansion result = ShapeTypeExpander.Expand("STM ", Types("", "1C"));

        Assert.Equal(3, result.ShapeTypes.Count);
        Assert.Equal("1C     ", result.ShapeTypes[0]);
        Assert.Equal("2C     ", result.ShapeTypes[1]);
        Assert.Equal("FC     ", result.ShapeTypes[2]);
    }
}
