using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 相・線式・極数からエレメント数/極数を決定するテーブル照合ユーティリティの検証。
/// 【C原典】toku/sekkei/src/Fyss14.c(Element_Gen / Pole_Gen)。
/// </summary>
public sealed class CircuitElementResolverTests
{
    [Theory]
    // 【C原典】Element_Gen の prmtable 各行(相,線式,極 → エレメント数)。
    [InlineData(3, 4, 4, 3)]
    [InlineData(3, 3, 3, 3)]
    [InlineData(1, 3, 3, 2)]
    [InlineData(1, 2, 2, 2)]
    [InlineData(1, 2, 1, 1)]
    public void ResolveElement_テーブル一致でエレメント数を返す(
        short ph, short wr, short p, short expected)
    {
        var prm = new MainCircuitParameter { Phase = ph, WireType = wr, Pole = p };

        short result = CircuitElementResolver.ResolveElement(prm);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, 0)]     // 終端値(該当なし)
    [InlineData(3, 4, 3)]     // 極数だけ不一致(表は p=4)
    [InlineData(2, 2, 2)]     // 相が不一致
    public void ResolveElement_テーブル不一致はマイナス1を返す(short ph, short wr, short p)
    {
        var prm = new MainCircuitParameter { Phase = ph, WireType = wr, Pole = p };

        short result = CircuitElementResolver.ResolveElement(prm);

        Assert.Equal((short)-1, result);
    }

    [Theory]
    // 【C原典】Pole_Gen の prmtable 各行(相,線式 → 極数)。
    [InlineData(3, 4, 4)]
    [InlineData(3, 3, 3)]
    [InlineData(1, 3, 3)]
    [InlineData(1, 2, 2)]
    public void ResolvePole_テーブル一致で極数を設定し0を返す(
        short ph, short wr, short expectedPole)
    {
        var prm = new MainCircuitParameter { Phase = ph, WireType = wr, Pole = 0 };

        int rc = CircuitElementResolver.ResolvePole(prm);

        Assert.Equal(0, rc);
        Assert.Equal(expectedPole, prm.Pole);
    }

    [Fact]
    public void ResolvePole_テーブル不一致はマイナス1を返し極数を変更しない()
    {
        // 【C原典】終端({0,0})まで一致せず → -1、pprmp->p は不変。
        var prm = new MainCircuitParameter { Phase = 2, WireType = 2, Pole = 99 };

        int rc = CircuitElementResolver.ResolvePole(prm);

        Assert.Equal(-1, rc);
        Assert.Equal((short)99, prm.Pole); // 不変
    }
}
