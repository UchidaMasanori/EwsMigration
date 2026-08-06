using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlEquipmentComparer"/>(【C原典】Fyss1k.c の sortcmp2)の単体テスト。
/// </summary>
public sealed class ControlEquipmentComparerTests
{
    private static ControlEquipmentEntry E(string yoyaku, short nkosu = 0, short gkosu = 0)
        => new() { ReservedWord = yoyaku, InternalCount = nkosu, ExternalCount = gkosu };

    [Fact]
    public void 予約語昇順で比較する()
    {
        Assert.Equal(-1, ControlEquipmentComparer.Instance.Compare(E("MC"), E("MG")));
        Assert.Equal(1, ControlEquipmentComparer.Instance.Compare(E("MG"), E("MC")));
    }

    [Fact]
    public void 予約語が接頭辞なら短い方が先()
    {
        // memcmp 16バイト: "MC\0..." < "MCFR..."。'\0' < 'F'。
        Assert.Equal(-1, ControlEquipmentComparer.Instance.Compare(E("MC"), E("MCFR")));
        Assert.Equal(1, ControlEquipmentComparer.Instance.Compare(E("MCFR"), E("MC")));
    }

    [Fact]
    public void 予約語同じなら内部機器個数昇順()
    {
        Assert.Equal(-1, ControlEquipmentComparer.Instance.Compare(E("MC", nkosu: 1), E("MC", nkosu: 3)));
        Assert.Equal(1, ControlEquipmentComparer.Instance.Compare(E("MC", nkosu: 3), E("MC", nkosu: 1)));
    }

    [Fact]
    public void 予約語と内部個数が同じなら外部機器個数昇順()
    {
        Assert.Equal(-1, ControlEquipmentComparer.Instance.Compare(E("MC", nkosu: 2, gkosu: 1), E("MC", nkosu: 2, gkosu: 5)));
        Assert.Equal(1, ControlEquipmentComparer.Instance.Compare(E("MC", nkosu: 2, gkosu: 5), E("MC", nkosu: 2, gkosu: 1)));
    }

    [Fact]
    public void 全て同じなら0()
    {
        Assert.Equal(0, ControlEquipmentComparer.Instance.Compare(E("MC", nkosu: 2, gkosu: 3), E("MC", nkosu: 2, gkosu: 3)));
    }

    [Fact]
    public void リストソートで予約語_内部_外部の順に整列する()
    {
        var list = new List<ControlEquipmentEntry>
        {
            E("MG", nkosu: 0, gkosu: 0),
            E("MC", nkosu: 2, gkosu: 1),
            E("MC", nkosu: 1, gkosu: 9),
            E("MC", nkosu: 2, gkosu: 0),
        };

        list.Sort(ControlEquipmentComparer.Instance);

        Assert.Equal(new[] { "MC", "MC", "MC", "MG" }, list.ConvertAll(e => e.ReservedWord).ToArray());
        // MC 群は nkosu 昇順 → 同 nkosu は gkosu 昇順。
        Assert.Equal(1, list[0].InternalCount);
        Assert.Equal(2, list[1].InternalCount);
        Assert.Equal(0, list[1].ExternalCount);
        Assert.Equal(2, list[2].InternalCount);
        Assert.Equal(1, list[2].ExternalCount);
    }

    [Fact]
    public void 空予約語同士は全個数比較へ進む()
    {
        Assert.Equal(-1, ControlEquipmentComparer.Instance.Compare(E("", nkosu: 0), E("", nkosu: 1)));
        Assert.Equal(0, ControlEquipmentComparer.Instance.Compare(E(""), E("")));
    }
}
