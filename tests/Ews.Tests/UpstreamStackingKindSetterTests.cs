using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 上流積み上げ区分セット(<see cref="UpstreamStackingKindSetter"/>)の移植検証。
/// 【C原典】Fyss32_SC_NT_Tumiage_Set(toku/sekkei/src/Fyss32.c)。
/// </summary>
public sealed class UpstreamStackingKindSetterTests
{
    private static MainCircuitResult Row(
        string datano,
        string oyatno = "000",
        char ksyubetu = '1',
        string yoyaku = "",
        string chokuno = "000",
        string gyocd = "",
        string gyoglno = "000",
        char mattan = '1',
        char jagekbn = ' ')
    {
        return new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                SystemKind = ksyubetu,
                ParentSequenceNumber = oyatno,
                ReservedWord = yoyaku,
                SeriesNumber = chokuno,
                LineTypeCode = gyocd,
                LineTypeGroupNumber = gyoglno,
                TerminalKind = mattan,
                StackKind = jagekbn,
            },
        };
    }

    [Fact]
    public void 積み上げ区分はKが1へ再セットされ他は空白クリアされる()
    {
        MainCircuitResult k = Row("001", jagekbn: 'K');   // 交互運転 → '1'
        MainCircuitResult x = Row("002", jagekbn: 'X');   // 他 → ' '

        UpstreamStackingKindSetter.SetUpstreamStackingKind([k, x]);

        Assert.Equal('1', k.Data.StackKind);
        Assert.Equal(' ', x.Data.StackKind);
    }

    [Fact]
    public void 直列追番1のNTは自身へ積み上げ区分をセットする()
    {
        MainCircuitResult nt = Row("001", yoyaku: "NT", chokuno: "001");

        UpstreamStackingKindSetter.SetUpstreamStackingKind([nt]);

        Assert.Equal('1', nt.Data.StackKind);
    }

    [Fact]
    public void 直列追番が1でないNTは直列先頭へ積み上げ区分をセットする()
    {
        MainCircuitResult top = Row("001", chokuno: "001");
        MainCircuitResult mid = Row("002", chokuno: "002");
        MainCircuitResult nt = Row("003", yoyaku: "NT", chokuno: "003");

        UpstreamStackingKindSetter.SetUpstreamStackingKind([top, mid, nt]);

        Assert.Equal('1', top.Data.StackKind);
        Assert.Equal(' ', mid.Data.StackKind);
        Assert.Equal(' ', nt.Data.StackKind);
    }

    [Fact]
    public void 直列追番1のSCは直前MCで系列にMGSD無なら自身へセットする()
    {
        MainCircuitResult mc = Row("001", yoyaku: "MC", gyocd: "X", gyoglno: "001");
        MainCircuitResult sc = Row("002", oyatno: "001", yoyaku: "SC", chokuno: "001", gyocd: "X", gyoglno: "001", mattan: '1');

        UpstreamStackingKindSetter.SetUpstreamStackingKind([mc, sc]);

        Assert.Equal('1', sc.Data.StackKind);
        Assert.Equal(' ', mc.Data.StackKind);
    }

    [Fact]
    public void 直列追番1のSCは直前MCで系列にMGSD有なら直前へセットする()
    {
        MainCircuitResult mc = Row("001", yoyaku: "MC", gyocd: "X", gyoglno: "001");
        MainCircuitResult sc = Row("002", oyatno: "001", yoyaku: "SC", chokuno: "001", gyocd: "X", gyoglno: "001", mattan: '1');
        MainCircuitResult mgsd = Row("003", yoyaku: "MGSD", gyocd: "X", gyoglno: "001");

        UpstreamStackingKindSetter.SetUpstreamStackingKind([mc, sc, mgsd]);

        Assert.Equal('1', mc.Data.StackKind);
        Assert.Equal(' ', sc.Data.StackKind);
    }

    [Fact]
    public void 直列追番1のSCは非MC直前で後方に有効負荷があれば自身へセットする()
    {
        MainCircuitResult thr = Row("001", yoyaku: "THR");
        MainCircuitResult sc = Row("002", oyatno: "001", yoyaku: "SC", chokuno: "001", gyocd: "X", gyoglno: "001", mattan: '1');
        MainCircuitResult load = Row("003", gyocd: "X", gyoglno: "001");
        load.Data.AttachedParameter.LoadCapacity = "0037000";

        UpstreamStackingKindSetter.SetUpstreamStackingKind([thr, sc, load]);

        Assert.Equal('1', sc.Data.StackKind);
    }

    [Fact]
    public void 直列追番1のSCは非MC直前で後方に有効負荷が無ければセットしない()
    {
        MainCircuitResult thr = Row("001", yoyaku: "THR");
        MainCircuitResult sc = Row("002", oyatno: "001", yoyaku: "SC", chokuno: "001", gyocd: "X", gyoglno: "001", mattan: '1');
        MainCircuitResult load = Row("003", gyocd: "X", gyoglno: "001");
        load.Data.AttachedParameter.LoadCapacity = "0000000";

        UpstreamStackingKindSetter.SetUpstreamStackingKind([thr, sc, load]);

        Assert.Equal(' ', sc.Data.StackKind);
    }

    [Fact]
    public void 末端でないSCはfpaln1が0KWでなければ積み上げ区分をセットする()
    {
        MainCircuitResult sc = Row("001", yoyaku: "SC", chokuno: "001", mattan: ' ');

        UpstreamStackingKindSetter.SetUpstreamStackingKind([sc]);

        Assert.Equal('1', sc.Data.StackKind);
    }

    [Fact]
    public void 末端でないSCでもfpaln1が0KWならセットしない()
    {
        MainCircuitResult sc = Row("001", yoyaku: "SC", chokuno: "001", mattan: ' ');
        sc.Data.AttachedParameter.LoadName[1] = "0KW";

        UpstreamStackingKindSetter.SetUpstreamStackingKind([sc]);

        Assert.Equal(' ', sc.Data.StackKind);
    }

    [Fact]
    public void P系統以外は対象外()
    {
        MainCircuitResult sc = Row("001", ksyubetu: '2', yoyaku: "SC", chokuno: "001", mattan: ' ');

        UpstreamStackingKindSetter.SetUpstreamStackingKind([sc]);

        Assert.Equal(' ', sc.Data.StackKind);
    }

    [Fact]
    public void 空リストでも例外にならない()
    {
        UpstreamStackingKindSetter.SetUpstreamStackingKind([]);
    }
}
