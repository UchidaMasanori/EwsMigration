using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="SecondaryParameterSetter"/>(【C原典】Fyss14.c SetParam_ep2_* 群)の単体テスト。
/// 回路電気値(kpa*)から ep[2] を決定する決定的処理を検証する。
/// </summary>
public sealed class SecondaryParameterSetterTests
{
    private static MainCircuitData NewData() => new();

    private static MainCircuitResult Res(string seq, MainCircuitData d) =>
        new() { SequenceNumber = seq, Data = d };

    // ---- SetParam_ep2_MCB_P -------------------------------------------------

    [Theory]
    [InlineData('1', "002")] // 回路極数 '1' → 3桁目 '2'
    [InlineData('2', "002")] // それ以外は回路極数そのまま(2→3桁目'2')
    [InlineData('3', "003")]
    public void SetMcbPoleは回路極数から極数3桁目を決定する(char pole, string expectedP)
    {
        MainCircuitData data = NewData();
        data.CircuitPoleCount = pole;

        SecondaryParameterSetter.SetMcbPole(data);

        Assert.Equal(expectedP, data.ElectricalParameterSlots[2].P);
    }

    // ---- SetParam_ep2_MCB_E -------------------------------------------------

    [Theory]
    [InlineData('1', '2', '1', "1")]
    [InlineData('1', '2', '2', "2")]
    [InlineData('1', '3', '0', "2")]
    [InlineData('3', '3', '0', "3")]
    [InlineData('3', '4', '0', "3")]
    [InlineData('0', '0', '0', "2")]
    public void SetMcbElementは相線式極からエレメント数を決定する(char ph, char wr, char p, string expectedE)
    {
        MainCircuitData data = NewData();
        data.CircuitPhaseCount = ph;
        data.CircuitWireType = wr;
        data.CircuitPoleCount = p;

        SecondaryParameterSetter.SetMcbElement(data);

        Assert.Equal(expectedE, data.ElectricalParameterSlots[2].E);
    }

    [Fact]
    public void SetMcbElementはep0のATが99999_999なら0にする()
    {
        MainCircuitData data = NewData();
        data.CircuitPhaseCount = '1';
        data.CircuitWireType = '2';
        data.CircuitPoleCount = '1'; // 通常なら "1" になる条件
        data.ElectricalParameterSlots[0].At = "99999.999";

        SecondaryParameterSetter.SetMcbElement(data);

        Assert.Equal("0", data.ElectricalParameterSlots[2].E);
    }

    // ---- SetParam_ep2_MCB_V2 ------------------------------------------------

