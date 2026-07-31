using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 末端回路行種先頭機器フラグセット(<see cref="LeadingEquipmentFlagSetter"/>)の移植検証。
/// 【C原典】Fyss34_MattanGyouSento_Set(toku/sekkei/src/Fyss34.c)。
/// </summary>
public sealed class LeadingEquipmentFlagSetterTests
{
    private static MainCircuitResult Row(
        string datano,
        char mattan = ' ',
        string yoyaku = "",
        char kiryoso = '1',
        string gyoglno = "000",
        string kaisono = "000",
        string chokuno = "000")
    {
        return new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                TerminalKind = mattan,
                ReservedWord = yoyaku,
                CircuitElement = kiryoso,
                LineTypeGroupNumber = gyoglno,
                HierarchyNumber = kaisono,
                SeriesNumber = chokuno,
            },
        };
    }

    [Fact]
    public void 末端が無ければフラグは全て空白()
    {
        MainCircuitResult a = Row("001", gyoglno: "001", kaisono: "001", chokuno: "001");
        MainCircuitResult b = Row("002", gyoglno: "001", kaisono: "002", chokuno: "001");

        LeadingEquipmentFlagSetter.SetLeadingEquipmentFlag([a, b]);

        Assert.Equal(' ', a.Work.LeadingEquipmentFlag);
        Assert.Equal(' ', b.Work.LeadingEquipmentFlag);
    }

    [Fact]
    public void 同一行種グループの最小ランク機器に先頭フラグを立てる()
    {
        // gyoglno=001 群。ランク: top=1×1000+1=1001, mid=2×1000+1=2001, end=末端。
        MainCircuitResult top = Row("001", gyoglno: "001", kaisono: "001", chokuno: "001");
        MainCircuitResult mid = Row("002", gyoglno: "001", kaisono: "002", chokuno: "001");
        MainCircuitResult end = Row("003", mattan: '1', gyoglno: "001", kaisono: "003", chokuno: "001");

        LeadingEquipmentFlagSetter.SetLeadingEquipmentFlag([top, mid, end]);

        Assert.Equal('1', top.Work.LeadingEquipmentFlag);
        Assert.Equal(' ', mid.Work.LeadingEquipmentFlag);
        Assert.Equal(' ', end.Work.LeadingEquipmentFlag);
    }

    [Fact]
    public void 最小ランクが複数あれば全てに先頭フラグを立てる()
    {
        // top1/top2 が同ランク(1001)で最小。
        MainCircuitResult top1 = Row("001", gyoglno: "001", kaisono: "001", chokuno: "001");
        MainCircuitResult top2 = Row("002", gyoglno: "001", kaisono: "001", chokuno: "001");
        MainCircuitResult end = Row("003", mattan: '1', gyoglno: "001", kaisono: "002", chokuno: "001");

        LeadingEquipmentFlagSetter.SetLeadingEquipmentFlag([top1, top2, end]);

        Assert.Equal('1', top1.Work.LeadingEquipmentFlag);
        Assert.Equal('1', top2.Work.LeadingEquipmentFlag);
        Assert.Equal(' ', end.Work.LeadingEquipmentFlag);
    }

    [Fact]
    public void 回路要素5も抽出対象となる()
    {
        // kiryoso='5'(主回路振り分け)も抽出対象。最小ランクは top。
        MainCircuitResult top = Row("001", kiryoso: '5', gyoglno: "001", kaisono: "001", chokuno: "001");
        MainCircuitResult end = Row("002", mattan: '1', kiryoso: '1', gyoglno: "001", kaisono: "002", chokuno: "001");

        LeadingEquipmentFlagSetter.SetLeadingEquipmentFlag([top, end]);

        Assert.Equal('1', top.Work.LeadingEquipmentFlag);
        Assert.Equal(' ', end.Work.LeadingEquipmentFlag);
    }

    [Fact]
    public void 回路要素が1と5以外は抽出されない()
    {
        // 末端自身が kiryoso='3'(計器) → 抽出対象外。同一群に 1/5 が無いのでフラグ無し。
        MainCircuitResult meter = Row("001", mattan: '1', kiryoso: '3', gyoglno: "001", kaisono: "001", chokuno: "001");

        LeadingEquipmentFlagSetter.SetLeadingEquipmentFlag([meter]);

        Assert.Equal(' ', meter.Work.LeadingEquipmentFlag);
    }

    [Fact]
    public void 計器WHのkiryoso3は末端でも対象外()
    {
        MainCircuitResult wh = Row("001", mattan: '1', yoyaku: "WH", kiryoso: '3', gyoglno: "001", kaisono: "001", chokuno: "001");
        // 同一群に主回路機器があるが、末端WH自身は 950405 でスキップされ抽出も行われない。
        MainCircuitResult other = Row("002", kiryoso: '1', gyoglno: "001", kaisono: "002", chokuno: "001");

        LeadingEquipmentFlagSetter.SetLeadingEquipmentFlag([wh, other]);

        Assert.Equal(' ', wh.Work.LeadingEquipmentFlag);
        Assert.Equal(' ', other.Work.LeadingEquipmentFlag);
    }

    [Fact]
    public void 計器CTのkiryoso2は末端でも対象外()
    {
        MainCircuitResult ct = Row("001", mattan: '1', yoyaku: "CT", kiryoso: '2', gyoglno: "001", kaisono: "001", chokuno: "001");

        LeadingEquipmentFlagSetter.SetLeadingEquipmentFlag([ct]);

        Assert.Equal(' ', ct.Work.LeadingEquipmentFlag);
    }

    [Fact]
    public void 行種グループが異なる機器は抽出されない()
    {
        // 末端(gyoglno=001)の群には自身のみ。gyoglno=002 の機器は別群で無関係。
        MainCircuitResult end = Row("001", mattan: '1', gyoglno: "001", kaisono: "005", chokuno: "001");
        MainCircuitResult otherGroup = Row("002", gyoglno: "002", kaisono: "001", chokuno: "001");

        LeadingEquipmentFlagSetter.SetLeadingEquipmentFlag([end, otherGroup]);

        Assert.Equal('1', end.Work.LeadingEquipmentFlag);
        Assert.Equal(' ', otherGroup.Work.LeadingEquipmentFlag);
    }

    [Fact]
    public void フラグは毎回クリアされてから再計算される()
    {
        MainCircuitResult top = Row("001", gyoglno: "001", kaisono: "001", chokuno: "001");
        MainCircuitResult end = Row("002", mattan: '1', gyoglno: "001", kaisono: "002", chokuno: "001");
        end.Work.LeadingEquipmentFlag = '1'; // 事前に誤った値。

        LeadingEquipmentFlagSetter.SetLeadingEquipmentFlag([top, end]);

        Assert.Equal('1', top.Work.LeadingEquipmentFlag);
        Assert.Equal(' ', end.Work.LeadingEquipmentFlag);
    }

    [Fact]
    public void 空リストでも例外にならない()
    {
        LeadingEquipmentFlagSetter.SetLeadingEquipmentFlag([]);
    }
}
