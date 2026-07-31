using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 末端区分セット(<see cref="TerminalKindSetter"/>)の移植検証。
/// 【C原典】Fyss30_MattanKubun_Set(toku/sekkei/src/Fyss30.c)。
/// </summary>
public sealed class TerminalKindSetterTests
{
    private static MainCircuitResult Row(
        string datano,
        string oyatno = "000",
        char ksyubetu = '1',
        string yoyaku = "",
        string gyocd = "",
        string gyoglno = "000",
        string kaisono = "000",
        string heino = "000")
    {
        return new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                SystemKind = ksyubetu,
                ParentSequenceNumber = oyatno,
                ReservedWord = yoyaku,
                LineTypeCode = gyocd,
                LineTypeGroupNumber = gyoglno,
                HierarchyNumber = kaisono,
                ParallelNumber = heino,
            },
        };
    }

    [Fact]
    public void 親を持たないP系統機器は末端になる()
    {
        // 001(親) ← 002(子, oyatno=001)。002 は誰の親でもないので末端。
        MainCircuitResult parent = Row("001");
        MainCircuitResult child = Row("002", oyatno: "001");

        TerminalKindSetter.SetTerminalKind([parent, child]);

        Assert.Equal(' ', parent.Data.TerminalKind);
        Assert.Equal('1', child.Data.TerminalKind);
    }

    [Fact]
    public void P系統以外は末端にならない()
    {
        MainCircuitResult sp = Row("001", ksyubetu: '2');

        TerminalKindSetter.SetTerminalKind([sp]);

        Assert.Equal(' ', sp.Data.TerminalKind);
    }

    [Fact]
    public void 末端区分は毎回クリアされてから再計算される()
    {
        MainCircuitResult parent = Row("001");
        MainCircuitResult child = Row("002", oyatno: "001");
        parent.Data.TerminalKind = '1'; // 事前に誤った値をセット。

        TerminalKindSetter.SetTerminalKind([parent, child]);

        Assert.Equal(' ', parent.Data.TerminalKind);
        Assert.Equal('1', child.Data.TerminalKind);
    }

    [Fact]
    public void 単独SCの末端は直前直列機器へ付け直される()
    {
        // 001 MCB(親) → 002 MC(oyatno=001) → 003 SC(oyatno=002, 直前=MC)。
        // 003 SC は末端だが直前の 002 へ付け直される(MGSD/MCSD 無し・階層/並列一致)。
        MainCircuitResult mcb = Row("001", yoyaku: "MCB", kaisono: "001", heino: "001");
        MainCircuitResult mc = Row("002", oyatno: "001", yoyaku: "MC", kaisono: "002", heino: "001");
        MainCircuitResult sc = Row("003", oyatno: "002", yoyaku: "SC", kaisono: "002", heino: "001");

        TerminalKindSetter.SetTerminalKind([mcb, mc, sc]);

        Assert.Equal(' ', sc.Data.TerminalKind);
        Assert.Equal('1', mc.Data.TerminalKind);
    }

    [Fact]
    public void MGSDが同一行種グループにあるSCは付け直されない()
    {
        // 直前が MC で、同一行種グループに MGSD があるため付け直さない(SC を末端のまま)。
        MainCircuitResult mc = Row("001", yoyaku: "MC", gyocd: "MCB", gyoglno: "001", kaisono: "001", heino: "001");
        MainCircuitResult sc = Row("002", oyatno: "001", yoyaku: "SC", gyocd: "MCB", gyoglno: "001", kaisono: "001", heino: "001");
        MainCircuitResult mgsd = Row("003", ksyubetu: '1', yoyaku: "MGSD", gyocd: "MCB", gyoglno: "001", kaisono: "001", heino: "002");
        // mgsd を誰かの親にして末端判定から外す。
        mgsd.Data.ParentSequenceNumber = "000";
        MainCircuitResult childOfMgsd = Row("004", oyatno: "003");

        TerminalKindSetter.SetTerminalKind([mc, sc, mgsd, childOfMgsd]);

        Assert.Equal('1', sc.Data.TerminalKind);
    }

    [Fact]
    public void 直前が非MCのSC末端も直列一致なら付け直される()
    {
        // 直前が非 MC(THR)でも、階層/並列一致なら付け直す。
        MainCircuitResult thr = Row("001", yoyaku: "THR", kaisono: "005", heino: "003");
        MainCircuitResult sc = Row("002", oyatno: "001", yoyaku: "SC", kaisono: "005", heino: "003");

        TerminalKindSetter.SetTerminalKind([thr, sc]);

        Assert.Equal(' ', sc.Data.TerminalKind);
        Assert.Equal('1', thr.Data.TerminalKind);
    }

    [Fact]
    public void fpaln1が0KWのSCは付け直し対象外()
    {
        MainCircuitResult mc = Row("001", yoyaku: "MC", kaisono: "002", heino: "001");
        MainCircuitResult sc = Row("002", oyatno: "001", yoyaku: "SC", kaisono: "002", heino: "001");
        sc.Data.AttachedParameter.LoadName[1] = "0KW";

        TerminalKindSetter.SetTerminalKind([mc, sc]);

        Assert.Equal('1', sc.Data.TerminalKind);
        Assert.Equal(' ', mc.Data.TerminalKind);
    }

    [Fact]
    public void 付け直し時に直前の負荷未設定なら負荷パラメータを移送する()
    {
        MainCircuitResult mc = Row("001", yoyaku: "MC", kaisono: "002", heino: "001");
        MainCircuitResult sc = Row("002", oyatno: "001", yoyaku: "SC", kaisono: "002", heino: "001");
        sc.Data.AttachedParameter.LoadKind = "LW";
        sc.Data.AttachedParameter.LoadCapacity = "0037000";
        sc.Data.AttachedParameter.LoadUnitKind = 'W';
        sc.Data.AttachedParameter.LoadName[0] = "MOTOR";
        sc.Data.AttachedParameter.LoadName[1] = "37KW";

        TerminalKindSetter.SetTerminalKind([mc, sc]);

        Assert.Equal('1', mc.Data.TerminalKind);
        Assert.Equal("LW", mc.Data.AttachedParameter.LoadKind);
        Assert.Equal("0037000", mc.Data.AttachedParameter.LoadCapacity);
        Assert.Equal('W', mc.Data.AttachedParameter.LoadUnitKind);
        Assert.Equal("MOTOR", mc.Data.AttachedParameter.LoadName[0]);
        Assert.Equal("37KW", mc.Data.AttachedParameter.LoadName[1]);
    }

    [Fact]
    public void 空リストでも例外にならない()
    {
        TerminalKindSetter.SetTerminalKind([]);
    }
}