    [Fact]
    public void SetMcbVoltage2は最大回路電圧を電圧2へ格納する()
    {
        MainCircuitData data = NewData();
        data.CircuitVoltage = ["100", "200", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetMcbVoltage2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        // epav2[0] の 4 桁目以降 3 桁へ最大電圧 "200"
        Assert.Equal("00020000", ep2.V2[0]);
        Assert.Equal("000000.0", ep2.V2[1]);
        Assert.Equal("000000.0", ep2.V2[2]);
        Assert.Equal('A', ep2.V2Kbn);
    }

    // ---- SetParam_ep2_MC_P --------------------------------------------------

    [Theory]
    [InlineData("200", "002")] // 105超 → '2'
    [InlineData("105", "001")] // 105以下 → '1'
    [InlineData("100", "001")]
    public void SetMcPoleは回路電圧0の105境界で極数を決定する(string v0, string expectedP)
    {
        MainCircuitData data = NewData();
        data.CircuitVoltage = [v0, "000", "000"];

        SecondaryParameterSetter.SetMcPole(data);

        Assert.Equal(expectedP, data.ElectricalParameterSlots[2].P);
    }

    // ---- SetParam_ep2_MG_* --------------------------------------------------

    [Fact]
    public void SetMgElementは常に2()
    {
        MainCircuitData data = NewData();
        SecondaryParameterSetter.SetMgElement(data);
        Assert.Equal("2", data.ElectricalParameterSlots[2].E);
    }

    [Fact]
    public void SetMgContactAとBは00にする()
    {
        MainCircuitData data = NewData();
        data.ElectricalParameterSlots[2].Ac = "99";
        data.ElectricalParameterSlots[2].Bc = "99";

        SecondaryParameterSetter.SetMgContactA(data);
        SecondaryParameterSetter.SetMgContactB(data);

        Assert.Equal("00", data.ElectricalParameterSlots[2].Ac);
        Assert.Equal("00", data.ElectricalParameterSlots[2].Bc);
    }

    // ---- SetParam_ep2_TS_* --------------------------------------------------

    [Fact]
    public void SetTsControlVoltageは最大回路電圧と区分を制御電圧へ設定する()
    {
        MainCircuitData data = NewData();
        data.CircuitVoltage = ["100", "210", "000"];
        data.CircuitVoltageKind = 'D';

        SecondaryParameterSetter.SetTsControlVoltage(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("210", ep2.Vc);
        Assert.Equal('D', ep2.VcKbn);
    }

    [Fact]
    public void SetTsContactAとBは00にする()
    {
        MainCircuitData data = NewData();
        data.ElectricalParameterSlots[2].Ac = "99";
        data.ElectricalParameterSlots[2].Bc = "99";

        SecondaryParameterSetter.SetTsContactA(data);
        SecondaryParameterSetter.SetTsContactB(data);

        Assert.Equal("00", data.ElectricalParameterSlots[2].Ac);
        Assert.Equal("00", data.ElectricalParameterSlots[2].Bc);
    }

    // ---- 転送メソッド(MC_V2/MG_V2/TS_V2 = MCB_V2) ---------------------------

    [Fact]
    public void MC_MG_TSのVoltage2はMCB_V2と同一結果になる()
    {
        MainCircuitData baseData = NewData();
        baseData.CircuitVoltage = ["220", "100", "000"];
        baseData.CircuitVoltageKind = 'A';

        MainCircuitData mcb = Clone(baseData);
        MainCircuitData mc = Clone(baseData);
        MainCircuitData mg = Clone(baseData);
        MainCircuitData ts = Clone(baseData);

        SecondaryParameterSetter.SetMcbVoltage2(mcb);
        SecondaryParameterSetter.SetMcVoltage2(mc);
        SecondaryParameterSetter.SetMgVoltage2(mg);
        SecondaryParameterSetter.SetTsVoltage2(ts);

        string expected = mcb.ElectricalParameterSlots[2].V2[0];
        Assert.Equal("00022000", expected); // 最大 "220" が [3..6) へ
        Assert.Equal(expected, mc.ElectricalParameterSlots[2].V2[0]);
        Assert.Equal(expected, mg.ElectricalParameterSlots[2].V2[0]);
        Assert.Equal(expected, ts.ElectricalParameterSlots[2].V2[0]);
    }

    private static MainCircuitData Clone(MainCircuitData src)
    {
        return new MainCircuitData
        {
            CircuitPhaseCount = src.CircuitPhaseCount,
            CircuitWireType = src.CircuitWireType,
            CircuitPoleCount = src.CircuitPoleCount,
            CircuitVoltage = [.. src.CircuitVoltage],
            CircuitVoltageKind = src.CircuitVoltageKind,
        };
    }

    // ---- SetParam_ep2 ディスパッチャ -----------------------------------------

    [Fact]
    public void SetParam_ep2_MCBは極数_エレメント_電圧を設定する()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "MCB";
        data.CircuitPhaseCount = '1';
        data.CircuitWireType = '2';
        data.CircuitPoleCount = '2';
        data.CircuitVoltage = ["105", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("002", ep2.P);        // kpap='2' → 3桁目 '2'
        Assert.Equal("2", ep2.E);          // 1P2W(p≠1) → 2
        Assert.Equal("000105.0", ep2.V2[0]);
        Assert.Equal("000000.0", ep2.V2[1]);
        Assert.Equal('A', ep2.V2Kbn);
    }

    [Fact]
    public void SetParam_ep2_SBは極数2とエレメントを設定する()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "SB";
        data.CircuitPoleCount = '1';
        data.CircuitVoltage = ["105", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("002", ep2.P); // epap[2]='2'
        Assert.Equal("1", ep2.E);   // kpap=='1' → '1'
        Assert.Equal("000105.0", ep2.V2[0]);
    }

    [Fact]
    public void SetParam_ep2_RRYは極数を回路極数そのままにしV2を設定する()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "RRY";
        data.CircuitPoleCount = '3';
        data.CircuitVoltage = ["210", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("003", ep2.P); // epap[2]=kpap='3'
        Assert.Equal("000210.0", ep2.V2[0]);
    }

    [Fact]
    public void SetParam_ep2_MGは極数_エレメント2_接点_電圧を設定する()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "MG";
        data.CircuitPhaseCount = '3';
        data.CircuitWireType = '3';
        data.CircuitPoleCount = '3';
        data.CircuitVoltage = ["210", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("003", ep2.P);  // SetMcbPole: kpap='3' → '3'
        Assert.Equal("2", ep2.E);    // MG_E 常に '2'
        Assert.Equal("00", ep2.Ac);
        Assert.Equal("00", ep2.Bc);
        Assert.Equal("000210.0", ep2.V2[0]);
    }

    [Fact]
    public void SetParam_ep2_MCは電圧2と接点AC_BCを設定し極数は初期化のまま()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "MC";
        data.CircuitVoltage = ["210", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        // 【C原典】MC_V2=MCB_V2(回路電圧最大値)。AC/BC は非INVBP経路 "00"。
        Assert.Equal("000210.0", ep2.V2[0]);
        Assert.Equal("000000.0", ep2.V2[1]);
        Assert.Equal('A', ep2.V2Kbn);
        Assert.Equal("00", ep2.Ac);
        Assert.Equal("00", ep2.Bc);
        // epap(2次側検出)は記録列依存でディスパッチャ未設定 → 初期化のまま。
        Assert.Equal("000", ep2.P);
    }

    [Theory]
    [InlineData("105", "", "000150.0")] // VT無・105V以下・datatype[1]非VS → 150V
    [InlineData("210", "", "000300.0")] // VT無・105<v<=220 → 300V
    [InlineData("105", "VS", "000300.0")] // VT無・105V以下・datatype[1]=VS(改訂<25>) → 300V
    public void SetParam_ep2_VMのV2はkiryoso3で回路電圧に応じ設定される(string kpav0, string dt1, string expectedV2)
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "VM";
        data.CircuitElement = '3';     // 計器用回路(VT無)
        data.CircuitVoltage = [kpav0, "000", "000"];
        data.CircuitVoltageKind = 'A';
        data.DataType = ["", dt1, "", "", "", "", ""];

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal(expectedV2, ep2.V2[0]);
        Assert.Equal("000000.0", ep2.V1[0]); // kiryoso=='3' は V1=0
        Assert.Equal('A', ep2.V2Kbn);
    }

    [Fact]
    public void SetParam_ep2_VMのkiryoso4はV1を1次電圧で_V2を150Vに設定する()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "VM";
        data.CircuitElement = '4';     // 計器用回路(VT付)
        data.MeterPrimaryVoltage = "220";
        data.CircuitVoltage = ["440", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("000300.0", ep2.V1[0]); // kpakv1=220(<=220) → 300V
        Assert.Equal("000150.0", ep2.V2[0]); // VT付 → 150V
        Assert.Equal('A', ep2.V2Kbn);
    }

    [Fact]
    public void SetParam_ep2_VMのkiryoso4は1次電圧220超でV1を600Vに設定する()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "VM";
        data.CircuitElement = '4';
        data.MeterPrimaryVoltage = "440";
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        Assert.Equal("000600.0", data.ElectricalParameterSlots[2].V1[0]);
    }

