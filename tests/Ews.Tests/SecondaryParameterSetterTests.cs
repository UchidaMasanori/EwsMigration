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

    // ---- SetParam_ep2 PLTR (list+index) -------------------------------------

    [Theory]
    [InlineData("210", "00020000")] // kv0>105 → "200"
    [InlineData("105", "00010000")] // kv0<=105 → "100"
    [InlineData("400", "00040000")] // kv0>=380 かつ PLTR → "400"
    public void SetParam_ep2_PLTRは親の回路電圧で1次側電圧V1を決める(string kv, string expectedV1)
    {
        MainCircuitData parent = NewData();
        parent.ReservedWord = "MCB";
        parent.CircuitVoltage = [kv, "000", "000"];

        MainCircuitData pltr = NewData();
        pltr.ReservedWord = "PLTR";
        pltr.ParentSequenceNumber = "001";
        pltr.MeterPrimaryVoltageKind = 'B';

        MainCircuitResult[] maina = [Res("001", parent), Res("002", pltr)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 1);

        Assert.Null(error);
        Assert.Equal(expectedV1, pltr.ElectricalParameterSlots[2].V1[0]);
        Assert.Equal('B', pltr.ElectricalParameterSlots[2].VcKbn); // kpakv1kb 伝播
    }

    [Fact]
    public void SetParam_ep2_PLTRは親がRTRならその親の回路電圧を参照する()
    {
        MainCircuitData grand = NewData();
        grand.ReservedWord = "MCB";
        grand.CircuitVoltage = ["210", "000", "000"];

        MainCircuitData rtr = NewData();
        rtr.ReservedWord = "RTR";
        rtr.ParentSequenceNumber = "001";

        MainCircuitData pltr = NewData();
        pltr.ReservedWord = "PLTR";
        pltr.ParentSequenceNumber = "002";

        MainCircuitResult[] maina = [Res("001", grand), Res("002", rtr), Res("003", pltr)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 2);

        Assert.Null(error);
        Assert.Equal("00020000", pltr.ElectricalParameterSlots[2].V1[0]); // 祖父 kv0=210>105
    }

    // ---- SetParam_ep2 MC (list+index) ---------------------------------------

    private static MainCircuitData Mc(string ysno, string kv0)
    {
        MainCircuitData mc = NewData();
        mc.ReservedWord = "MC";
        mc.DesignationNumber = ysno;
        mc.IdentityNumber = "00"; // 同一機器認識番号の 2 次側探索をスキップ
        mc.CircuitVoltage = [kv0, "000", "000"];
        return mc;
    }

    [Fact]
    public void SetParam_ep2_MCはINVBPなら極数003固定()
    {
        MainCircuitData mc = Mc("01", "210");
        mc.SpecialReservedWordKind = '7';

        MainCircuitResult[] maina = [Res("001", mc)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Null(error);
        Assert.Equal("003", mc.ElectricalParameterSlots[2].P);
    }

    [Fact]
    public void SetParam_ep2_MCは2次側なしで同一ysnoのMC数から極数を決める()
    {
        MainCircuitData mc1 = Mc("01", "100");
        MainCircuitData mc2 = Mc("01", "100");

        MainCircuitResult[] maina = [Res("001", mc1), Res("002", mc2)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Null(error);
        Assert.Equal("002", mc1.ElectricalParameterSlots[2].P); // icnt=2
    }

    [Fact]
    public void SetParam_ep2_MCは2次側あり共用時にMC数集計で極数を決める()
    {
        MainCircuitData mc1 = Mc("01", "100");
        MainCircuitData child = NewData();
        child.ReservedWord = "MCB";
        child.ParentSequenceNumber = "001";
        MainCircuitData mc2 = Mc("01", "200");

        MainCircuitResult[] maina = [Res("001", mc1), Res("002", child), Res("003", mc2)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Null(error);
        Assert.Equal("003", mc1.ElectricalParameterSlots[2].P); // icnt100=1+icnt200*2=2 → 3
    }

    [Fact]
    public void SetParam_ep2_MCは2次側あり非共用ならMC極数を設定する()
    {
        MainCircuitData mc = Mc("01", "100");
        mc.DesignationSuffix = 'A'; // 共用しない
        MainCircuitData child = NewData();
        child.ReservedWord = "MCB";
        child.ParentSequenceNumber = "001";

        MainCircuitResult[] maina = [Res("001", mc), Res("002", child)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Null(error);
        Assert.Equal("001", mc.ElectricalParameterSlots[2].P); // SetMcPole 100<=105 → '1'
    }

    [Fact]
    public void SetParam_ep2_MCはTM行ありのM系で極数を2Pにする()
    {
        MainCircuitData tm = NewData();
        tm.ReservedWord = "MCB";
        tm.LineTypeCode = "TM";
        tm.SystemNumber = "001";

        MainCircuitData mc = NewData();
        mc.ReservedWord = "MC";
        mc.LineTypeCode = "M";
        mc.SystemNumber = "001";
        mc.CircuitPhaseCount = '1';
        mc.DataType[0] = "SF";

        MainCircuitResult[] maina = [Res("001", tm), Res("002", mc)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 1);

        Assert.Null(error);
        Assert.Equal("002", mc.ElectricalParameterSlots[2].P); // epap2P で 2P
    }

    // ---- PropMcChildElement (改訂<6>/<8>/<9>/<10>) ---------------------------

    private static MainCircuitData McParent(string kv0, char pole)
    {
        MainCircuitData mc = NewData();
        mc.ReservedWord = "MC";
        mc.LineTypeCode = "M"; // TM/SM/M 系 → PropMcChildElement 経由
        mc.CircuitVoltage = [kv0, "000", "000"];
        mc.CircuitPoleCount = pole;
        return mc;
    }

    private static MainCircuitData Child(string yoyaku, string fpalv0)
    {
        MainCircuitData c = NewData();
        c.ReservedWord = yoyaku;
        c.ParentSequenceNumber = "001";
        c.AttachedParameter.LoadVoltage[0] = fpalv0;
        return c;
    }

    [Fact]
    public void PropMcChildElementは負荷電圧200の子機をエレメント2にする()
    {
        MainCircuitData mc = McParent("210", '2');
        MainCircuitData sb = Child("SB", "200");

        MainCircuitResult[] maina = [Res("001", mc), Res("002", sb)];

        SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Equal("2", sb.ElectricalParameterSlots[2].E);
    }

    [Fact]
    public void PropMcChildElementは負荷電圧100の子機をエレメント1にする()
    {
        MainCircuitData mc = McParent("210", '2');
        MainCircuitData mcb = Child("MCB", "100");

        MainCircuitResult[] maina = [Res("001", mc), Res("002", mcb)];

        SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Equal("1", mcb.ElectricalParameterSlots[2].E);
    }

    [Fact]
    public void PropMcChildElementは負荷電圧なしで親210かつ非3Pならエレメント2にする()
    {
        MainCircuitData mc = McParent("210", '2'); // ep[2].P="002"(非003)
        MainCircuitData elb = Child("ELB", "000");

        MainCircuitResult[] maina = [Res("001", mc), Res("002", elb)];

        SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Equal("2", elb.ElectricalParameterSlots[2].E);
    }

    [Fact]
    public void PropMcChildElementは負荷電圧なしで親3Pならエレメント1にする()
    {
        MainCircuitData mc = McParent("105", '3'); // ep[2].P="003"
        MainCircuitData sb = Child("SB", "000");

        MainCircuitResult[] maina = [Res("001", mc), Res("002", sb)];

        SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Equal("1", sb.ElectricalParameterSlots[2].E);
    }

    [Fact]
    public void PropMcChildElementは子機が3Pなら線式と極数を3にする()
    {
        MainCircuitData mc = McParent("210", '2');
        MainCircuitData mcb = Child("MCB", "");
        mcb.ElectricalParameterSlots[0].P = "003"; // 子機3P

        MainCircuitResult[] maina = [Res("001", mc), Res("002", mcb)];

        SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Equal('3', mcb.CircuitWireType);
        Assert.Equal('3', mcb.CircuitPoleCount);
    }

    // ---- SetParam_ep2 TB (list+index) ---------------------------------------

    private static MainCircuitData Tb()
    {
        MainCircuitData tb = NewData();
        tb.ReservedWord = "TB";
        return tb;
    }

    [Fact]
    public void SetParam_ep2_TBは直列トリップ兄弟ありで極数6にする()
    {
        MainCircuitData p = NewData();
        p.ReservedWord = "P";
        MainCircuitData sd = NewData();
        sd.ReservedWord = "MCSD";
        MainCircuitData tb = Tb();

        MainCircuitResult[] maina = [Res("001", p), Res("002", sd), Res("003", tb)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 2);

        Assert.Null(error);
        Assert.Equal("006", tb.ElectricalParameterSlots[2].P);
    }

    [Fact]
    public void SetParam_ep2_TBはMGSH3Pの電源3P3Wで極数6にする()
    {
        MainCircuitData p = NewData();
        p.ReservedWord = "P";
        p.SystemNumber = "001";
        p.CircuitPhaseCount = '3';
        p.CircuitWireType = '3';
        MainCircuitData mg = NewData();
        mg.ReservedWord = "MG";
        mg.SpecialReservedWordKind = '1'; // MGSH+(3P)
        MainCircuitData tb = Tb();
        tb.SystemNumber = "001";

        MainCircuitResult[] maina = [Res("001", p), Res("002", mg), Res("003", tb)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 2);

        Assert.Null(error);
        Assert.Equal("006", tb.ElectricalParameterSlots[2].P);
    }

    [Fact]
    public void SetParam_ep2_TBは27Cで極数3かつ自身を特殊区分6にする()
    {
        MainCircuitData p = NewData();
        p.ReservedWord = "P";
        MainCircuitData cr = NewData();
        cr.ReservedWord = "CR";
        cr.SpecialReservedWordKind = '5'; // 27C
        MainCircuitData tb = Tb();

        MainCircuitResult[] maina = [Res("001", p), Res("002", cr), Res("003", tb)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 2);

        Assert.Null(error);
        Assert.Equal("003", tb.ElectricalParameterSlots[2].P);
        Assert.Equal('6', tb.SpecialReservedWordKind);
    }

    [Fact]
    public void SetParam_ep2_TBは自身にTBKYコメントがあれば極数2にする()
    {
        MainCircuitData tb = Tb();
        tb.AttachedParameter.Comment = "TBKY";

        MainCircuitResult[] maina = [Res("001", tb)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Null(error);
        Assert.Equal("002", tb.ElectricalParameterSlots[2].P);
    }

    [Fact]
    public void SetParam_ep2_TBは同一行のTBKYコメントで極数1にする()
    {
        MainCircuitData other = NewData();
        other.ReservedWord = "TB";
        other.AttachedParameter.Comment = "TBKY";
        MainCircuitData tb = Tb();

        MainCircuitResult[] maina = [Res("001", other), Res("002", tb)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 1);

        Assert.Null(error);
        Assert.Equal("001", tb.ElectricalParameterSlots[2].P);
    }

    [Fact]
    public void SetParam_ep2_TBは基本ケースで相線式から極数と電圧2を決める()
    {
        MainCircuitData tb = Tb();
        tb.CircuitPhaseCount = '1';
        tb.CircuitWireType = '2';
        tb.CircuitVoltage = ["210", "000", "000"];
        tb.CircuitVoltageKind = 'A';

        MainCircuitResult[] maina = [Res("001", tb)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Null(error);
        Assert.Equal("002", tb.ElectricalParameterSlots[2].P);
        Assert.Equal("000210.0", tb.ElectricalParameterSlots[2].V2[0]);
        Assert.Equal('A', tb.ElectricalParameterSlots[2].V2Kbn);
    }

    // ---- SetParam_ep2 WL/GL/RL/OL/BL (list+index) ---------------------------

    private static MainCircuitData Lamp(string yoyaku)
    {
        MainCircuitData lamp = NewData();
        lamp.ReservedWord = yoyaku;
        lamp.CircuitVoltage = ["100", "000", "000"];
        lamp.CircuitVoltageKind = 'A';
        return lamp;
    }

    [Theory]
    [InlineData("01", "025.0")]
    [InlineData("02", "025.0")]
    [InlineData("03", "030.0")]
    [InlineData("", "030.0")]
    public void SetParam_ep2_WLは製作仕様区分で径サイズを決める(string spec, string expectedKsize)
    {
        MainCircuitData wl = Lamp("WL");

        MainCircuitResult[] maina = [Res("001", wl)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 0, spec);

        Assert.Null(error);
        Assert.Equal(expectedKsize, wl.ElectricalParameterSlots[2].Ksize);
    }

    [Fact]
    public void SetParam_ep2_WLは製作仕様区分未指定なら径サイズ030にする()
    {
        MainCircuitData gl = Lamp("GL");

        MainCircuitResult[] maina = [Res("001", gl)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 0);

        Assert.Null(error);
        Assert.Equal("030.0", gl.ElectricalParameterSlots[2].Ksize);
    }

    [Fact]
    public void SetParam_ep2_WLは電圧2に回路電圧最大値を格納する()
    {
        MainCircuitData rl = Lamp("RL");

        MainCircuitResult[] maina = [Res("001", rl)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 0, "01");

        Assert.Null(error);
        Assert.Equal("000100.0", rl.ElectricalParameterSlots[2].V2[0]);
        Assert.Equal('A', rl.ElectricalParameterSlots[2].V2Kbn);
    }

    [Fact]
    public void SetParam_ep2_WLは直前がFかつTRなら電圧を5点5Vにする()
    {
        MainCircuitData f = NewData();
        f.ReservedWord = "F";
        f.DataType[0] = "TR     ";
        MainCircuitData bl = Lamp("BL");

        MainCircuitResult[] maina = [Res("001", f), Res("002", bl)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 1, "01");

        Assert.Null(error);
        Assert.Equal("005", bl.CircuitVoltage[0]);
        Assert.Equal("000", bl.CircuitVoltage[1]);
        Assert.Equal("000", bl.CircuitVoltage[2]);
        Assert.Equal("000005.5", bl.ElectricalParameterSlots[2].V2[0]);
    }

    [Fact]
    public void SetParam_ep2_WLは直前がFでもTR以外なら電圧を上書きしない()
    {
        MainCircuitData f = NewData();
        f.ReservedWord = "F";
        f.DataType[0] = "       "; // TR 以外
        MainCircuitData ol = Lamp("OL");

        MainCircuitResult[] maina = [Res("001", f), Res("002", ol)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 1, "01");

        Assert.Null(error);
        Assert.Equal("100", ol.CircuitVoltage[0]); // 上書きされない
        Assert.Equal("000100.0", ol.ElectricalParameterSlots[2].V2[0]);
    }

    // ---- SetParam_ep2 WH (list+index) ---------------------------------------

    [Fact]
    public void SetParam_ep2_WHはVT無で自身の回路電圧を公称電圧に変換しV2にする()
    {
        MainCircuitData wh = NewData();
        wh.ReservedWord = "WH";
        wh.CircuitElement = '3'; // 計器用回路(VT無)
        wh.CircuitVoltage = ["000", "000", "105"];
        wh.CircuitVoltageKind = 'A';
        wh.CircuitFrequency = "60";
        wh.CircuitPhaseCount = '1';
        wh.CircuitWireType = '2';

        MainCircuitResult[] maina = [Res("001", wh)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 0);

        ElectricalParameters ep2 = wh.ElectricalParameterSlots[2];
        Assert.Null(error);
        Assert.Equal("000100.0", ep2.V2[0]); // 105 → 公称100
        Assert.Equal("000000.0", ep2.V1[0]); // VT無は V1=0
        Assert.Equal("1", ep2.Ph2[0]);
        Assert.Equal("2", ep2.Wr2[0]);
        Assert.Equal('A', ep2.V2Kbn);
        Assert.Equal("60", ep2.Hz);
    }

    [Fact]
    public void SetParam_ep2_WHはVT付で上方VTの回路電圧をV1にしV2は110固定にする()
    {
        MainCircuitData vt = NewData();
        vt.ReservedWord = "VT";
        vt.CircuitVoltage = ["000", "000", "210"];

        MainCircuitData wh = NewData();
        wh.ReservedWord = "WH";
        wh.CircuitElement = '4'; // 計器用回路(VT付)
        wh.CircuitVoltage = ["000", "000", "105"];
        wh.CircuitFrequency = "50";

        MainCircuitResult[] maina = [Res("001", vt), Res("002", wh)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 1);

        ElectricalParameters ep2 = wh.ElectricalParameterSlots[2];
        Assert.Null(error);
        Assert.Equal("000200.0", ep2.V1[0]); // VT 210 → 公称200
        Assert.Equal("000110.0", ep2.V2[0]); // VT付は 110 固定
        Assert.Equal("50", ep2.Hz);
    }

    [Fact]
    public void SetParam_ep2_WHは周波数不整合なら設定せず抜ける()
    {
        MainCircuitData wh = NewData();
        wh.ReservedWord = "WH";
        wh.CircuitElement = '3';
        wh.CircuitVoltage = ["000", "000", "105"];
        wh.CircuitFrequency = "60";
        wh.ElectricalParameterSlots[0].Hz = "50"; // ep[0].Hz が "00" 以外かつ kpahz と相違

        MainCircuitResult[] maina = [Res("001", wh)];

        CircuitParseError? error = SecondaryParameterSetter.SetParam_ep2(maina, 0);

        ElectricalParameters ep2 = wh.ElectricalParameterSlots[2];
        Assert.Null(error);
        Assert.Equal("000000.0", ep2.V2[0]); // 初期化のみで電圧未設定
        Assert.Equal("00", ep2.Hz);          // Hz は設定されない
    }
}

