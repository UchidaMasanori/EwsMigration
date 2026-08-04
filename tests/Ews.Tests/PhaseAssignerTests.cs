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

    // ── Fyss3D_ResetRRYsou ───────────────────────────────────────────────────

    [Fact]
    public void RRYのCTは極数一で使用相をN付き2Pへ戻す()
    {
        var r = Row(yoyaku: "RRY ", siyouso: "X   ", ep0P: "001", ep2P: "000");
        r.Data.DataType[1] = "CT ";
        PhaseAssigner.ResetRRYPhase([r]);
        Assert.Equal("XN  ", r.Data.UsedPhase);
        Assert.Equal("022", r.Data.ElectricalParameterSlots[2].P);
    }

    [Fact]
    public void RRYのCTは極数無かつ2次側1でも戻す()
    {
        var r = Row(yoyaku: "RRY ", siyouso: "Y   ", ep0P: "000", ep2P: "001");
        r.Data.DataType[1] = "CT ";
        PhaseAssigner.ResetRRYPhase([r]);
        Assert.Equal("YN  ", r.Data.UsedPhase);
        Assert.Equal("022", r.Data.ElectricalParameterSlots[2].P);
    }

    [Fact]
    public void RRY以外は使用相を戻さない()
    {
        var r = Row(yoyaku: "MC  ", siyouso: "X   ", ep0P: "001");
        r.Data.DataType[1] = "CT ";
        PhaseAssigner.ResetRRYPhase([r]);
        Assert.Equal("X   ", r.Data.UsedPhase);
    }

    // ── PropChkElem1P2W ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("CT ")]
    [InlineData("CS ")]
    [InlineData("ZS ")]
    [InlineData("SE ")]
    [InlineData("SES ")]
    public void 分岐エレメント数一の計器類はエラーを返す(string dt0)
    {
        var r = Row(gyocd: "B  ");
        r.Data.DataType[0] = dt0;
        r.Data.ElectricalParameterSlots[0].E = "1";
        r.Data.DescriptionRow = "012";
        r.Data.DescriptionColumn = "034";
        var err = PhaseAssigner.CheckElement1P2W(r.Data);
        Assert.NotNull(err);
        Assert.Equal("FY-144E", err!.ErrorCode);
        Assert.Equal(12, err.LineNumber);
        Assert.Equal(34, err.Column);
        Assert.Equal("FYMEE90", err.MessageId);
    }

    [Fact]
    public void 分岐でもエレメント数が一でなければエラーなし()
    {
        var r = Row(gyocd: "B  ");
        r.Data.DataType[0] = "CT ";
        r.Data.ElectricalParameterSlots[0].E = "0";
        Assert.Null(PhaseAssigner.CheckElement1P2W(r.Data));
    }

    // ── PropCheckUseVolt ─────────────────────────────────────────────────────

    [Fact]
    public void 子200V親100Vはエラーを返す()
    {
        var oya = Row(fpalv0: "100");
        var ko = Row(fpalv0: "200");
        ko.Data.DescriptionRow = "005";
        ko.Data.DescriptionColumn = "007";
        var err = PhaseAssigner.CheckUseVolt(oya.Data, ko.Data);
        Assert.NotNull(err);
        Assert.Equal("FY-074E", err!.ErrorCode);
        Assert.Equal(5, err.LineNumber);
        Assert.Equal(7, err.Column);
    }

    [Fact]
    public void 子200V親200Vはエラーなし()
    {
        var oya = Row(fpalv0: "200");
        var ko = Row(fpalv0: "200");
        Assert.Null(PhaseAssigner.CheckUseVolt(oya.Data, ko.Data));
    }

    // ── PropChkLacslRryFuka ──────────────────────────────────────────────────

    [Fact]
    public void RRYのLAは極数一200Vでエラーを返す()
    {
        var r = Row(yoyaku: "RRY ", ep0P: "001", fpalv0: "200");
        r.Data.DataType[1] = "LA ";
        var err = PhaseAssigner.CheckLacslRryLoad(r.Data);
        Assert.NotNull(err);
        Assert.Equal("FY-074E", err!.ErrorCode);
    }

    [Fact]
    public void RRYのLAでも100Vならエラーなし()
    {
        var r = Row(yoyaku: "RRY ", ep0P: "001", fpalv0: "100");
        r.Data.DataType[1] = "LA ";
        Assert.Null(PhaseAssigner.CheckLacslRryLoad(r.Data));
    }

    // ── Fyss3D_Katagiri ──────────────────────────────────────────────────────

    [Fact]
    public void 片切りMC2次側無しは極数から使用相N相を削除する()
    {
        var r = Row(datano: "005", yoyaku: "MC      ", siyouso: "XN  ", ep0P: "000", kpav0: "105");
        r.Data.IdentityNumber = "01";
        PhaseAssigner.AdjustKatagiriPhase([r]);
        Assert.Equal("X   ", r.Data.UsedPhase); // ikpap=1 で index1 以降をクリア
    }

    [Fact]
    public void 片切りMC2次側有りは回路極数で使用相をクリアする()
    {
        var oya = Row(datano: "005", yoyaku: "MC      ", siyouso: "XN  ", ep0P: "000");
        oya.Data.CircuitPoleCount = '1';
        var ko = Row(oyatno: "005");
        PhaseAssigner.AdjustKatagiriPhase([oya, ko]);
        Assert.Equal("X   ", oya.Data.UsedPhase);
    }

    [Fact]
    public void 片切りMCで使用相がXNYN以外はパスして変更しない()
    {
        var r = Row(datano: "005", yoyaku: "MC      ", siyouso: "XY  ", ep0P: "000", kpav0: "105");
        r.Data.IdentityNumber = "01";
        PhaseAssigner.AdjustKatagiriPhase([r]);
        Assert.Equal("XY  ", r.Data.UsedPhase);
    }

    // ── Fyss3D_Keiki_set ─────────────────────────────────────────────────────

    private static MainCircuitResult MeterRow(string yoyaku, char kiryoso, char ep0Qty = '0')
    {
        var r = Row(yoyaku: yoyaku);
        r.Data.CircuitElement = kiryoso;
        r.Data.ElectricalParameterSlots[0].Qty = ep0Qty;
        return r;
    }

    [Fact]
    public void CT従属のAMは通常KL計器箱付ASはKKLをセットする()
    {
        var ct = MeterRow("CT      ", '2');
        var am1 = MeterRow("AM      ", '2');
        var am2 = MeterRow("AM      ", '2');
        am2.Data.DataType[2] = "AS     ";
        PhaseAssigner.SetMeterCircuitPhase([ct, am1, am2]);
        Assert.Equal("KL  ", am1.Data.UsedPhase);
        Assert.Equal("KKL ", am2.Data.UsedPhase);
    }

    [Fact]
    public void CT従属のWHはC原典代入バグで常にKKLになる()
    {
        var ct = MeterRow("CT      ", '2');
        var wh = MeterRow("WH      ", '2');
        PhaseAssigner.SetMeterCircuitPhase([ct, wh]);
        Assert.Equal("KKL ", wh.Data.UsedPhase);
        Assert.Equal('2', ct.Data.ElectricalParameterSlots[2].Qty); // 代入副作用
    }

    [Fact]
    public void 単相3線のヒューズ数量1はXをオーバレイし4桁目を保持する()
    {
        var p = MeterRow("P       ", '0');
        p.Data.CircuitPhaseCount = '1';
        p.Data.CircuitWireType = '3';
        var f = MeterRow("F       ", '3', ep0Qty: '1');
        f.Data.UsedPhase = "???N"; // 4桁目保持の確認
        PhaseAssigner.SetMeterCircuitPhase([p, f]);
        Assert.Equal("X  N", f.Data.UsedPhase);
    }

    [Fact]
    public void 三相3線のワットメータはRSTをセットする()
    {
        var p = MeterRow("P       ", '0');
        p.Data.CircuitPhaseCount = '3';
        p.Data.CircuitWireType = '3';
        var wh = MeterRow("WH      ", '3', ep0Qty: '1');
        PhaseAssigner.SetMeterCircuitPhase([p, wh]);
        Assert.Equal("RST ", wh.Data.UsedPhase);
    }

    [Fact]
    public void 単相2線105Vのヒューズ数量2はXNをセットする()
    {
        var p = MeterRow("P       ", '0');
        p.Data.CircuitPhaseCount = '1';
        p.Data.CircuitWireType = '2';
        var f = MeterRow("F       ", '3', ep0Qty: '2');
        f.Data.CircuitVoltage[0] = "105";
        PhaseAssigner.SetMeterCircuitPhase([p, f]);
        Assert.Equal("XN  ", f.Data.UsedPhase);
    }

    [Fact]
    public void F01は次要素のVSを参照してRTをセットする()
    {
        var p = MeterRow("P       ", '0');
        p.Data.CircuitPhaseCount = '3';
        p.Data.CircuitWireType = '3';
        var f = MeterRow("F       ", '3', ep0Qty: '2'); // F,2,3,3 → F01
        f.SequenceNumber = "010";
        var vs = MeterRow("VS      ", '0', ep0Qty: '1');
        vs.Data.ParentSequenceNumber = "010";
        PhaseAssigner.SetMeterCircuitPhase([p, f, vs]);
        Assert.Equal("RT  ", f.Data.UsedPhase);
    }

    [Fact]
    public void F01で次要素が該当しなければ既定のRSをセットする()
    {
        var p = MeterRow("P       ", '0');
        p.Data.CircuitPhaseCount = '3';
        p.Data.CircuitWireType = '3';
        var f = MeterRow("F       ", '3', ep0Qty: '2');
        f.SequenceNumber = "010";
        var other = MeterRow("XX      ", '0');
        PhaseAssigner.SetMeterCircuitPhase([p, f, other]);
        Assert.Equal("RS  ", f.Data.UsedPhase);
    }

    [Fact]
    public void ZCT従属のLGRとELRはKLをセットする()
    {
        var zct = MeterRow("ZCT     ", '5');
        var lgr = MeterRow("LGR     ", '5');
        var elr = MeterRow("ELR     ", '5');
        PhaseAssigner.SetMeterCircuitPhase([zct, lgr, elr]);
        Assert.Equal("KL  ", lgr.Data.UsedPhase);
        Assert.Equal("KL  ", elr.Data.UsedPhase);
    }

    // ── PropGetF800Index 系 ──────────────────────────────────────────────────

    private static MainCircuitResult IdxRow(
        string datano = "000", string oyatno = "000", string yoyaku = "", string gyocd = "   ",
        char kiryoso = '0', char kpaph = '0', char kpawr = '0', char kpap = '0',
        char sentflg = ' ', string ep0P = "000")
    {
        var r = Row(datano: datano, oyatno: oyatno, yoyaku: yoyaku, gyocd: gyocd, ep0P: ep0P);
        r.Data.CircuitElement = kiryoso;
        r.Data.CircuitPhaseCount = kpaph;
        r.Data.CircuitWireType = kpawr;
        r.Data.CircuitPoleCount = kpap;
        r.Work.LeadingEquipmentFlag = sentflg;
        return r;
    }

    [Fact]
    public void F800Indexは分岐送り機器をtaその他をtbヒューズをtfへ振り分ける()
    {
        var mains = new[]
        {
            IdxRow(datano: "001", oyatno: "999", yoyaku: "MC      "), // 親(oyatno不一致)
            IdxRow(oyatno: "005", yoyaku: "MCB     ", gyocd: "B  ", kiryoso: '1', kpaph: '1', kpawr: '2', kpap: '1', sentflg: '1'), // ta
            IdxRow(oyatno: "005", yoyaku: "F       ", gyocd: "B  ", kiryoso: '1', kpaph: '1', kpawr: '2', kpap: '1', sentflg: '1'), // tf
            IdxRow(oyatno: "005", yoyaku: "TB      ", gyocd: "   ", kiryoso: '1', kpaph: '1', kpawr: '2', kpap: '1'), // tb
        };
        var res = PhaseAssigner.CollectF800Index(mains, "005", '0', '1', '2', '1');
        Assert.Equal(new[] { 1, 2, 3 }, res.T.ToArray());
        Assert.Equal(new[] { 1 }, res.Ta.ToArray());
        Assert.Equal(new[] { 2 }, res.Tf.ToArray());
        Assert.Equal(new[] { 3 }, res.Tb.ToArray());
    }

    [Fact]
    public void F800IndexはWH計器回路とCT計器回路をパスする()
    {
        var mains = new[]
        {
            IdxRow(oyatno: "005", yoyaku: "WH      ", kiryoso: '3', kpaph: '1', kpawr: '2', kpap: '1'),
            IdxRow(oyatno: "005", yoyaku: "CT      ", kiryoso: '2', kpaph: '1', kpawr: '2', kpap: '1'),
        };
        var res = PhaseAssigner.CollectF800Index(mains, "005", '0', '1', '2', '1');
        Assert.Empty(res.T);
    }

    [Fact]
    public void F800Indexは極数不一致で1P対象のときtaへ登録する()
    {
        var mains = new[]
        {
            IdxRow(oyatno: "005", yoyaku: "MCB     ", gyocd: "B  ", kiryoso: '1', kpaph: '1', kpawr: '2', kpap: '3', sentflg: '1'),
        };
        var res = PhaseAssigner.CollectF800Index(mains, "005", '0', '1', '2', '1');
        Assert.Empty(res.T); // 極数不一致で主回路には入らない
        Assert.Equal(new[] { 0 }, res.Ta.ToArray());
    }

    [Fact]
    public void F800Indexは特注BO単独配置を対象外にする()
    {
        var mains = new[]
        {
            IdxRow(oyatno: "005", yoyaku: "MCB     ", gyocd: "BO ", kiryoso: '1', kpaph: '1', kpawr: '2', kpap: '3', sentflg: '1'),
        };
        var res = PhaseAssigner.CollectF800Index(mains, "005", '3', '1', '2', '1'); // hycpskbn='3'
        Assert.Empty(res.Ta);
    }

    [Fact]
    public void F800Index34はヒューズを識別せず分岐送りはtaへ入れる()
    {
        var mains = new[]
        {
            IdxRow(oyatno: "005", yoyaku: "F       ", gyocd: "B  ", kiryoso: '1', kpaph: '3', kpawr: '4', kpap: '4', sentflg: '1'),
            IdxRow(oyatno: "005", yoyaku: "TB      ", gyocd: "   ", kiryoso: '1', kpaph: '3', kpawr: '4', kpap: '4'),
        };
        var res = PhaseAssigner.CollectF800Index34(mains, "005", '3', '4', '4');
        Assert.Equal(new[] { 0, 1 }, res.T.ToArray());
        Assert.Equal(new[] { 0 }, res.Ta.ToArray()); // F もヒューズ識別せず ta
        Assert.Equal(new[] { 1 }, res.Tb.ToArray());
    }

    [Fact]
    public void F800Index34Pは2Pをta3Pをtbその他をtへ振り分ける()
    {
        var mains = new[]
        {
            IdxRow(oyatno: "005", yoyaku: "MCB     ", gyocd: "B  ", kiryoso: '1', kpaph: '3', kpawr: '4', kpap: '4', sentflg: '1', ep0P: "002"),
            IdxRow(oyatno: "005", yoyaku: "MCB     ", gyocd: "B  ", kiryoso: '1', kpaph: '3', kpawr: '4', kpap: '4', sentflg: '1', ep0P: "003"),
            IdxRow(oyatno: "005", yoyaku: "TB      ", gyocd: "   ", kiryoso: '1', kpaph: '3', kpawr: '4', kpap: '4'),
        };
        var res = PhaseAssigner.CollectF800Index34P(mains, "005", '3', '4', '4');
        Assert.Equal(new[] { 0 }, res.Ta.ToArray()); // 2P
        Assert.Equal(new[] { 1 }, res.Tb.ToArray()); // 3P
        Assert.Equal(new[] { 2 }, res.T.ToArray()); // その他
    }

    [Fact]
    public void F800Index33は極数3P機器数を計上する()
    {
        var mains = new[]
        {
            IdxRow(oyatno: "005", yoyaku: "MCB     ", gyocd: "B  ", kiryoso: '1', kpaph: '3', kpawr: '3', kpap: '3', sentflg: '1'),
            IdxRow(oyatno: "005", yoyaku: "TB      ", kiryoso: '1', kpaph: '3', kpawr: '3', kpap: '3'),
            IdxRow(oyatno: "005", yoyaku: "TB      ", kiryoso: '1', kpaph: '1', kpawr: '2', kpap: '1'), // 3Pでない
        };
        var res = PhaseAssigner.CollectF800Index33(mains, "005", '0', '3', '3', '3');
        Assert.Equal(2, res.Count3P);
        Assert.Equal(new[] { 0, 1 }, res.T.ToArray());
        Assert.Equal(new[] { 0 }, res.Ta.ToArray());
        Assert.Equal(new[] { 1 }, res.Tb.ToArray());
    }

    // ── ChangeSiyousouFor3P4W (PropChgSiyousou / PropConnect3P4W) ──────────────

    private static MainCircuitResult PowerRow(
        string yoyaku, string gyocd, string gyono, char kpaph, char kpawr,
        string kno, string siyouso = "    ", string oyatno = "000")
    {
        var r = Row(yoyaku: yoyaku, gyocd: gyocd, oyatno: oyatno, siyouso: siyouso);
        r.Data.LineTypeNumber = gyono;
        r.Data.CircuitPhaseCount = kpaph;
        r.Data.CircuitWireType = kpawr;
        r.Data.SystemNumber = kno;
        return r;
    }

    [Fact]
    public void 使用相変更_3P4W連携の1P3W電源と下流をRNSへ変更する()
    {
        var mains = new[]
        {
            PowerRow("P       ", "P  ", "01", '3', '4', "001"),                        // 3P4W電源
            PowerRow("P       ", "P  ", "01", '1', '3', "002", siyouso: "XNY "),       // 1P3W電源(対象)
            PowerRow("MCB     ", "B  ", "  ", '1', '3', "002", siyouso: "XNY "),       // 下流
        };
        PhaseAssigner.ChangeSiyousouFor3P4W(mains);
        Assert.Equal("RNS ", mains[1].Data.UsedPhase);
        Assert.Equal("RNS ", mains[2].Data.UsedPhase);
    }

    [Fact]
    public void 使用相変更_後続に同一行種番号の電源があればパスする()
    {
        var mains = new[]
        {
            PowerRow("P       ", "P  ", "01", '1', '3', "001", siyouso: "XNY "),       // 1P3W電源
            PowerRow("MCB     ", "B  ", "  ", '1', '3', "001", siyouso: "XN  "),       // 下流
            PowerRow("P       ", "P  ", "01", '3', '4', "003"),                        // 後続の同一行種電源(3P4W)
        };
        PhaseAssigner.ChangeSiyousouFor3P4W(mains);
        Assert.Equal("XNY ", mains[0].Data.UsedPhase);
        Assert.Equal("XN  ", mains[1].Data.UsedPhase);
    }

    [Fact]
    public void 使用相変更_3P4W連携が無ければ変更しない()
    {
        var mains = new[]
        {
            PowerRow("P       ", "P  ", "01", '1', '3', "001", siyouso: "XNY "),
            PowerRow("MCB     ", "B  ", "  ", '1', '3', "001", siyouso: "XN  "),
        };
        PhaseAssigner.ChangeSiyousouFor3P4W(mains);
        Assert.Equal("XNY ", mains[0].Data.UsedPhase);
    }

    [Fact]
    public void 使用相変更_行種番号が0なら連携無しとして変更しない()
    {
        var mains = new[]
        {
            PowerRow("P       ", "P  ", "00", '1', '3', "001", siyouso: "XNY "),
            PowerRow("P       ", "P  ", "00", '3', '4', "002"),
        };
        PhaseAssigner.ChangeSiyousouFor3P4W(mains);
        Assert.Equal("XNY ", mains[0].Data.UsedPhase);
    }

    // ── CopyMeterPhaseByIdentity ──────────────────────────────────────────────

    [Fact]
    public void 計器同一認識番号_設定済み機器の使用相をコピーする()
    {
        var wh = Row(yoyaku: "WH      ", siyouso: "    ");
        wh.Data.IdentityNumber = "01";
        var mate = Row(yoyaku: "MCB     ", siyouso: "XN  ");
        mate.Data.IdentityNumber = "01";
        PhaseAssigner.CopyMeterPhaseByIdentity([wh, mate]);
        Assert.Equal("XN  ", wh.Data.UsedPhase);
    }

    [Fact]
    public void 計器同一認識番号_認識番号00は対象外()
    {
        var wh = Row(yoyaku: "WH      ", siyouso: "    ");
        wh.Data.IdentityNumber = "00";
        var mate = Row(yoyaku: "MCB     ", siyouso: "XN  ");
        mate.Data.IdentityNumber = "00";
        PhaseAssigner.CopyMeterPhaseByIdentity([wh, mate]);
        Assert.Equal("    ", wh.Data.UsedPhase);
    }

    // ── ReducePhaseForRelayAndAmmeter ─────────────────────────────────────────

    [Fact]
    public void リレー極数変更_RRYと下流を2極から1極へ変更する()
    {
        var rry = Row(datano: "001", yoyaku: "RRY     ", oyatno: "000", siyouso: "XN  ", ep0P: "001");
        rry.Data.SystemKind = '1';
        var karyu = Row(datano: "002", yoyaku: "TB      ", oyatno: "001", siyouso: "YN  ");
        PhaseAssigner.ReducePhaseForRelayAndAmmeter([rry, karyu]);
        Assert.Equal("X   ", rry.Data.UsedPhase);
        Assert.Equal("Y   ", karyu.Data.UsedPhase);
    }

    [Fact]
    public void リレー極数変更_RRYコンパクトタイプは変更しない()
    {
        var rry = Row(yoyaku: "RRY     ", siyouso: "XN  ", ep0P: "001");
        rry.Data.DataType[1] = "CT ";
        PhaseAssigner.ReducePhaseForRelayAndAmmeter([rry]);
        Assert.Equal("XN  ", rry.Data.UsedPhase);
    }

    [Fact]
    public void 電流計_RSTはSへ他はN相以降をクリアする()
    {
        var am1 = Row(yoyaku: "AM      ", siyouso: "RST ");
        am1.Data.CircuitElement = '1';
        var am2 = Row(yoyaku: "AM      ", siyouso: "XNY ");
        am2.Data.CircuitElement = '1';
        PhaseAssigner.ReducePhaseForRelayAndAmmeter([am1, am2]);
        Assert.Equal("S   ", am1.Data.UsedPhase);
        Assert.Equal("X   ", am2.Data.UsedPhase);
    }

    // ── AssignPhaseDcOr1P2WPole2 / AssignPhase1P2WPole1 ───────────────────────

    [Fact]
    public void 電源DCまたは1P2W極2_同系統の下流をXYにする()
    {
        var p = Row(yoyaku: "P       ", siyouso: "    ");
        p.Data.SystemNumber = "001";
        var child = Row(yoyaku: "MCB     ", siyouso: "    ");
        child.Data.SystemNumber = "001";
        var other = Row(yoyaku: "MCB     ", siyouso: "    ");
        other.Data.SystemNumber = "002";
        PhaseAssigner.AssignPhaseDcOr1P2WPole2([p, child, other], 0);
        Assert.Equal("XY  ", p.Data.UsedPhase);
        Assert.Equal("XY  ", child.Data.UsedPhase);
        Assert.Equal("    ", other.Data.UsedPhase); // 系統番号が変わったら対象外
    }

    [Fact]
    public void 電源1P2W極1_同系統の下流をXNにする()
    {
        var p = Row(yoyaku: "P       ", siyouso: "    ");
        p.Data.SystemNumber = "001";
        var child = Row(yoyaku: "MCB     ", siyouso: "    ");
        child.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase1P2WPole1([p, child], 0);
        Assert.Null(err);
        Assert.Equal("XN  ", p.Data.UsedPhase);
        Assert.Equal("XN  ", child.Data.UsedPhase);
    }

    [Fact]
    public void 電源1P2W極1_エレメント数不正でエラーを返す()
    {
        var p = Row(yoyaku: "P       ", siyouso: "    ");
        p.Data.SystemNumber = "001";
        var child = Row(yoyaku: "CT      ", gyocd: "B  ", siyouso: "    ");
        child.Data.SystemNumber = "001";
        child.Data.ElectricalParameterSlots[0].E = "1";
        child.Data.DataType[0] = "CT ";
        var err = PhaseAssigner.AssignPhase1P2WPole1([p, child], 0);
        Assert.NotNull(err);
        Assert.Equal("FY-144E", err!.ErrorCode);
    }

    // ── AssignParent1P3WPole3 ────────────────────────────────────────────────

    private static void SetKpa(MainCircuitResult r, char ph, char wr, char pole)
    {
        r.Data.CircuitPhaseCount = ph;
        r.Data.CircuitWireType = wr;
        r.Data.CircuitPoleCount = pole;
    }

    [Fact]
    public void 親1P3W3_子1P3W3は親の使用相をコピーする()
    {
        var parent = Row(datano: "001", yoyaku: "MC      ", siyouso: "XNY ");
        SetKpa(parent, '1', '3', '3');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "ELB     ", siyouso: "    ");
        SetKpa(child, '1', '3', '3');
        int mc = 0;
        var set = new HashSet<string>();
        PhaseAssigner.AssignParent1P3WPole3([parent, child], 1, 0, ' ', ref mc, set);
        Assert.Equal("XNY ", child.Data.UsedPhase);
    }

    [Fact]
    public void 親1P3W3_子1P2W2はXYにする()
    {
        var parent = Row(datano: "001", yoyaku: "MC      ", siyouso: "XNY ");
        SetKpa(parent, '1', '3', '3');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '1', '2', '2');
        int mc = 0;
        var set = new HashSet<string>();
        PhaseAssigner.AssignParent1P3WPole3([parent, child], 1, 0, ' ', ref mc, set);
        Assert.Equal("XY  ", child.Data.UsedPhase);
    }

    [Fact]
    public void 親1P3W3_子1P2W1は同一親100V機器をXNYN交互にする()
    {
        var parent = Row(datano: "001", yoyaku: "MC      ", siyouso: "XNY ");
        SetKpa(parent, '1', '3', '3');
        // 同一親を持つ 1P2W1 の子2台(PropGetF800Index の主回路 t に入る条件)
        var c1 = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", heino: "001", siyouso: "    ");
        SetKpa(c1, '1', '2', '1');
        c1.Data.CircuitElement = '1';
        var c2 = Row(datano: "003", oyatno: "001", yoyaku: "MCB     ", heino: "002", siyouso: "    ");
        SetKpa(c2, '1', '2', '1');
        c2.Data.CircuitElement = '1';
        int mc = 0;
        var set = new HashSet<string>();
        var mains = new[] { parent, c1, c2 };
        PhaseAssigner.AssignParent1P3WPole3(mains, 1, 0, ' ', ref mc, set);
        Assert.Equal("XN  ", c1.Data.UsedPhase);
        Assert.Equal("YN  ", c2.Data.UsedPhase);
        Assert.Contains("001", set); // 親追番が処理済みに登録される
    }

    [Fact]
    public void 親1P3W3_処理済み親は再処理しない()
    {
        var parent = Row(datano: "001", yoyaku: "MC      ", siyouso: "XNY ");
        SetKpa(parent, '1', '3', '3');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '1', '2', '1');
        int mc = 0;
        var set = new HashSet<string> { "001" };
        PhaseAssigner.AssignParent1P3WPole3([parent, child], 1, 0, ' ', ref mc, set);
        Assert.Equal("    ", child.Data.UsedPhase); // 処理済みなので変更なし
    }

    [Fact]
    public void 親1P3W3_想定外の子種別は設計エラーを通知する()
    {
        var parent = Row(datano: "001", yoyaku: "MC      ", siyouso: "XNY ");
        SetKpa(parent, '1', '3', '3');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '3', '4', '4'); // 想定外
        int mc = 0;
        var set = new HashSet<string>();
        int? reported = null;
        PhaseAssigner.AssignParent1P3WPole3([parent, child], 1, 0, ' ', ref mc, set, n => reported = n);
        Assert.Equal(1, reported);
    }

    // ── AssignParent1P2WPole2 ────────────────────────────────────────────────

    [Fact]
    public void 親1P2W2_子極1は親相をXYからX等へ変換する()
    {
        var parent = Row(datano: "001", yoyaku: "MCB     ", siyouso: "XN  ");
        SetKpa(parent, '1', '2', '2');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '1', '2', '1');
        var set = new HashSet<string>();
        var err = PhaseAssigner.AssignParent1P2WPole2([parent, child], 1, 0, set);
        Assert.Null(err);
        Assert.Equal("X   ", child.Data.UsedPhase);
    }

    [Fact]
    public void 親1P2W2_非MC子極2は親の使用相をコピーする()
    {
        var parent = Row(datano: "001", yoyaku: "ELB     ", siyouso: "RS  ");
        SetKpa(parent, '1', '2', '2');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '1', '2', '2');
        var set = new HashSet<string>();
        var err = PhaseAssigner.AssignParent1P2WPole2([parent, child], 1, 0, set);
        Assert.Null(err);
        Assert.Equal("RS  ", child.Data.UsedPhase);
    }

    [Fact]
    public void 親1P2W2_親MC無指定なら親相と回路電圧をコピーして終了する()
    {
        var parent = Row(datano: "001", yoyaku: "MC      ", siyouso: "XY  ", kpav0: "210", fpalv0: "200");
        SetKpa(parent, '1', '2', '2');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ", fpalv0: "000");
        SetKpa(child, '1', '2', '2');
        var set = new HashSet<string>();
        var err = PhaseAssigner.AssignParent1P2WPole2([parent, child], 1, 0, set);
        Assert.Null(err);
        Assert.Equal("XY  ", child.Data.UsedPhase);
        Assert.Equal("210", child.Data.CircuitVoltage[0]);
    }

    [Fact]
    public void 親1P2W2_親MCで100V機器は使用相を交互設定し回路電圧を105にする()
    {
        var parent = Row(datano: "001", yoyaku: "MC      ", siyouso: "XN  ", fpalv0: "000");
        SetKpa(parent, '1', '2', '2');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ", fpalv0: "100");
        SetKpa(child, '1', '2', '2');
        var set = new HashSet<string>();
        var err = PhaseAssigner.AssignParent1P2WPole2([parent, child], 1, 0, set);
        Assert.Null(err);
        Assert.Equal("XN  ", child.Data.UsedPhase);
        Assert.Equal("105", child.Data.CircuitVoltage[0]);
        Assert.Contains("001", set);
    }

    [Fact]
    public void 親1P2W2_LACSLリレー負荷電圧不正でエラーを返す()
    {
        var parent = Row(datano: "001", yoyaku: "ELB     ", siyouso: "XN  ");
        SetKpa(parent, '1', '2', '2');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "RRY     ", siyouso: "    ", fpalv0: "200", ep0P: "001");
        SetKpa(child, '1', '2', '2');
        child.Data.DataType[1] = "LA ";
        var set = new HashSet<string>();
        var err = PhaseAssigner.AssignParent1P2WPole2([parent, child], 1, 0, set);
        Assert.NotNull(err);
        Assert.Equal("FY-074E", err!.ErrorCode);
    }

    [Fact]
    public void 親1P2W2_想定外の子種別は設計エラーを通知する()
    {
        var parent = Row(datano: "001", yoyaku: "MCB     ", siyouso: "XN  ");
        SetKpa(parent, '1', '2', '2');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '3', '4', '4'); // 想定外
        var set = new HashSet<string>();
        int? reported = null;
        PhaseAssigner.AssignParent1P2WPole2([parent, child], 1, 0, set, n => reported = n);
        Assert.Equal(2, reported);
    }

    // ── AssignParent1P2WPole1 ────────────────────────────────────────────────

    [Fact]
    public void 親1P2W1_非MC子1P2W1は親の使用相をコピーする()
    {
        var parent = Row(datano: "001", yoyaku: "MCB     ", siyouso: "XN  ");
        SetKpa(parent, '1', '2', '1');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '1', '2', '1');
        var set = new HashSet<string>();
        var err = PhaseAssigner.AssignParent1P2WPole1([parent, child], 1, 0, set);
        Assert.Null(err);
        Assert.Equal("XN  ", child.Data.UsedPhase);
    }

    [Fact]
    public void 親1P2W1_親MCで100V機器は交互設定する()
    {
        var parent = Row(datano: "001", yoyaku: "MC      ", siyouso: "XN  ", fpalv0: "000");
        SetKpa(parent, '1', '2', '1');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ", fpalv0: "100");
        SetKpa(child, '1', '2', '1');
        var set = new HashSet<string>();
        var err = PhaseAssigner.AssignParent1P2WPole1([parent, child], 1, 0, set);
        Assert.Null(err);
        Assert.Equal("XN  ", child.Data.UsedPhase);
        Assert.Contains("001", set);
    }

    [Fact]
    public void 親1P2W1_親MCで100V機器なしなら親の使用相をコピーする()
    {
        var parent = Row(datano: "001", yoyaku: "MC      ", siyouso: "XN  ", fpalv0: "000");
        SetKpa(parent, '1', '2', '1');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ", fpalv0: "000");
        SetKpa(child, '1', '2', '1');
        var set = new HashSet<string>();
        var err = PhaseAssigner.AssignParent1P2WPole1([parent, child], 1, 0, set);
        Assert.Null(err);
        Assert.Equal("XN  ", child.Data.UsedPhase);
    }

    [Fact]
    public void 親1P2W1_TB3P対応で親XspaceY子XNYにする()
    {
        var parent = Row(datano: "001", yoyaku: "MC      ", siyouso: "XN  ", fpalv0: "000", ep0P: "002");
        SetKpa(parent, '1', '2', '1');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "TB      ", siyouso: "    ", fpalv0: "200", ep0P: "003");
        SetKpa(child, '1', '2', '1');
        var set = new HashSet<string>();
        var err = PhaseAssigner.AssignParent1P2WPole1([parent, child], 1, 0, set);
        Assert.Null(err);
        Assert.Equal("X Y ", parent.Data.UsedPhase);
        Assert.Equal("XNY ", child.Data.UsedPhase);
    }

    [Fact]
    public void 親1P2W1_子1P3W3で親MC中抜きはXNYにする()
    {
        var parent = Row(datano: "001", yoyaku: "MC      ", siyouso: "XN  ");
        SetKpa(parent, '1', '2', '1');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '1', '3', '3');
        var set = new HashSet<string>();
        var err = PhaseAssigner.AssignParent1P2WPole1([parent, child], 1, 0, set);
        Assert.Null(err);
        Assert.Equal("XNY ", child.Data.UsedPhase);
    }

    [Fact]
    public void 親1P2W1_想定外の子種別は設計エラーを通知する()
    {
        var parent = Row(datano: "001", yoyaku: "MCB     ", siyouso: "XN  ");
        SetKpa(parent, '1', '2', '1');
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '3', '4', '4'); // 想定外
        var set = new HashSet<string>();
        int? reported = null;
        PhaseAssigner.AssignParent1P2WPole1([parent, child], 1, 0, set, n => reported = n);
        Assert.Equal(3, reported);
    }

    // ── AssignPhase3P3W ──────────────────────────────────────────────────────

    [Fact]
    public void 電源3P3W_子3P3W3は親の使用相をコピーする()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '3', '3');
        p.Data.SystemNumber = "001";
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '3', '3', '3');
        child.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P3W([p, child], 0, '0');
        Assert.Null(err);
        Assert.Equal("RST ", p.Data.UsedPhase);
        Assert.Equal("RST ", child.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P3W_親MC子3Pは親の極数電圧使用相をコピーする()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '3', '3');
        p.Data.SystemNumber = "001";
        var mc = Row(datano: "002", oyatno: "001", yoyaku: "MC      ", siyouso: "    ", kpav0: "210");
        SetKpa(mc, '3', '3', '3');
        mc.Data.SystemNumber = "001";
        var child = Row(datano: "003", oyatno: "002", yoyaku: "MCB     ", siyouso: "    ", kpav0: "000", ep0P: "003", fpalv0: "000");
        SetKpa(child, '3', '3', '1'); // 3P3W3でも1P2W2でもない
        child.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P3W([p, mc, child], 0, '0');
        Assert.Null(err);
        Assert.Equal('3', child.Data.CircuitPoleCount);
        Assert.Equal("210", child.Data.CircuitVoltage[0]);
        Assert.Equal("RST ", child.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P3W_親3P3W3で想定外の子種別は設計エラー4を通知する()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '3', '3');
        p.Data.SystemNumber = "001";
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '3', '4', '4'); // 想定外
        child.Data.SystemNumber = "001";
        int? reported = null;
        PhaseAssigner.AssignPhase3P3W([p, child], 0, '0', n => reported = n);
        Assert.Equal(4, reported);
    }

    [Fact]
    public void 電源3P3W_親1P2W2のMC子3Pは3P3W化する()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '3', '3');
        p.Data.SystemNumber = "001";
        var mc = Row(datano: "002", oyatno: "001", yoyaku: "MC      ", siyouso: "    ");
        SetKpa(mc, '1', '2', '2');
        mc.Data.SystemNumber = "001";
        var child = Row(datano: "003", oyatno: "002", yoyaku: "MCB     ", siyouso: "    ", ep0P: "003");
        SetKpa(child, '1', '2', '2');
        child.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P3W([p, mc, child], 0, '0');
        Assert.Null(err);
        Assert.Equal('3', child.Data.CircuitPhaseCount);
        Assert.Equal('3', child.Data.CircuitWireType);
        Assert.Equal('3', child.Data.CircuitPoleCount);
        Assert.Equal("210", child.Data.CircuitVoltage[0]);
        Assert.Equal("RST ", child.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P3W_親1P2W2で想定外の子種別は設計エラー5を通知する()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '3', '3');
        p.Data.SystemNumber = "001";
        var parent = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(parent, '1', '2', '2');
        parent.Data.SystemNumber = "001";
        var child = Row(datano: "003", oyatno: "002", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '3', '4', '4'); // 想定外
        child.Data.SystemNumber = "001";
        int? reported = null;
        PhaseAssigner.AssignPhase3P3W([p, parent, child], 0, '0', n => reported = n);
        Assert.Equal(5, reported);
    }

    // ── AssignPhase3P4WNoV2 ──────────────────────────────────────────────────

    [Fact]
    public void 電源3P4Wv0_親3P4W4の子3P4W4は親の使用相をコピーする()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.SystemNumber = "001";
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '3', '4', '4');
        child.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P4WNoV2([p, child], 0);
        Assert.Null(err);
        Assert.Equal("RSTN", p.Data.UsedPhase);
        Assert.Equal("RSTN", child.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P4Wv0_親3P4W4の子3P3W3はRSTにする()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.SystemNumber = "001";
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '3', '3', '3');
        child.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P4WNoV2([p, child], 0);
        Assert.Null(err);
        Assert.Equal("RST ", child.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P4Wv0_親3P4W4の子1P2W2はRSSTを交互に設定する()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.SystemNumber = "001";
        var c1 = Row(datano: "002", oyatno: "001", heino: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(c1, '1', '2', '2');
        c1.Data.SystemNumber = "001";
        var c2 = Row(datano: "003", oyatno: "001", heino: "002", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(c2, '1', '2', '2');
        c2.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P4WNoV2([p, c1, c2], 0);
        Assert.Null(err);
        Assert.Equal("RS  ", c1.Data.UsedPhase);
        Assert.Equal("ST  ", c2.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P4Wv0_親3P4W4の子1P2W1はRNSNを交互に設定する()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.SystemNumber = "001";
        var c1 = Row(datano: "002", oyatno: "001", heino: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(c1, '1', '2', '1');
        c1.Data.SystemNumber = "001";
        var c2 = Row(datano: "003", oyatno: "001", heino: "002", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(c2, '1', '2', '1');
        c2.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P4WNoV2([p, c1, c2], 0);
        Assert.Null(err);
        Assert.Equal("RN  ", c1.Data.UsedPhase);
        Assert.Equal("SN  ", c2.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P4Wv0_親3P4W4で想定外の子種別は設計エラー6を通知する()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.SystemNumber = "001";
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '1', '3', '3'); // 想定外
        child.Data.SystemNumber = "001";
        int? reported = null;
        PhaseAssigner.AssignPhase3P4WNoV2([p, child], 0, n => reported = n);
        Assert.Equal(6, reported);
    }

    [Fact]
    public void 電源3P4Wv0_親1P2W2の子1P2W2は親の使用相をコピーする()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.SystemNumber = "001";
        var parent = Row(datano: "002", oyatno: "005", yoyaku: "MCB     ", siyouso: "RS  ");
        SetKpa(parent, '1', '2', '2');
        parent.Data.SystemNumber = "001";
        var child = Row(datano: "003", oyatno: "002", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '1', '2', '2');
        child.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P4WNoV2([p, parent, child], 0);
        Assert.Null(err);
        Assert.Equal("RS  ", child.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P4Wv0_親1P2W2で想定外の子種別は設計エラー8を通知する()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.SystemNumber = "001";
        var parent = Row(datano: "002", oyatno: "005", yoyaku: "MCB     ", siyouso: "RS  ");
        SetKpa(parent, '1', '2', '2');
        parent.Data.SystemNumber = "001";
        var child = Row(datano: "003", oyatno: "002", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '3', '4', '4'); // 想定外
        child.Data.SystemNumber = "001";
        int? reported = null;
        PhaseAssigner.AssignPhase3P4WNoV2([p, parent, child], 0, n => reported = n);
        Assert.Equal(8, reported);
    }

    // ── AssignPhase3P4WWithV2 ────────────────────────────────────────────────

    [Fact]
    public void 電源3P4Wv2_親3P4W4の子3P3W3はRSTにする()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ", kpav0: "210");
        SetKpa(p, '3', '4', '4');
        p.Data.CircuitVoltage[2] = "210"; // v2あり
        p.Data.SystemNumber = "001";
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '3', '3', '3');
        child.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P4WWithV2([p, child], 0);
        Assert.Null(err);
        Assert.Equal("RSTN", p.Data.UsedPhase);
        Assert.Equal("RST ", child.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P4Wv2_親3P4W4の子1P3W3はRNSにする()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.CircuitVoltage[2] = "210";
        p.Data.SystemNumber = "001";
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '1', '3', '3');
        child.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P4WWithV2([p, child], 0);
        Assert.Null(err);
        Assert.Equal("RNS ", child.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P4Wv2_親3P4W4で想定外の子種別は設計エラー9を通知する()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.CircuitVoltage[2] = "210";
        p.Data.SystemNumber = "001";
        var child = Row(datano: "002", oyatno: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '1', '2', '2'); // 想定外
        child.Data.SystemNumber = "001";
        int? reported = null;
        PhaseAssigner.AssignPhase3P4WWithV2([p, child], 0, n => reported = n);
        Assert.Equal(9, reported);
    }

    [Fact]
    public void 電源3P4Wv2_親1P3W3の子1P2W2はRSにする()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.CircuitVoltage[2] = "210";
        p.Data.SystemNumber = "001";
        var parent = Row(datano: "002", oyatno: "005", yoyaku: "MCB     ", siyouso: "XNY ");
        SetKpa(parent, '1', '3', '3');
        parent.Data.SystemNumber = "001";
        var child = Row(datano: "003", oyatno: "002", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '1', '2', '2');
        child.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P4WWithV2([p, parent, child], 0);
        Assert.Null(err);
        Assert.Equal("RS  ", child.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P4Wv2_親1P3W3の子1P2W1はRNSNを交互設定する()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.CircuitVoltage[2] = "210";
        p.Data.SystemNumber = "001";
        var parent = Row(datano: "002", oyatno: "005", yoyaku: "MCB     ", siyouso: "XNY ");
        SetKpa(parent, '1', '3', '3');
        parent.Data.SystemNumber = "001";
        var c1 = Row(datano: "003", oyatno: "002", heino: "001", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(c1, '1', '2', '1');
        c1.Data.SystemNumber = "001";
        var c2 = Row(datano: "004", oyatno: "002", heino: "002", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(c2, '1', '2', '1');
        c2.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P4WWithV2([p, parent, c1, c2], 0);
        Assert.Null(err);
        Assert.Equal("RN  ", c1.Data.UsedPhase);
        Assert.Equal("SN  ", c2.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P4Wv2_親1P2W1の子1P2W1は親の使用相をコピーする()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.CircuitVoltage[2] = "210";
        p.Data.SystemNumber = "001";
        var parent = Row(datano: "002", oyatno: "005", yoyaku: "MCB     ", siyouso: "RN  ");
        SetKpa(parent, '1', '2', '1');
        parent.Data.SystemNumber = "001";
        var child = Row(datano: "003", oyatno: "002", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '1', '2', '1');
        child.Data.SystemNumber = "001";
        var err = PhaseAssigner.AssignPhase3P4WWithV2([p, parent, child], 0);
        Assert.Null(err);
        Assert.Equal("RN  ", child.Data.UsedPhase);
    }

    [Fact]
    public void 電源3P4Wv2_親1P3W3で想定外の子種別は設計エラー12を通知する()
    {
        var p = Row(datano: "001", yoyaku: "P       ", siyouso: "    ");
        SetKpa(p, '3', '4', '4');
        p.Data.CircuitVoltage[2] = "210";
        p.Data.SystemNumber = "001";
        var parent = Row(datano: "002", oyatno: "005", yoyaku: "MCB     ", siyouso: "XNY ");
        SetKpa(parent, '1', '3', '3');
        parent.Data.SystemNumber = "001";
        var child = Row(datano: "003", oyatno: "002", yoyaku: "MCB     ", siyouso: "    ");
        SetKpa(child, '3', '4', '4'); // 想定外
        child.Data.SystemNumber = "001";
        int? reported = null;
        PhaseAssigner.AssignPhase3P4WWithV2([p, parent, child], 0, n => reported = n);
        Assert.Equal(12, reported);
    }
}