    [Theory]
    [InlineData("HPSB")]
    [InlineData("HSB")]
    public void SetParam_ep2_HPSB_HSBは極数と電圧を設定する(string yoyaku)
    {
        MainCircuitData data = NewData();
        data.ReservedWord = yoyaku;
        data.CircuitPoleCount = '3';
        data.CircuitVoltage = ["210", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("003", ep2.P);          // SetMcbPole: kpap='3' → 3桁目 '3'
        Assert.Equal("000210.0", ep2.V2[0]); // MCB_V2: 回路電圧最大値
        Assert.Equal('A', ep2.V2Kbn);
    }

    [Fact]
    public void SetParam_ep2_TSは電圧2と制御電圧_接点AC_BC_CCを設定する()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "TS";
        data.CircuitVoltage = ["100", "210", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("000210.0", ep2.V2[0]); // TS_V2=MCB_V2: 回路電圧最大値
        Assert.Equal('A', ep2.V2Kbn);
        Assert.Equal("210", ep2.Vc);          // TS_VC: 回路電圧最大値(3桁)
        Assert.Equal('A', ep2.VcKbn);
        Assert.Equal("00", ep2.Ac);           // TS_AC: 常に "00"
        Assert.Equal("00", ep2.Bc);           // TS_BC: 常に "00"
        Assert.Equal("01", ep2.Cc);           // TS_CC: 常に "01"
        Assert.Equal("000", ep2.P);           // 極数は初期化のまま
    }

    [Fact]
    public void SetParam_ep2_Lは相線式を設定しSP枠区分を1にする()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "L";
        data.CircuitPhaseCount = '1';
        data.CircuitWireType = '2';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("1", ep2.Ph2[0]);
        Assert.Equal("0", ep2.Ph2[1]);
        Assert.Equal("2", ep2.Wr2[0]);
        Assert.Equal("0", ep2.Wr2[1]);
        Assert.Equal('1', data.AttachedParameter.SpFutureMountKind); // リミッターは常にSP枠扱い
    }

