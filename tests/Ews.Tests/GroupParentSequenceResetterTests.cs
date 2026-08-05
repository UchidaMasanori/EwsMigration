using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="GroupParentSequenceResetter"/>(【C原典】Fyss14.c Main_Rank_Update)の単体テスト。
/// 系統内で有効なグループ並列追番を持つ階層 001 要素が無い場合に、
/// 階層 001 要素の goyano を "000" に戻す挙動を検証する。
/// </summary>
public sealed class GroupParentSequenceResetterTests
{
    private static MainCircuitResult Rec(
        string kno,
        string kaisono,
        string heino = "000",
        string glheino = "000",
        char ksyubetu = '1',
        string goyano = "005")
    {
        var r = new MainCircuitResult();
        MainCircuitData d = r.Data;
        d.SystemNumber = kno;
        d.HierarchyNumber = kaisono;
        d.ParallelNumber = heino;
        d.GroupParallelNumber = glheino;
        d.SystemKind = ksyubetu;
        d.GroupParentSequenceNumber = goyano;
        return r;
    }

    [Fact]
    public void Reset_有効なグループ並列追番が無ければ階層001のgoyanoを000に戻す()
    {
        var head = Rec("001", "000");
        var lv1a = Rec("001", "001", goyano: "005");
        var lv1b = Rec("001", "001", goyano: "007");
        var mains = new List<MainCircuitResult> { head, lv1a, lv1b };

        GroupParentSequenceResetter.Reset(mains);

        Assert.Equal("000", lv1a.Data.GroupParentSequenceNumber);
        Assert.Equal("000", lv1b.Data.GroupParentSequenceNumber);
    }

    [Fact]
    public void Reset_有効なグループ並列追番があればgoyanoを変えない()
    {
        var head = Rec("001", "000");
        // heino!="001" かつ glheino!="000" の要素が存在する → リセットしない。
        var target = Rec("001", "001", heino: "002", glheino: "003", goyano: "005");
        var mains = new List<MainCircuitResult> { head, target };

        GroupParentSequenceResetter.Reset(mains);

        Assert.Equal("005", target.Data.GroupParentSequenceNumber);
    }

    [Fact]
    public void Reset_系統種別が1以外は対象外()
    {
        var head = Rec("001", "000");
        var target = Rec("001", "001", ksyubetu: '0', goyano: "005");
        var mains = new List<MainCircuitResult> { head, target };

        GroupParentSequenceResetter.Reset(mains);

        Assert.Equal("005", target.Data.GroupParentSequenceNumber);
    }

    [Fact]
    public void Reset_カウント対象の並列追番001は有効扱いにならない()
    {
        var head = Rec("001", "000");
        // glheino!="000" でも heino=="001" ならカウントされず、リセットされる。
        var lv1 = Rec("001", "001", heino: "001", glheino: "003", goyano: "005");
        var mains = new List<MainCircuitResult> { head, lv1 };

        GroupParentSequenceResetter.Reset(mains);

        Assert.Equal("000", lv1.Data.GroupParentSequenceNumber);
    }

    [Fact]
    public void Reset_異なる系統は独立に判定する()
    {
        var head1 = Rec("001", "000");
        var sys1 = Rec("001", "001", heino: "002", glheino: "003", goyano: "005"); // 有効あり→保持
        var head2 = Rec("002", "000");
        var sys2 = Rec("002", "001", goyano: "008");                                // 有効なし→000
        var mains = new List<MainCircuitResult> { head1, sys1, head2, sys2 };

        GroupParentSequenceResetter.Reset(mains);

        Assert.Equal("005", sys1.Data.GroupParentSequenceNumber);
        Assert.Equal("000", sys2.Data.GroupParentSequenceNumber);
    }
}
