using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CircuitAreaRewriter"/>(【C原典】Fysk00_Area_Rewrite/Set_Kairo/Set_Datachi)の単体テスト。
/// </summary>
public class CircuitAreaRewriterTests
{
    private static (ElectricalParameters[] epa, NumericElectricalParameters[] sep) NewAreas()
    {
        var epa = new[]
        {
            new ElectricalParameters(),
            new ElectricalParameters(),
            new ElectricalParameters(),
        };
        var sep = new[]
        {
            new NumericElectricalParameters(),
            new NumericElectricalParameters(),
            new NumericElectricalParameters(),
        };
        return (epa, sep);
    }

    [Fact]
    public void Rewrite_ATは9桁3小数のゼロ埋めで上位機器へ書き戻す()
    {
        (ElectricalParameters[] epa, NumericElectricalParameters[] sep) = NewAreas();
        sep[1].At = 100.0;
        var flags = new AreaRewriteFlags();
        flags.At[0] = true;

        short ret = CircuitAreaRewriter.Rewrite(epa, sep, flags);

        Assert.Equal(CircuitAreaRewriter.Good, ret);
        Assert.Equal("00100.000", epa[1].At);
        // 他フィールド・下位機器は不変。
        Assert.Equal(new string('0', 9), epa[1].Af);
        Assert.Equal(new string('0', 9), epa[2].At);
    }

    [Fact]
    public void Rewrite_AMは3桁0小数で書き戻す()
    {
        (ElectricalParameters[] epa, NumericElectricalParameters[] sep) = NewAreas();
        sep[1].Am = 5.0;
        var flags = new AreaRewriteFlags();
        flags.Am[0] = true;

        CircuitAreaRewriter.Rewrite(epa, sep, flags);

        Assert.Equal("005", epa[1].Am);
    }

    [Fact]
    public void Rewrite_MAフラグはMA0からMA2まで一括で書き戻す()
    {
        (ElectricalParameters[] epa, NumericElectricalParameters[] sep) = NewAreas();
        sep[2].Ma[0] = 30.0;
        sep[2].Ma[1] = 15.0;
        sep[2].Ma[2] = 0.0;
        var flags = new AreaRewriteFlags();
        flags.Ma[1] = true; // 添字1=下位機器(sep[2]/epa[2])。

        CircuitAreaRewriter.Rewrite(epa, sep, flags);

        Assert.Equal("0030", epa[2].Ma[0]);
        Assert.Equal("0015", epa[2].Ma[1]);
        Assert.Equal("0000", epa[2].Ma[2]);
    }

    [Fact]
    public void Rewrite_AF_A2も所定桁で書き戻す()
    {
        (ElectricalParameters[] epa, NumericElectricalParameters[] sep) = NewAreas();
        sep[1].Af = 225.0;
        sep[1].A2 = 60.5;
        var flags = new AreaRewriteFlags();
        flags.Af[0] = true;
        flags.A2[0] = true;

        CircuitAreaRewriter.Rewrite(epa, sep, flags);

        Assert.Equal("00225.000", epa[1].Af);
        Assert.Equal("00060.500", epa[1].A2);
    }

    [Fact]
    public void Rewrite_フラグ未指定なら回路側は不変()
    {
        (ElectricalParameters[] epa, NumericElectricalParameters[] sep) = NewAreas();
        sep[1].At = 100.0;
        var flags = new AreaRewriteFlags();

        short ret = CircuitAreaRewriter.Rewrite(epa, sep, flags);

        Assert.Equal(CircuitAreaRewriter.Good, ret);
        Assert.Equal(new string('0', 9), epa[1].At);
    }

    [Fact]
    public void Rewrite_書式幅を超える値はフィールド幅へ切り詰める()
    {
        // 【C原典】memcpy(epaat, str, 9): "123456.789"(10桁)は先頭9桁へ切り詰め。
        (ElectricalParameters[] epa, NumericElectricalParameters[] sep) = NewAreas();
        sep[1].At = 123456.789;
        var flags = new AreaRewriteFlags();
        flags.At[0] = true;

        CircuitAreaRewriter.Rewrite(epa, sep, flags);

        Assert.Equal("123456.78", epa[1].At);
    }

    [Fact]
    public void Rewrite_配列が3要素未満なら例外を投げる()
    {
        var epa = new[] { new ElectricalParameters(), new ElectricalParameters() };
        var sep = new[] { new NumericElectricalParameters(), new NumericElectricalParameters() };

        Assert.Throws<ArgumentException>(() =>
            CircuitAreaRewriter.Rewrite(epa, sep, new AreaRewriteFlags()));
    }
}