    [Theory]
    [InlineData("MCFR")]
    public void SetParam_ep2_MCFRは電圧2と接点AC_BCを設定する(string yoyaku)
    {
        MainCircuitData data = NewData();
        data.ReservedWord = yoyaku;
        data.CircuitVoltage = ["210", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("000210.0", ep2.V2[0]); // MC_V2=MCB_V2
        Assert.Equal("00", ep2.Ac);
        Assert.Equal("00", ep2.Bc);
        Assert.Equal("000", ep2.P);          // 極数は初期化のまま
    }

    [Theory]
    [InlineData("MGFR")]
    [InlineData("MGSD")]
    [InlineData("MGFRSD")]
    public void SetParam_ep2_MG派生はエレメントと電圧2を設定する(string yoyaku)
    {
        MainCircuitData data = NewData();
        data.ReservedWord = yoyaku;
        data.CircuitVoltage = ["210", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("2", ep2.E);            // MG_E: 常に '2'
        Assert.Equal("000210.0", ep2.V2[0]); // MG_V2=MCB_V2
    }

    [Theory]
    [InlineData("DCSIR")]
    [InlineData("DCNI")]
    public void SetParam_ep2_DC系は電圧2を設定し区分を直流Dにする(string yoyaku)
    {
        MainCircuitData data = NewData();
        data.ReservedWord = yoyaku;
        data.CircuitVoltage = ["100", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("000100.0", ep2.V2[0]);
        Assert.Equal('D', ep2.V2Kbn);        // 直流区分に上書き
    }

    [Theory]
    [InlineData("TSU")]
    [InlineData("SSWU")]
    [InlineData("PBSU")]
    [InlineData("COSU")]
    [InlineData("2COSU")]
    [InlineData("OLU")]
    public void SetParam_ep2_TSU系は電圧2と制御電圧を設定する(string yoyaku)
    {
        MainCircuitData data = NewData();
        data.ReservedWord = yoyaku;
        data.CircuitVoltage = ["100", "210", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("000210.0", ep2.V2[0]); // TS_V2=MCB_V2
        Assert.Equal("210", ep2.Vc);          // TS_VC
        Assert.Equal('A', ep2.VcKbn);
    }

    [Fact]
    public void SetParam_ep2_SCは相2桁_周波数_電圧を設定し極数は初期化のまま()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "SC";
        data.CircuitPhaseCount = '3';
        data.CircuitFrequency = "60";
        data.CircuitVoltage = ["210", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("3", ep2.Ph2[0]);
        Assert.Equal("0", ep2.Ph2[1]);
        Assert.Equal("60", ep2.Hz);
        Assert.Equal("000210.0", ep2.V2[0]);
        Assert.Equal("000", ep2.P); // 未設定(初期化のまま)
    }

    [Fact]
    public void SetParam_ep2_Fは電圧のみ設定し極数は初期化のまま()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "F";
        data.CircuitVoltage = ["210", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("000210.0", ep2.V2[0]);
        Assert.Equal("000", ep2.P); // 初期化のまま
    }

    [Fact]
    public void SetParam_ep2_VSは相2桁と線式2桁を設定する()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "VS";
        data.CircuitPhaseCount = '3';
        data.CircuitWireType = '3';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("3", ep2.Ph2[0]);
        Assert.Equal("0", ep2.Ph2[1]);
        Assert.Equal("3", ep2.Wr2[0]);
        Assert.Equal("0", ep2.Wr2[1]);
    }

    [Theory]
    [InlineData('1', '2', '3')] // 1P2W → 個数3
    [InlineData('1', '3', '4')] // 1P3W → 個数4
    [InlineData('3', '3', '6')] // 3相 → 個数6
    public void SetParam_ep2_LAは相線式から個数を設定しV2も設定する(char ph, char wr, char expectedQty)
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "LA";
        data.CircuitPhaseCount = ph;
        data.CircuitWireType = wr;
        data.CircuitVoltage = ["210", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal(expectedQty, ep2.Qty);
        Assert.Equal(ph.ToString(), ep2.Ph2[0]);
        Assert.Equal("000210.0", ep2.V2[0]);
    }

    [Fact]
    public void SetParam_ep2_LAはdatatype0がCTなら個数を設定しない()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "LA";
        data.CircuitPhaseCount = '1';
        data.CircuitWireType = '2';
        data.DataType[0] = "CT";
        data.CircuitVoltage = ["210", "000", "000"];

        SecondaryParameterSetter.SetParam_ep2(data);

        Assert.Equal('0', data.ElectricalParameterSlots[2].Qty); // CT なので個数未設定(既定'0')
    }

    [Fact]
    public void SetParam_ep2_CONは相線式から極数3桁目とV2を設定する()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "CON";
        data.CircuitPhaseCount = '3';
        data.CircuitWireType = '3';
        data.CircuitVoltage = ["210", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("003", ep2.P); // 3P3W → 3桁目 '3'
        Assert.Equal("000210.0", ep2.V2[0]);
    }

    // ---- SetParam_ep2 NHMB --------------------------------------------------

    [Fact]
    public void SetParam_ep2_NHMBはW入力があればATをW割るV2で算出する()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "NHMB";
        data.CircuitPoleCount = '2';
        data.CircuitVoltage = ["100", "000", "000"];
        data.ElectricalParameterSlots[0].W1 = "0002000.00"; // 2000W
        data.ElectricalParameterSlots[0].V2[0] = "000100.0"; // 100V

        SecondaryParameterSetter.SetParam_ep2(data);

        Assert.Equal("00020.000", data.ElectricalParameterSlots[2].At); // 2000/100=20
    }

    [Fact]
    public void SetParam_ep2_NHMBはW入力時V2が0ならep2側V2を分母に使う()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "NHMB";
        data.CircuitPoleCount = '2';
        data.CircuitVoltage = ["200", "000", "000"];
        data.AttachedParameter.LoadCapacity = "0004000"; // 4000W
        // ep[0].V2[0] は既定 "000000.0"(=0)のまま。ep[2].V2[0] は MCB_V2 が回路電圧から設定。

        SecondaryParameterSetter.SetParam_ep2(data);

        // MCB_V2 で ep[2].V2[0]="000200.0"(200V) → 4000/200=20
        Assert.Equal("000200.0", data.ElectricalParameterSlots[2].V2[0]);
        Assert.Equal("00020.000", data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void SetParam_ep2_NHMBはW無_A2入力ならep0のATにA2を設定する()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "NHMB";
        data.CircuitPoleCount = '2';
        data.CircuitVoltage = ["100", "000", "000"];
        data.ElectricalParameterSlots[0].A2 = "00050.000"; // 50A

        SecondaryParameterSetter.SetParam_ep2(data);

        Assert.Equal("00050.000", data.ElectricalParameterSlots[0].At); // ep[0] 側に設定
        Assert.Equal("000000000", data.ElectricalParameterSlots[2].At); // ep[2] は未変更(既定)
    }

    [Fact]
    public void SetParam_ep2_NHMBはW無_A2無ならATを設定しない()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "NHMB";
        data.CircuitPoleCount = '2';
        data.CircuitVoltage = ["100", "000", "000"];

        SecondaryParameterSetter.SetParam_ep2(data);

        Assert.Equal("000000000", data.ElectricalParameterSlots[0].At);
        Assert.Equal("000000000", data.ElectricalParameterSlots[2].At);
    }

