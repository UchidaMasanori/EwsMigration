using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlEquipmentIdentityAssigner"/>(【C原典】Fyss1k.c の SetCkikiDkkno, 改訂&lt;26&gt;)の単体テスト。
/// </summary>
public sealed class ControlEquipmentIdentityAssignerTests
{
    private static EquipmentTableEntry Kiki(
        string yoyaku,
        string ysno,
        short rank = 0,
        string? dtype1 = null)
    {
        var e = new EquipmentTableEntry
        {
            ReservedWord = yoyaku,
            ReservedWordNumber = ysno,
            Rank = rank,
        };
        if (dtype1 is not null)
        {
            e.DType[1] = dtype1;
        }

        return e;
    }

    private static ControlSpecEntry Sgs(short cnameno, string pcstrg)
        => new() { SpecNameSequence = cnameno, RawText = pcstrg };

    [Fact]
    public void 件数0なら何もしない()
    {
        var list = new List<EquipmentTableEntry>();
        ControlEquipmentIdentityAssigner.AssignIdentityNumbers(list, 1, new List<ControlSpecEntry>());
        Assert.Empty(list);
    }

    [Fact]
    public void ysnoが00以下は対象外_E_No設定されない()
    {
        // ysno == "00" は strcmp(...,"00") > 0 が偽 → 対象外。
        var e1 = Kiki("MC", "00");
        var e2 = Kiki("MC", "00");
        var list = new List<EquipmentTableEntry> { e1, e2 };

        ControlEquipmentIdentityAssigner.AssignIdentityNumbers(list, 5, new List<ControlSpecEntry>());

        Assert.Equal(0, e1.EquipmentIdentityNumber);
        Assert.Equal(0, e2.EquipmentIdentityNumber);
    }

    [Fact]
    public void 同一予約語グループに同一番号を採番する()
    {
        // 予約語+予約語番号 "MC01"/"MC01" が一致 → 同一機器。開始 dkkno=5。
        var e1 = Kiki("MC", "01");
        var e2 = Kiki("MC", "01");
        var list = new List<EquipmentTableEntry> { e1, e2 };

        ControlEquipmentIdentityAssigner.AssignIdentityNumbers(list, 5, new List<ControlSpecEntry>());

        Assert.Equal(5, e1.EquipmentIdentityNumber);
        Assert.Equal(5, e2.EquipmentIdentityNumber);
    }

    [Fact]
    public void 予約語が異なる単独機器は採番されない()
    {
        // "MC01" と "MG01" は異なる → どちらも単独 → RRY6A4K でもないので採番なし。
        var e1 = Kiki("MC", "01");
        var e2 = Kiki("MG", "01");
        var list = new List<EquipmentTableEntry> { e1, e2 };

        ControlEquipmentIdentityAssigner.AssignIdentityNumbers(list, 5, new List<ControlSpecEntry>());

        Assert.Equal(0, e1.EquipmentIdentityNumber);
        Assert.Equal(0, e2.EquipmentIdentityNumber);
    }

    [Fact]
    public void 複数グループにそれぞれ連番を採番する()
    {
        // グループ "MC01"(2件) と "MG02"(2件)。開始 dkkno=10 → MC群=10, MG群=11。
        var mc1 = Kiki("MC", "01");
        var mc2 = Kiki("MC", "01");
        var mg1 = Kiki("MG", "02");
        var mg2 = Kiki("MG", "02");
        var list = new List<EquipmentTableEntry> { mc1, mg1, mc2, mg2 };

        ControlEquipmentIdentityAssigner.AssignIdentityNumbers(list, 10, new List<ControlSpecEntry>());

        Assert.Equal(10, mc1.EquipmentIdentityNumber);
        Assert.Equal(10, mc2.EquipmentIdentityNumber);
        Assert.Equal(11, mg1.EquipmentIdentityNumber);
        Assert.Equal(11, mg2.EquipmentIdentityNumber);
    }

    [Fact]
    public void 全件が同一予約語なら全件同一番号()
    {
        // 末尾番兵(空文字)により最終要素まで正しく採番される(off-by-one 再現)。
        var e1 = Kiki("RRY", "03");
        var e2 = Kiki("RRY", "03");
        var e3 = Kiki("RRY", "03");
        var list = new List<EquipmentTableEntry> { e1, e2, e3 };

        ControlEquipmentIdentityAssigner.AssignIdentityNumbers(list, 7, new List<ControlSpecEntry>());

        Assert.Equal(7, e1.EquipmentIdentityNumber);
        Assert.Equal(7, e2.EquipmentIdentityNumber);
        Assert.Equal(7, e3.EquipmentIdentityNumber);
    }

    [Fact]
    public void RRY6A4K制御回路側で接点2個以上なら採番する()
    {
        // RRY 6A4K 単独機器。Rank=3 と一致する Sgs があり G(RRY) でない → 制御回路側。
        // ysno="05" → work="RRY5-"。Pcstrg に "RRY5-" を含む Sgs が 2 件 → 採番。
        var rry = Kiki("RRY", "05", rank: 3, dtype1: "6A4K");
        var list = new List<EquipmentTableEntry> { rry };
        var specs = new List<ControlSpecEntry>
        {
            Sgs(3, "X(RRY5-1)"),
            Sgs(9, "Y(RRY5-2)"),
        };

        ControlEquipmentIdentityAssigner.AssignIdentityNumbers(list, 20, specs);

        Assert.Equal(20, rry.EquipmentIdentityNumber);
    }

    [Fact]
    public void RRY6A4K制御回路側で接点1個なら採番しない()
    {
        var rry = Kiki("RRY", "05", rank: 3, dtype1: "6A4K");
        var list = new List<EquipmentTableEntry> { rry };
        var specs = new List<ControlSpecEntry>
        {
            Sgs(3, "X(RRY5-1)"),
        };

        ControlEquipmentIdentityAssigner.AssignIdentityNumbers(list, 20, specs);

        Assert.Equal(0, rry.EquipmentIdentityNumber);
    }

    [Fact]
    public void RRY6A4K主回路側_GRRYなら採番しない()
    {
        // Rank 一致の最初の Sgs が "G(RRY)" → 主回路側 → 何もしない(接点カウントもしない)。
        var rry = Kiki("RRY", "05", rank: 3, dtype1: "6A4K");
        var list = new List<EquipmentTableEntry> { rry };
        var specs = new List<ControlSpecEntry>
        {
            Sgs(3, "G(RRY)"),
            Sgs(3, "X(RRY5-1)"),
            Sgs(3, "Y(RRY5-2)"),
        };

        ControlEquipmentIdentityAssigner.AssignIdentityNumbers(list, 20, specs);

        Assert.Equal(0, rry.EquipmentIdentityNumber);
    }

    [Fact]
    public void RRYでもDType1が6A4Kでなければ特例対象外()
    {
        var rry = Kiki("RRY", "05", rank: 3, dtype1: "6A2K");
        var list = new List<EquipmentTableEntry> { rry };
        var specs = new List<ControlSpecEntry>
        {
            Sgs(3, "X(RRY5-1)"),
            Sgs(3, "Y(RRY5-2)"),
        };

        ControlEquipmentIdentityAssigner.AssignIdentityNumbers(list, 20, specs);

        Assert.Equal(0, rry.EquipmentIdentityNumber);
    }
}
