using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 負荷発生元の負荷容量決定(<see cref="LoadSourceSelector"/>)の移植検証。
/// 【C原典】set_fky/get_ep(toku/sekkei/src/Fyss31.c)。
/// </summary>
public sealed class LoadSourceSelectorTests
{
    private const int Precision = 9;

    private static MainCircuitResult Rec(string yoyaku, char kpaph = '0', string kpav = "000")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = "001",
            Data = new MainCircuitData
            {
                ReservedWord = yoyaku,
                CircuitPhaseCount = kpaph,
            },
        };
        r.Data.CircuitVoltage[0] = kpav;
        return r;
    }

    [Fact]
    public void ブレーカはATに係数を乗じて電流を求め予約語優先順位2を返す()
    {
        MainCircuitResult mcb = Rec("MCB");
        mcb.Data.ElectricalParameterSlots[0].At = "00050.000";

        int rc = LoadSourceSelector.SelectLoadCurrent([mcb], 0, 4, out int priority, out double current);

        Assert.Equal(LoadSourceSelector.Selected, rc);
        Assert.Equal(2, priority);
        Assert.Equal(40.0, current, Precision); // 0.8 × 50
    }

    [Fact]
    public void 候補の予約語優先順位がbestより低ければ2を返す()
    {
        MainCircuitResult mcb = Rec("MCB"); // 予約語優先順位 2
        mcb.Data.ElectricalParameterSlots[0].At = "00050.000";

        int rc = LoadSourceSelector.SelectLoadCurrent([mcb], 0, 1, out _, out _);

        Assert.Equal(LoadSourceSelector.LowerPriority, rc);
    }

    [Fact]
    public void MGはATゼロならWで電動機として電流化し優先順位1を返す()
    {
        MainCircuitResult mg = Rec("MG", kpaph: '3', kpav: "200");
        mg.Data.ElectricalParameterSlots[0].At = "00000.000";
        mg.Data.ElectricalParameterSlots[0].W1 = "0000050.00"; // 50W

        int rc = LoadSourceSelector.SelectLoadCurrent([mg], 0, 4, out int priority, out double current);

        EnergizingCurrentCalculator.TryCalculate(mg.Data, 50.0, "M ", out double expected);

        Assert.Equal(LoadSourceSelector.Selected, rc);
        Assert.Equal(1, priority);
        Assert.Equal(expected, current, Precision);
    }

    [Fact]
    public void VAは非電動機予約語で相数により電動機かヒーターを選ぶ()
    {
        MainCircuitResult tr = Rec("TR", kpaph: '1', kpav: "100"); // TR ep_pry {0,0,1,0,0}=VA
        tr.Data.ElectricalParameterSlots[0].Va = "0000500.00"; // 500VA

        int rc = LoadSourceSelector.SelectLoadCurrent([tr], 0, 4, out _, out double current);

        // TR は非電動機予約語 → 単相なのでヒーター("H ")として電流化。
        EnergizingCurrentCalculator.TryCalculate(tr.Data, 500.0, "H ", out double expected);

        Assert.Equal(LoadSourceSelector.Selected, rc);
        Assert.Equal(expected, current, Precision);
    }

    [Fact]
    public void MCはA2に係数を乗じて電流を求める()
    {
        MainCircuitResult mc = Rec("MC"); // MC ep_pry {0,0,0,0,1}
        mc.Data.ElectricalParameterSlots[0].A2 = "00020.000";

        int rc = LoadSourceSelector.SelectLoadCurrent([mc], 0, 4, out int priority, out double current);

        Assert.Equal(LoadSourceSelector.Selected, rc);
        Assert.Equal(2, priority);
        Assert.Equal(16.0, current, Precision); // 0.8 × 20
    }

    [Fact]
    public void ATがサーチ上限値ならフレーム電流AFを用いる()
    {
        MainCircuitResult mcb = Rec("MCB");
        mcb.Data.ElectricalParameterSlots[0].At = "99999.999";
        mcb.Data.ElectricalParameterSlots[0].Af = "00030.000";

        int rc = LoadSourceSelector.SelectLoadCurrent([mcb], 0, 4, out _, out double current);

        Assert.Equal(LoadSourceSelector.Selected, rc);
        Assert.Equal(24.0, current, Precision); // 0.8 × 30
    }

    [Fact]
    public void 電気パラメータが全てゼロなら値無しの1を返す()
    {
        MainCircuitResult mcb = Rec("MCB"); // ep_pry {1,0,0,0,0}=AT のみ
        mcb.Data.ElectricalParameterSlots[0].At = "00000.000";

        int rc = LoadSourceSelector.SelectLoadCurrent([mcb], 0, 4, out _, out _);

        Assert.Equal(LoadSourceSelector.NoValue, rc);
    }

    [Fact]
    public void 負荷容量表に無い予約語は3を返す()
    {
        MainCircuitResult x = Rec("XYZ");

        int rc = LoadSourceSelector.SelectLoadCurrent([x], 0, 4, out _, out _);

        Assert.Equal(LoadSourceSelector.NotInTable, rc);
    }

    // ---- Fyss31_FukaHassei_Set(SetLoadSource) 本体オーケストレータの検証 ----

    /// <summary>負荷発生元設定用の主回路データを組み立てる。</summary>
    private static MainCircuitResult Circuit(
        string datano,
        string yoyaku,
        char kiryoso = '1',
        char mattan = '1',
        string hei = "001",
        string kaiso = "001",
        string nyuse = "001",
        string oya = "000",
        string gyoglno = "001",
        string gyocd = "AA",
        string loadCapacity = "0000000",
        string loadKind = "H ",
        char kpaph = '1',
        string kpav = "100",
        char kairobun = ' ',
        string ysno = "00",
        string gyo = "000",
        string keta = "000")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ReservedWord = yoyaku,
                CircuitElement = kiryoso,
                TerminalKind = mattan,
                ParallelNumber = hei,
                HierarchyNumber = kaiso,
                IncomingNumber = nyuse,
                ParentSequenceNumber = oya,
                LineTypeGroupNumber = gyoglno,
                LineTypeCode = gyocd,
                CircuitPhaseCount = kpaph,
                CircuitClass = kairobun,
                DesignationNumber = ysno,
                DescriptionRow = gyo,
                DescriptionColumn = keta,
            },
        };
        r.Data.CircuitVoltage[0] = kpav;
        r.Data.AttachedParameter.LoadCapacity = loadCapacity;
        r.Data.AttachedParameter.LoadKind = loadKind;
        // 電気パラメータは eparm_set 整形後の「値無し」= 小数点付きゼロで初期化する
        // (既定の "000000000" は get_ep のゼロ判定 "00000.000" と一致せず値有り扱いになるため)。
        ElectricalParameters ep = r.Data.ElectricalParameterSlots[0];
        ep.At = "00000.000";
        ep.Af = "00000.000";
        ep.A1 = "00000.000";
        ep.A2 = "00000.000";
        ep.W1 = "0000000.00";
        ep.Va = "0000000.00";
        return r;
    }

    [Fact]
    public void 末端機器の負荷容量から通電電流値を求め負荷発生元区分を立てる()
    {
        MainCircuitResult mcb = Circuit("001", "MCB", loadCapacity: "0000050", loadKind: "H ", kpav: "100");

        CircuitParseError? err = LoadSourceSelector.SetLoadSource([mcb]);

        Assert.Null(err);
        Assert.Equal('1', mcb.Data.LoadSourceKind);
        Assert.Equal("00000.50", mcb.Data.EnergizingCurrent); // 50 / 100
    }

    [Fact]
    public void 負荷容量はあるが通電電流を算出できなければFY560Eエラーを返す()
    {
        MainCircuitResult mcb = Circuit("001", "MCB", loadCapacity: "0000050", loadKind: "ZZ", gyo: "004", keta: "011");

        CircuitParseError? err = LoadSourceSelector.SetLoadSource([mcb]);

        Assert.NotNull(err);
        Assert.Equal("FY-560E", err!.ErrorCode);
        Assert.Equal(4, err.LineNumber);
        Assert.Equal(11, err.Column);
        Assert.Equal("FYMEE80", err.MessageId);
    }

    [Fact]
    public void 負荷容量が無くても電気パラメータから負荷発生元を決定する()
    {
        MainCircuitResult mcb = Circuit("001", "MCB", loadCapacity: "0000000");
        mcb.Data.ElectricalParameterSlots[0].At = "00050.000"; // AT=50 → 0.8×50=40

        CircuitParseError? err = LoadSourceSelector.SetLoadSource([mcb]);

        Assert.Null(err);
        Assert.Equal('1', mcb.Data.LoadSourceKind);
        Assert.Equal("00040.00", mcb.Data.EnergizingCurrent);
    }

    [Fact]
    public void 上流の同一行種グループを遡って負荷発生元を決定し中間要素へ電流を伝播する()
    {
        // 親(MCB,AT=50) ← 子(MG,末端,パラメータ無)。子は上流遡りで親を負荷発生元とする。
        MainCircuitResult parent = Circuit("001", "MCB", kiryoso: '2', mattan: '2',
            hei: "002", kaiso: "002", nyuse: "002", gyoglno: "005");
        parent.Data.ElectricalParameterSlots[0].At = "00050.000";
        MainCircuitResult child = Circuit("002", "MG", oya: "001", gyoglno: "005");

        CircuitParseError? err = LoadSourceSelector.SetLoadSource([parent, child]);

        Assert.Null(err);
        Assert.Equal('1', parent.Data.LoadSourceKind);
        Assert.Equal("00040.00", parent.Data.EnergizingCurrent);
        Assert.Equal("00040.00", child.Data.EnergizingCurrent); // 中間要素へ伝播
    }

    [Fact]
    public void 親が無い末端機器で負荷発生元が見つからなければFY560Eエラーを返す()
    {
        MainCircuitResult mg = Circuit("001", "MG", oya: "000", gyo: "003", keta: "007");

        CircuitParseError? err = LoadSourceSelector.SetLoadSource([mg]);

        Assert.NotNull(err);
        Assert.Equal("FY-560E", err!.ErrorCode);
        Assert.Equal(3, err.LineNumber);
        Assert.Equal(7, err.Column);
    }

    [Fact]
    public void Fヒューズで制御電源番号がある場合は固定電流と再サーチフラグを立てる()
    {
        MainCircuitResult parent = Circuit("001", "MC", kiryoso: '2', mattan: '2', gyoglno: "001");
        MainCircuitResult fuse = Circuit("002", "F", oya: "001", gyoglno: "009");
        fuse.Data.AttachedParameter.ControlPowerNumber = "01";

        CircuitParseError? err = LoadSourceSelector.SetLoadSource([parent, fuse]);

        Assert.Null(err);
        Assert.Equal("00000.80", fuse.Data.EnergizingCurrent);
        Assert.Equal('1', fuse.Data.SearchAgainFlag);
    }

    [Fact]
    public void 一二型で予約語と指定番号が一致する両末端が負荷発生元エラーならFY560Eを返す()
    {
        MainCircuitResult parent = Circuit("001", "MC", kiryoso: '2', mattan: '2', gyoglno: "001");
        MainCircuitResult a = Circuit("002", "CSDT", oya: "001", gyoglno: "009", ysno: "01", hei: "002");
        MainCircuitResult b = Circuit("003", "CSDT", oya: "001", gyoglno: "009", ysno: "01", hei: "003");

        CircuitParseError? err = LoadSourceSelector.SetLoadSource([parent, a, b]);

        Assert.NotNull(err);
        Assert.Equal("FY-560E", err!.ErrorCode);
    }

    [Fact]
    public void 主幹機器は負荷容量から通電電流をセットする()
    {
        // 末端でない主幹(kairobun='M',kiryoso='1',mattan='0')。改訂<4>でセットされる。
        MainCircuitResult m = Circuit("001", "MCCB", mattan: '0', kairobun: 'M',
            loadCapacity: "0000100", loadKind: "H ", kpav: "100");

        CircuitParseError? err = LoadSourceSelector.SetLoadSource([m]);

        Assert.Null(err);
        Assert.Equal("00001.00", m.Data.EnergizingCurrent); // 100 / 100
    }

    [Fact]
    public void SC系統は注入されたデリゲートで通電電流値を算出する()
    {
        MainCircuitResult sc = Circuit("001", "SC");
        sc.Data.AttachedParameter.LoadName[1] = "0KW";
        int? called = null;

        CircuitParseError? err = LoadSourceSelector.SetLoadSource([sc], processSystemCircuit: i => called = i);

        Assert.Null(err);
        Assert.Equal(0, called);
    }

    [Fact]
    public void 主回路エリアがnullなら例外を投げる()
    {
        Assert.Throws<ArgumentNullException>(() => LoadSourceSelector.SetLoadSource(null!));
    }
}