    // ---- SetParam_ep2 CR ----------------------------------------------------

    [Theory]
    [InlineData('3')]
    [InlineData('4')]
    [InlineData('5')]
    public void SetParam_ep2_CRは27ABCなら制御電圧_接点_タイプ_極数を設定する(char tokkbn)
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "CR";
        data.SpecialReservedWordKind = tokkbn;
        data.CircuitPoleCount = '2';
        data.CircuitVoltage = ["210", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("210", ep2.Vc);
        Assert.Equal('A', ep2.VcKbn);
        Assert.Equal("02", data.ElectricalParameterSlots[0].Cc);
        Assert.Equal("02", ep2.Cc);
        Assert.Equal("NC     ", data.DataType[2]);
        Assert.Equal("002", ep2.P); // 極数3桁目 '2'
    }

    [Fact]
    public void SetParam_ep2_CRは27ABC以外なら何も設定しない()
    {
        MainCircuitData data = NewData();
        data.ReservedWord = "CR";
        data.SpecialReservedWordKind = '0';
        data.CircuitVoltage = ["210", "000", "000"];
        data.CircuitVoltageKind = 'A';

        SecondaryParameterSetter.SetParam_ep2(data);

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        Assert.Equal("000", ep2.Vc); // 既定のまま
        Assert.Equal("00", ep2.Cc);
        Assert.Equal("", data.DataType[2]);
    }

