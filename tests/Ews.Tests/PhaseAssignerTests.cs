using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 使用相決定(<see cref="PhaseAssigner"/>)の純粋ヘルパー群の移植検証。
/// 【C原典】Fyss3D.c の Siyousou2to1／sort3d／PropSetSou*／PropSetSouMC*／PropSetPrmFor2P200v／
/// PropCount100Vkiki／PropMcChildVolt(toku/sekkei/src/Fyss3D.c)。
/// </summary>
public sealed class PhaseAssignerTests
{
    private static MainCircuitResult Row(
        string datano = "000",
        string yoyaku = "",
        string oyatno = "000",
        string gyocd = "",
        string heino = "000",
        string nyuseno = "000",
        string joheino = "000",
        string kaisono = "000",
        string chokuno = "000",
        string siyouso = "    ",
        string kpav0 = "000",
        string fpalv0 = "000",
        string ep0P = "000",
        string ep2P = "000")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ReservedWord = yoyaku,
                ParentSequenceNumber = oyatno,
                LineTypeCode = gyocd,
                ParallelNumber = heino,
                IncomingNumber = nyuseno,
                UpperParallelNumber = joheino,
                HierarchyNumber = kaisono,
                SeriesNumber = chokuno,
                UsedPhase = siyouso,
            },
        };
        r.Data.CircuitVoltage[0] = kpav0;
        r.Data.AttachedParameter.LoadVoltage[0] = fpalv0;
        r.Data.ElectricalParameterSlots[0].P = ep0P;
        r.Data.ElectricalParameterSlots[2].P = ep2P;
        return r;
    }

    // ── Siyousou2to1 ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("XN  ", "X   ", 1)]
    [InlineData("YN  ", "Y   ", 1)]
    [InlineData("XY  ", "X   ", 1)]
    [InlineData("YX  ", "X   ", 1)]
    public void 二相を一相へ変換する(string before, string after, int expectRet)
    {
        var r = Row(siyouso: before);
        Assert.Equal(expectRet, PhaseAssigner.Convert2PhaseTo1Phase(r.Data));
        Assert.Equal(after, r.Data.UsedPhase);
    }

    [Fact]
    public void 対象外の使用相は変換せず0を返す()
    {
        var r = Row(siyouso: "RST ");
        Assert.Equal(0, PhaseAssigner.Convert2PhaseTo1Phase(r.Data));
        Assert.Equal("RST ", r.Data.UsedPhase);
    }

    // ── sort3d ─────────────────────────────────────────────────────────────

    [Fact]
    public void 並列追番昇順にindexを並べ替える()
    {
        var mains = new[]
        {
            Row(heino: "003"),
            Row(heino: "001"),
            Row(heino: "002"),
        };
        var t = new[] { 0, 1, 2 };
        PhaseAssigner.SortByParallelNumber(mains, t, 3);
        Assert.Equal(new[] { 1, 2, 0 }, t); // heino 001,002,003 の index 順
    }

    // ── PropSetSou100Vkiki ───────────────────────────────────────────────────

    [Fact]
    public void 百V機器へXNYNを交互にセットし210はスキップ()
    {
        var mains = new[]
        {
            Row(kpav0: "105"),
            Row(kpav0: "210"), // 200V→スキップ(相セットせず、iは進む)
            Row(kpav0: "105"),
        };
        var t = new[] { 0, 1, 2 };
        PhaseAssigner.SetPhase100VDevices(mains, t, 3);
        Assert.Equal("XN  ", mains[0].Data.UsedPhase); // i=0
        Assert.Equal("    ", mains[1].Data.UsedPhase); // i=1 スキップ(未変更)
        Assert.Equal("XN  ", mains[2].Data.UsedPhase); // i=2 偶数
    }

    // ── PropSetSou3P3Wkiki / PropSetSou3P4Wkiki ───────────────────────────────

    [Fact]
    public void 三相三線はRSSTTRを循環セットする()
    {
        var mains = new[] { Row(), Row(), Row(), Row() };
        var t = new[] { 0, 1, 2, 3 };
        PhaseAssigner.SetPhase3P3WDevices(mains, t, 4);
        Assert.Equal("RS  ", mains[0].Data.UsedPhase);
        Assert.Equal("ST  ", mains[1].Data.UsedPhase);
        Assert.Equal("TR  ", mains[2].Data.UsedPhase);
        Assert.Equal("RS  ", mains[3].Data.UsedPhase);
    }

    [Fact]
    public void 三相四線はRNSNTNを循環セットする()
    {
        var mains = new[] { Row(), Row(), Row() };
        var t = new[] { 0, 1, 2 };
        PhaseAssigner.SetPhase3P4WDevices(mains, t, 3);
        Assert.Equal("RN  ", mains[0].Data.UsedPhase);
        Assert.Equal("SN  ", mains[1].Data.UsedPhase);
        Assert.Equal("TN  ", mains[2].Data.UsedPhase);
    }

    // ── PropSetSouMC ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("002", "XY  ")]
    [InlineData("003", "XNY ")]
    public void MCは極数に応じ使用相をセットする(string ep0P, string expected)
    {
        var r = Row(yoyaku: "MC ", ep0P: ep0P, siyouso: "    ");
        PhaseAssigner.SetPhaseMc(r.Data);
        Assert.Equal(expected, r.Data.UsedPhase);
    }

    [Fact]
    public void MCB2Pで負荷200Vは回路電圧210と使用相XYへ補正する()
    {
        var r = Row(yoyaku: "MCB2P", ep0P: "002", fpalv0: "200", kpav0: "000", siyouso: "    ");
        PhaseAssigner.SetPhaseMc(r.Data);
        Assert.Equal("210", r.Data.CircuitVoltage[0]);
        Assert.Equal("XY  ", r.Data.UsedPhase);
    }

    // ── PropSetSouMC2P ───────────────────────────────────────────────────────

    [Fact]
    public void MC2PとTBが親子なら中抜き相をセットする()
    {
        var mc = Row(datano: "005", yoyaku: "MC ", ep2P: "002", siyouso: "XNY ");
        var tb = Row(yoyaku: "TB ", oyatno: "005", ep2P: "003", siyouso: "XNY ");
        PhaseAssigner.SetPhaseMc2P(mc, tb);
        Assert.Equal("X Y ", mc.Data.UsedPhase); // 先頭3桁"X Y"、4桁目保持
        Assert.Equal("XNY ", tb.Data.UsedPhase);
    }

    [Fact]
    public void MC2PとTBが親子でなければ設定しない()
    {
        var mc = Row(datano: "005", yoyaku: "MC ", ep2P: "002", siyouso: "XNY ");
        var tb = Row(yoyaku: "TB ", oyatno: "009", ep2P: "003", siyouso: "XNY ");
        PhaseAssigner.SetPhaseMc2P(mc, tb);
        Assert.Equal("XNY ", mc.Data.UsedPhase); // 未変更
    }

    // ── PropSetSouMC3P ───────────────────────────────────────────────────────

    [Fact]
    public void MC3P直下が2Pなら交互にXNYNをセットする()
    {
        var mc = Row(datano: "005", yoyaku: "MC ", gyocd: "B ", siyouso: "XNY ",
            nyuseno: "001", joheino: "000", kaisono: "002", heino: "001", chokuno: "001");
        var next = Row(yoyaku: "TB ", oyatno: "005", ep0P: "002", siyouso: "    ",
            nyuseno: "001", joheino: "000", kaisono: "002", heino: "001", chokuno: "002");

        int mcCount = 0;
        PhaseAssigner.SetPhaseMc3P(mc, next, ref mcCount);

        Assert.Equal("X   ", mc.Data.UsedPhase);   // mcCount 偶数→X
        Assert.Equal("XN  ", next.Data.UsedPhase); // 親相の2桁目='N'
        Assert.Equal(1, mcCount);
    }

    // ── PropSetPrmFor2P200v ──────────────────────────────────────────────────

    [Fact]
    public void 二P二百V対応で使用相XYと回路電圧210へ変更する()
    {
        var r = Row(ep0P: "002", siyouso: "    ", kpav0: "000");
        PhaseAssigner.SetParamFor2P200V(r.Data);
        Assert.Equal("XY  ", r.Data.UsedPhase);
        Assert.Equal("210", r.Data.CircuitVoltage[0]);
    }

    // ── PropCount100Vkiki ────────────────────────────────────────────────────

    [Fact]
    public void 同一親の百V機器indexを収集する()
    {
        var mains = new[]
        {
            Row(oyatno: "005", fpalv0: "100"),          // 負荷100V→対象
            Row(oyatno: "005", fpalv0: "000", kpav0: "105"), // 回路105→対象
            Row(oyatno: "005", fpalv0: "200"),          // 対象外
            Row(oyatno: "009", fpalv0: "100"),          // 親違い
        };
        var t = new int[10];
        int count = 0;
        PhaseAssigner.CountVolt100VDevices(mains, "005", "ELB", t, ref count);
        Assert.Equal(2, count);
        Assert.Equal(0, t[0]);
        Assert.Equal(1, t[1]);
    }

    [Fact]
    public void 親MCの下はブレーカ以外を除外する()
    {
        var mains = new[]
        {
            Row(oyatno: "005", yoyaku: "MCB  ", fpalv0: "100"), // ブレーカ→対象
            Row(oyatno: "005", yoyaku: "TB   ", fpalv0: "100"), // ブレーカ以外→除外
        };
        var t = new int[10];
        int count = 0;
        PhaseAssigner.CountVolt100VDevices(mains, "005", "MC ", t, ref count);
        Assert.Equal(1, count);
        Assert.Equal(0, t[0]);
    }

    // ── PropMcChildVolt ──────────────────────────────────────────────────────

    [Fact]
    public void 親MC無指定なら子の最大負荷電圧を返す()
    {
        var oya = Row(datano: "005", yoyaku: "MC ", fpalv0: "000");
        var mains = new[]
        {
            oya,
            Row(oyatno: "005", fpalv0: "100"),
            Row(oyatno: "005", fpalv0: "200"),
            Row(oyatno: "009", fpalv0: "400"), // 親違い→除外
        };
        Assert.Equal(200, PhaseAssigner.GetMcChildMaxVolt(mains, oya));
    }

    [Fact]
    public void 親MCに負荷電圧指定があれば0を返す()
    {
        var oya = Row(datano: "005", yoyaku: "MC ", fpalv0: "200");
        Assert.Equal(0, PhaseAssigner.GetMcChildMaxVolt([oya], oya));
    }
}
