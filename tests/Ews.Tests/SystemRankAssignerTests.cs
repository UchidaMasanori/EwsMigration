using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="SystemRankAssigner"/>(C 原典 Main_Rank_Set)の単体テスト。
/// </summary>
public sealed class SystemRankAssignerTests
{
    /// <summary>1 要素 = 主回路データ 1 件を組み立てるヘルパ。</summary>
    private static MainCircuitResult Rec(
        string datano = "000",
        char ksyubetu = '1',
        char narakbn = '1',
        string yoyaku = "",
        string gyocd = "",
        char kairobun = ' ',
        string kaisono = "000",
        char kpaph = '1')
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                SystemKind = ksyubetu,
                SortKind = narakbn,
                ReservedWord = yoyaku,
                LineTypeCode = gyocd,
                CircuitClass = kairobun,
                HierarchyNumber = kaisono,
                CircuitPhaseCount = kpaph,
            },
        };
        return r;
    }

    [Fact]
    public void 系統外SEPは上流並列追番001と系統種別0にする()
    {
        var m = Rec(ksyubetu: '2', yoyaku: "SEP");
        var mains = new[] { m };

        SystemRankAssigner.Assign(mains);

        Assert.Equal("001", m.Data.UpperParallelNumber);
        Assert.Equal("000", m.Data.HierarchyNumber);
        Assert.Equal('0', m.Data.SystemKind);
    }

    [Fact]
    public void 系統外の一般行は座標を000へ整える()
    {
        var m = Rec(ksyubetu: '2', yoyaku: "M");
        var mains = new[] { m };

        SystemRankAssigner.Assign(mains);

        Assert.Equal("000", m.Data.IncomingNumber);
        Assert.Equal("000", m.Data.SeriesNumber);
        Assert.Equal("000", m.Data.ParallelNumber);
        Assert.Equal("000", m.Data.UpperParallelNumber);
    }

    [Fact]
    public void P行は入線番号を採番しランクをクリアする()
    {
        var p = Rec(datano: "001", yoyaku: "P", gyocd: "P", kaisono: "000");
        var mains = new[] { p };

        SystemRankAssigner.Assign(mains);

        Assert.Equal("001", p.Data.IncomingNumber);   // nsen_no は 1 に加算
        Assert.Equal("000", p.Data.SeriesNumber);
        Assert.Equal("000", p.Data.ParallelNumber);
        Assert.Equal("000", p.Data.GroupParentSequenceNumber);
    }

    [Fact]
    public void 行種グループ番号は並び替え区分3と4で加算される()
    {
        var p = Rec(datano: "001", yoyaku: "P", gyocd: "P");
        var a = Rec(datano: "002", narakbn: '3', yoyaku: "M", kaisono: "001");
        var b = Rec(datano: "003", narakbn: '4', yoyaku: "M", kaisono: "001");
        var c = Rec(datano: "004", narakbn: '1', yoyaku: "M", kaisono: "001");
        var mains = new[] { p, a, b, c };

        SystemRankAssigner.Assign(mains);

        Assert.Equal("000", p.Data.LineTypeGroupNumber);
        Assert.Equal("001", a.Data.LineTypeGroupNumber);
        Assert.Equal("002", b.Data.LineTypeGroupNumber);
        Assert.Equal("002", c.Data.LineTypeGroupNumber);   // narakbn 1 は加算しない
    }

    [Fact]
    public void 同一階層の直列追番は前要素から連番する()
    {
        // P → M(階層001) → M(階層001) : 2 つめの M は直列追番が加算される
        var p = Rec(datano: "001", yoyaku: "P", gyocd: "P");
        var m1 = Rec(datano: "002", yoyaku: "M", kaisono: "001");
        var m2 = Rec(datano: "003", yoyaku: "M", kaisono: "001");
        var mains = new[] { p, m1, m2 };

        SystemRankAssigner.Assign(mains);

        Assert.Equal("001", m1.Data.SeriesNumber);   // P の直後は 001
        Assert.Equal("002", m2.Data.SeriesNumber);   // 同一階層で連番
    }

    [Fact]
    public void 親データ追番は直前要素の追番を継承する()
    {
        var p = Rec(datano: "001", yoyaku: "P", gyocd: "P");
        var m1 = Rec(datano: "002", yoyaku: "M", kaisono: "001");
        var m2 = Rec(datano: "003", yoyaku: "M", kaisono: "001");
        var mains = new[] { p, m1, m2 };

        SystemRankAssigner.Assign(mains);

        Assert.Equal("002", m2.Data.ParentSequenceNumber);   // maina[i-1].datano
    }

    [Fact]
    public void 機器選定指示フラグ2は並列追番をランク配列で採番する()
    {
        // narakbn '2' の要素は rank[階層-1] を進めて並列追番とする
        var p = Rec(datano: "001", yoyaku: "P", gyocd: "P");
        var s1 = Rec(datano: "002", narakbn: '2', yoyaku: "M", kaisono: "001");
        var s2 = Rec(datano: "003", narakbn: '2', yoyaku: "M", kaisono: "001");
        var mains = new[] { p, s1, s2 };

        SystemRankAssigner.Assign(mains);

        Assert.Equal("001", s1.Data.ParallelNumber);
        Assert.Equal("002", s2.Data.ParallelNumber);
        Assert.Equal("001", s1.Data.SeriesNumber);   // 指示フラグ時の直列追番は常に 001
    }

    [Fact]
    public void 上流並列追番は上位ランクの並列追番を参照する()
    {
        // 階層001 の指示要素 → 階層002 の指示要素 : 002 は 001 の並列追番を上流とする
        var p = Rec(datano: "001", yoyaku: "P", gyocd: "P");
        var lv1 = Rec(datano: "002", narakbn: '2', yoyaku: "M", kaisono: "001");
        var lv2 = Rec(datano: "003", narakbn: '2', yoyaku: "M", kaisono: "002");
        var mains = new[] { p, lv1, lv2 };

        SystemRankAssigner.Assign(mains);

        Assert.Equal("001", lv1.Data.ParallelNumber);
        Assert.Equal("001", lv2.Data.UpperParallelNumber);   // lv1 の並列追番
    }

    [Fact]
    public void グループ並列追番は同一グループ親の指示要素数で採番する()
    {
        // 同一グループ親(goyano)を持つ narakbn 2 要素が 2 つあれば 2 つめは 002
        var p = Rec(datano: "001", yoyaku: "P", gyocd: "P");
        var parent = Rec(datano: "002", narakbn: '2', yoyaku: "M", kaisono: "001");
        var g1 = Rec(datano: "003", narakbn: '2', yoyaku: "M", kaisono: "002");
        var g2 = Rec(datano: "004", narakbn: '2', yoyaku: "M", kaisono: "002");
        var mains = new[] { p, parent, g1, g2 };

        SystemRankAssigner.Assign(mains);

        Assert.Equal("001", g1.Data.GroupParallelNumber);
        Assert.Equal("002", g2.Data.GroupParallelNumber);
    }

    [Fact]
    public void 生成回路番号が採番されワークエリアがクリアされる()
    {
        var p = Rec(datano: "001", yoyaku: "P", gyocd: "P", kairobun: 'M');
        p.Work.SetCurrent = 12.3;
        p.Work.AccumulationSlots[0].A = 5.0;

        var mains = new[] { p };
        SystemRankAssigner.Assign(mains);

        Assert.Equal(' ', p.Work.EquipmentSelectionKind);
        Assert.Equal(0.0, p.Work.SetCurrent);
        Assert.Equal(0.0, p.Work.AccumulationSlots[0].A);
        Assert.Equal(3, p.Data.CircuitNumber.Length);   // 3 桁で採番される
    }
}