    // ---- SetParam_ep2 DCPW (list+index) -------------------------------------

    [Fact]
    public void SetParam_ep2_DCPWは親のV2をV1へ複写しW入力からA2を算出する()
    {
        MainCircuitData parent = NewData();
        parent.ElectricalParameterSlots[2].V2[0] = "000200.0";

        MainCircuitData dcpw = NewData();
        dcpw.ReservedWord = "DCPW";
        dcpw.ParentSequenceNumber = "001";
        dcpw.CircuitVoltage = ["100", "000", "000"];
        dcpw.ElectricalParameterSlots[0].W1 = "0002000.00"; // 2000W

        MainCircuitResult[] maina = [Res("001", parent), Res("002", dcpw)];

        SecondaryParameterSetter.SetParam_ep2(maina, 1);

        ElectricalParameters ep2 = dcpw.ElectricalParameterSlots[2];
        Assert.Equal("000200.0", ep2.V1[0]); // 親の V2 を複写
        Assert.Equal("00020.000", ep2.A2);   // 2000/100=20
        Assert.Equal('D', ep2.V2Kbn);
    }

    [Fact]
    public void SetParam_ep2_DCPWはW無ならA2を変更せずV2区分をDにする()
    {
        MainCircuitData parent = NewData();
        parent.ElectricalParameterSlots[2].V2[0] = "000100.0";

        MainCircuitData dcpw = NewData();
        dcpw.ReservedWord = "DCPW";
        dcpw.ParentSequenceNumber = "001";
        dcpw.CircuitVoltage = ["100", "000", "000"];

        MainCircuitResult[] maina = [Res("001", parent), Res("002", dcpw)];

        SecondaryParameterSetter.SetParam_ep2(maina, 1);

        ElectricalParameters ep2 = dcpw.ElectricalParameterSlots[2];
        Assert.Equal(new ElectricalParameters().A2, ep2.A2); // 未変更(既定)
        Assert.Equal("000100.0", ep2.V1[0]);
        Assert.Equal('D', ep2.V2Kbn);
    }

