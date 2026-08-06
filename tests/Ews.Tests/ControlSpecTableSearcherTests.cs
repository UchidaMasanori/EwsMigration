using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlSpecTableSearcher"/>(【C原典】Fyss1k.c の checkSameSgtkk)の単体テスト。
/// </summary>
public sealed class ControlSpecTableSearcherTests
{
    private static ControlSpecEntry Spec(short cnameno, string pcstrg)
    {
        return new ControlSpecEntry
        {
            SpecNameSequence = cnameno,
            RawText = pcstrg,
        };
    }

    [Fact]
    public void 他エントリの制御対象機器に一致すればtrue()
    {
        var specs = new List<ControlSpecEntry>
        {
            Spec(1, "OL:MC"),
            Spec(2, "MG:INV"),
        };

        Assert.True(ControlSpecTableSearcher.HasSameControlTargetEquipment("MG", 1, specs));
    }

    [Fact]
    public void コロン前がカンマ区切りでも各機器を判定する()
    {
        var specs = new List<ControlSpecEntry>
        {
            Spec(2, "MC,MG,COS:xxx"),
        };

        Assert.True(ControlSpecTableSearcher.HasSameControlTargetEquipment("MG", 1, specs));
    }

    [Fact]
    public void 自身のエントリはスキップする()
    {
        var specs = new List<ControlSpecEntry>
        {
            Spec(1, "MG:INV"),
        };

        // cnameno=1 は自身なので除外され、他に一致なし。
        Assert.False(ControlSpecTableSearcher.HasSameControlTargetEquipment("MG", 1, specs));
    }

    [Fact]
    public void コロンが無いエントリは対象外()
    {
        var specs = new List<ControlSpecEntry>
        {
            Spec(2, "MGINV"),
        };

        Assert.False(ControlSpecTableSearcher.HasSameControlTargetEquipment("MG", 1, specs));
    }

    [Fact]
    public void コロン後の機器は判定しない()
    {
        var specs = new List<ControlSpecEntry>
        {
            Spec(2, "OL:MG"),
        };

        // MG はコロン後(制御対象機器でない)なので不一致。
        Assert.False(ControlSpecTableSearcher.HasSameControlTargetEquipment("MG", 1, specs));
    }

    [Fact]
    public void 部分一致では該当しない()
    {
        var specs = new List<ControlSpecEntry>
        {
            Spec(2, "MGFR:xxx"),
        };

        // strcmp 完全一致のため "MG" は "MGFR" に一致しない。
        Assert.False(ControlSpecTableSearcher.HasSameControlTargetEquipment("MG", 1, specs));
    }

    [Fact]
    public void 空テーブルはfalse()
    {
        Assert.False(ControlSpecTableSearcher.HasSameControlTargetEquipment("MG", 1, new List<ControlSpecEntry>()));
    }
}