    // ---- SetParam_ep2 ELR (list+index) --------------------------------------

    [Fact]
    public void SetParam_ep2_ELRは直前が非ZCTならVCを設定し同一ysnoのELRへ伝播する()
    {
        MainCircuitData mcb = NewData();
        mcb.ReservedWord = "MCB";
        mcb.CircuitVoltage = ["210", "000", "000"];
        mcb.CircuitVoltageKind = 'A';

        MainCircuitData elr1 = NewData();
        elr1.ReservedWord = "ELR";
        elr1.DesignationNumber = "01";

        MainCircuitData zct = NewData();
        zct.ReservedWord = "ZCT";

        MainCircuitData elr2 = NewData();
        elr2.ReservedWord = "ELR";
        elr2.DesignationNumber = "01";

        MainCircuitResult[] maina =
            [Res("001", mcb), Res("002", elr1), Res("003", zct), Res("004", elr2)];

        SecondaryParameterSetter.SetParam_ep2(maina, 1);

        Assert.Equal("210", elr1.ElectricalParameterSlots[2].Vc);
        Assert.Equal('A', elr1.ElectricalParameterSlots[2].VcKbn);
        Assert.Equal("210", elr2.ElectricalParameterSlots[2].Vc); // 同一ysnoへ伝播
        Assert.Equal('A', elr2.ElectricalParameterSlots[2].VcKbn);
    }

    [Fact]
    public void SetParam_ep2_ELRは直前がZCTなら何も設定しない()
    {
        MainCircuitData zct = NewData();
        zct.ReservedWord = "ZCT";

        MainCircuitData elr = NewData();
        elr.ReservedWord = "ELR";
        elr.DesignationNumber = "01";

        MainCircuitResult[] maina = [Res("001", zct), Res("002", elr)];

        SecondaryParameterSetter.SetParam_ep2(maina, 1);

        Assert.Equal("000", elr.ElectricalParameterSlots[2].Vc); // 既定のまま
    }

    [Fact]
    public void SetParam_ep2_ELRはysnoが異なるELRには伝播しない()
    {
        MainCircuitData mcb = NewData();
        mcb.ReservedWord = "MCB";
        mcb.CircuitVoltage = ["210", "000", "000"];
        mcb.CircuitVoltageKind = 'A';

        MainCircuitData elr1 = NewData();
        elr1.ReservedWord = "ELR";
        elr1.DesignationNumber = "01";

        MainCircuitData zct = NewData();
        zct.ReservedWord = "ZCT";

        MainCircuitData elr2 = NewData();
        elr2.ReservedWord = "ELR";
        elr2.DesignationNumber = "02"; // 異なる ysno

        MainCircuitResult[] maina =
            [Res("001", mcb), Res("002", elr1), Res("003", zct), Res("004", elr2)];

        SecondaryParameterSetter.SetParam_ep2(maina, 1);

        Assert.Equal("000", elr2.ElectricalParameterSlots[2].Vc); // 未伝播
    }

    [Fact]
    public void SetParam_ep2_listオーバーロードは対象外予約語を単一レコード版へ委譲する()
    {
        MainCircuitData mcb = NewData();
        mcb.ReservedWord = "MCB";
        mcb.CircuitPoleCount = '2';
        mcb.CircuitVoltage = ["100", "000", "000"];

        MainCircuitResult[] maina = [Res("001", mcb)];

        SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Equal("002", mcb.ElectricalParameterSlots[2].P); // MCB_P が実行された
    }

    // ---- SetParam_ep2 LGR (list+index) --------------------------------------

    private static (MainCircuitResult[] maina, MainCircuitData primary) BuildLgr(int siblingCount)
    {
        MainCircuitData mcb = NewData();
        mcb.ReservedWord = "MCB";
        mcb.CircuitVoltage = ["210", "000", "000"];
        mcb.CircuitVoltageKind = 'A';

        MainCircuitData lgr1 = NewData(); // 直前が非ZCTの主LGR
        lgr1.ReservedWord = "LGR";
        lgr1.DesignationNumber = "01";
        lgr1.DescriptionRow = "012";
        lgr1.DescriptionColumn = "034";

        var list = new List<MainCircuitResult> { Res("001", mcb), Res("002", lgr1) };

        // 直前が ZCT の同一 ysno LGR を siblingCount 個追加する。
        for (int k = 0; k < siblingCount; k++)
        {
            MainCircuitData zct = NewData();
            zct.ReservedWord = "ZCT";
            MainCircuitData lgr = NewData();
            lgr.ReservedWord = "LGR";
            lgr.DesignationNumber = "01";
            list.Add(Res("100", zct));
            list.Add(Res("101", lgr));
        }

        return ([.. list], lgr1);
    }

    [Theory]
    [InlineData(1, "001")]
    [InlineData(2, "002")]
    [InlineData(3, "005")]
    [InlineData(5, "005")]
    public void SetParam_ep2_LGRは同一ysno兄弟数でKを決めVCを伝播する(int siblings, string expectedK)
    {
        (MainCircuitResult[] maina, MainCircuitData primary) = BuildLgr(siblings);

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 1);

        Assert.Null(error);
        Assert.Equal(expectedK, primary.ElectricalParameterSlots[2].K);
        Assert.Equal("210", primary.ElectricalParameterSlots[2].Vc);
        Assert.Equal('A', primary.ElectricalParameterSlots[2].VcKbn);
        // 兄弟 LGR にも VC が伝播する。
        Assert.Equal("210", maina[3].Data.ElectricalParameterSlots[2].Vc);
    }

    [Theory]
    [InlineData(0)] // 兄弟なし → エラー
    [InlineData(6)] // 6 以上 → エラー
    public void SetParam_ep2_LGRは兄弟数が0または6以上でFY632Eを返す(int siblings)
    {
        (MainCircuitResult[] maina, MainCircuitData primary) = BuildLgr(siblings);

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 1);

        Assert.NotNull(error);
        Assert.Equal("FY-632E", error.ErrorCode);
        Assert.Equal(12, error.LineNumber);  // DescriptionRow "012"
        Assert.Equal(34, error.Column);      // DescriptionColumn "034"
    }

    [Fact]
    public void SetParam_ep2_LGRは直前がZCTなら何も設定せず正常を返す()
    {
        MainCircuitData zct = NewData();
        zct.ReservedWord = "ZCT";

        MainCircuitData lgr = NewData();
        lgr.ReservedWord = "LGR";
        lgr.DesignationNumber = "01";

        MainCircuitResult[] maina = [Res("001", zct), Res("002", lgr)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 1);

        Assert.Null(error);
        Assert.Equal("000", lgr.ElectricalParameterSlots[2].K); // 既定のまま
        Assert.Equal("000", lgr.ElectricalParameterSlots[2].Vc);
    }
}
