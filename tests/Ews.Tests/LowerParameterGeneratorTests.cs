using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 下流からのパラメータ生成(<see cref="LowerParameterGenerator"/>)の移植検証。
/// 【C原典】Fyss15_MCB1P_NT(toku/sekkei/src/Fyss15.c:404)。
/// ＮＴに直接つながる MCB1P/RMCB1P(下流なし)の使用相へ N 相を追加する処理を検証する。
/// </summary>
public sealed class LowerParameterGeneratorTests
{
    private static MainCircuitResult Row(
        string datano,
        string yoyaku = "",
        string oyatno = "000",
        string ep0P = "000",
        string siyouso = "    ",
        char ksyubetu = '1',
        char kiryoso = ' ',
        char kaetyp = ' ',
        string doukkno = "  ",
        string denryu = "00000000",
        char ahassei = ' ')
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ReservedWord = yoyaku,
                ParentSequenceNumber = oyatno,
                UsedPhase = siyouso,
                SystemKind = ksyubetu,
                CircuitElement = kiryoso,
                SwitchType = kaetyp,
                IdentityNumber = doukkno,
                EnergizingCurrent = denryu,
                LoadSourceKind = ahassei,
            },
        };
        r.Data.ElectricalParameterSlots[0].P = ep0P;
        return r;
    }

    [Fact]
    public void MCB1Pで下流なしなら使用相にN相を追加する()
    {
        var mcb = Row("001", yoyaku: "MCB     ", oyatno: "001", ep0P: "001", siyouso: "X   ");
        LowerParameterGenerator.AdjustMcb1PhaseForNt([mcb], 'N');
        Assert.Equal("XN  ", mcb.Data.UsedPhase);
    }

    [Fact]
    public void MCB1Pで下流があれば使用相を変更しない()
    {
        var mcb = Row("001", yoyaku: "MCB     ", oyatno: "001", ep0P: "001", siyouso: "X   ");
        var child = Row("002", yoyaku: "MCB     ", oyatno: "005", ep0P: "001", siyouso: "Y   ");
        LowerParameterGenerator.AdjustMcb1PhaseForNt([mcb, child], 'N');
        Assert.Equal("X   ", mcb.Data.UsedPhase);
    }

    [Fact]
    public void RMCB1Pで下流なしなら使用相にN相を追加する()
    {
        var rmcb = Row("001", yoyaku: "RMCB    ", oyatno: "001", ep0P: "001", siyouso: "Y   ");
        LowerParameterGenerator.AdjustMcb1PhaseForNt([rmcb], 'N');
        Assert.Equal("YN  ", rmcb.Data.UsedPhase);
    }

    [Fact]
    public void 極数001以外のMCBは対象外()
    {
        var mcb = Row("001", yoyaku: "MCB     ", oyatno: "001", ep0P: "003", siyouso: "X   ");
        LowerParameterGenerator.AdjustMcb1PhaseForNt([mcb], 'N');
        Assert.Equal("X   ", mcb.Data.UsedPhase);
    }

    [Fact]
    public void 予約語MCB以外は対象外()
    {
        var mc = Row("001", yoyaku: "MC      ", oyatno: "001", ep0P: "001", siyouso: "X   ");
        LowerParameterGenerator.AdjustMcb1PhaseForNt([mc], 'N');
        Assert.Equal("X   ", mc.Data.UsedPhase);
    }

    [Fact]
    public void 下流抽出エラー時は使用相を変更しない()
    {
        // 系統種別が '1' 以外だと Fyss35_Select_Karyu_Sub が ret!=0(null)を返す。
        var mcb = Row("001", yoyaku: "MCB     ", oyatno: "001", ep0P: "001", siyouso: "X   ", ksyubetu: '0');
        LowerParameterGenerator.AdjustMcb1PhaseForNt([mcb], 'N');
        Assert.Equal("X   ", mcb.Data.UsedPhase);
    }

    [Fact]
    public void 型MCDTは同一機器認識番号ペアの通電電流小さい方に上流積み上げ区分をセットする()
    {
        var a = Row("001", yoyaku: "MCDT    ", oyatno: "000", kiryoso: '1', kaetyp: '1',
            doukkno: "01", denryu: "00010.00");
        var b = Row("002", yoyaku: "MCDT    ", oyatno: "000", kiryoso: '1', kaetyp: '1',
            doukkno: "01", denryu: "00005.00");
        LowerParameterGenerator.Process12McdtCsdt([a, b]);
        Assert.Equal('1', b.Data.StackKind); // 小さい方(5A)
        Assert.Equal(' ', a.Data.StackKind);
    }

    [Fact]
    public void 型CSDTも同様に処理される()
    {
        var a = Row("001", yoyaku: "CSDT    ", oyatno: "000", kiryoso: '1', kaetyp: '1',
            doukkno: "01", denryu: "00005.00");
        var b = Row("002", yoyaku: "CSDT    ", oyatno: "000", kiryoso: '1', kaetyp: '1',
            doukkno: "01", denryu: "00010.00");
        LowerParameterGenerator.Process12McdtCsdt([a, b]);
        Assert.Equal('1', a.Data.StackKind); // 小さい方(5A)
        Assert.Equal(' ', b.Data.StackKind);
    }

    [Fact]
    public void 切り換えタイプ1以外のMCDTは対象外()
    {
        var a = Row("001", yoyaku: "MCDT    ", oyatno: "000", kiryoso: '1', kaetyp: '2',
            doukkno: "01", denryu: "00010.00");
        var b = Row("002", yoyaku: "MCDT    ", oyatno: "000", kiryoso: '1', kaetyp: '2',
            doukkno: "01", denryu: "00005.00");
        LowerParameterGenerator.Process12McdtCsdt([a, b]);
        Assert.Equal(' ', a.Data.StackKind);
        Assert.Equal(' ', b.Data.StackKind);
    }

    [Fact]
    public void 型MCDTの下流機器の通電電流値と積算エリアをクリアする()
    {
        var mcdt = Row("001", yoyaku: "MCDT    ", oyatno: "000", kiryoso: '1', kaetyp: '1',
            doukkno: "01", denryu: "00010.00");
        var child = Row("002", yoyaku: "LOAD    ", oyatno: "001", kiryoso: '1',
            doukkno: "02", denryu: "00003.00");
        child.Work.AccumulationSlots[0].A = 5.0;
        LowerParameterGenerator.Process12McdtCsdt([mcdt, child]);
        Assert.Equal("00000.00", child.Data.EnergizingCurrent);
        Assert.Equal(0.0, child.Work.AccumulationSlots[0].A);
    }

    [Fact]
    public void 下流に負荷発生元があるとそこで打ち切りクリアしない()
    {
        var mcdt = Row("001", yoyaku: "MCDT    ", oyatno: "000", kiryoso: '1', kaetyp: '1',
            doukkno: "01", denryu: "00010.00");
        var child = Row("002", yoyaku: "LOAD    ", oyatno: "001", kiryoso: '1',
            doukkno: "02", denryu: "00003.00", ahassei: '1');
        LowerParameterGenerator.Process12McdtCsdt([mcdt, child]);
        Assert.Equal("00003.00", child.Data.EnergizingCurrent);
    }

    [Fact]
    public void 型MCDTは同一機器認識番号の相手へ通電電流値をコピーし対象をクリアする()
    {
        var mcdt = Row("001", yoyaku: "MCDT    ", oyatno: "000", kiryoso: '1', kaetyp: '2',
            doukkno: "01", denryu: "00010.00");
        var mate = Row("002", yoyaku: "LOAD    ", oyatno: "000", kiryoso: '1',
            doukkno: "01", denryu: "00000.00");
        LowerParameterGenerator.Process21McdtCsdt([mcdt, mate]);
        Assert.Equal("00010.00", mate.Data.EnergizingCurrent);
        Assert.Equal('1', mate.Data.LoadSourceKind);
        Assert.Equal("00000.00", mcdt.Data.EnergizingCurrent);
    }

    [Fact]
    public void 型CSDTも2_1型として処理される()
    {
        var csdt = Row("001", yoyaku: "CSDT    ", oyatno: "000", kiryoso: '1', kaetyp: '2',
            doukkno: "01", denryu: "00007.00");
        var mate = Row("002", yoyaku: "LOAD    ", oyatno: "000", kiryoso: '1',
            doukkno: "01", denryu: "00000.00");
        LowerParameterGenerator.Process21McdtCsdt([csdt, mate]);
        Assert.Equal("00007.00", mate.Data.EnergizingCurrent);
        Assert.Equal("00000.00", csdt.Data.EnergizingCurrent);
    }

    [Fact]
    public void 型で末端区分1のMCDTは対象外()
    {
        var mcdt = Row("001", yoyaku: "MCDT    ", oyatno: "000", kiryoso: '1', kaetyp: '2',
            doukkno: "01", denryu: "00010.00");
        mcdt.Data.TerminalKind = '1';
        var mate = Row("002", yoyaku: "LOAD    ", oyatno: "000", kiryoso: '1',
            doukkno: "01", denryu: "00000.00");
        LowerParameterGenerator.Process21McdtCsdt([mcdt, mate]);
        Assert.Equal("00000.00", mate.Data.EnergizingCurrent);
        Assert.Equal(' ', mate.Data.LoadSourceKind);
    }

    [Fact]
    public void 型で切り換えタイプ2以外のMCDTは対象外()
    {
        var mcdt = Row("001", yoyaku: "MCDT    ", oyatno: "000", kiryoso: '1', kaetyp: '1',
            doukkno: "01", denryu: "00010.00");
        var mate = Row("002", yoyaku: "LOAD    ", oyatno: "000", kiryoso: '1',
            doukkno: "01", denryu: "00000.00");
        LowerParameterGenerator.Process21McdtCsdt([mcdt, mate]);
        Assert.Equal(' ', mate.Data.LoadSourceKind);
    }

    [Fact]
    public void 型で機器選定区分が異なると親を辿って伝播する()
    {
        var mcdt = Row("001", yoyaku: "MCDT    ", oyatno: "000", kiryoso: '1', kaetyp: '2',
            doukkno: "01", denryu: "00010.00");
        mcdt.Work.EquipmentSelectionKind = '3';
        var mate = Row("002", yoyaku: "LOAD    ", oyatno: "003", kiryoso: '1',
            doukkno: "01", denryu: "00000.00");
        var parent = Row("003", yoyaku: "LOAD    ", oyatno: "000", kiryoso: '1',
            doukkno: "09", denryu: "00000.00");
        LowerParameterGenerator.Process21McdtCsdt([mcdt, mate, parent]);
        Assert.Equal('3', mate.Work.EquipmentSelectionKind);
        Assert.Equal('3', parent.Work.EquipmentSelectionKind);
    }

    [Fact]
    public void 型で積算エリアが相手へコピーされ対象がクリアされる()
    {
        var mcdt = Row("001", yoyaku: "MCDT    ", oyatno: "000", kiryoso: '1', kaetyp: '2',
            doukkno: "01", denryu: "00010.00");
        mcdt.Work.AccumulationSlots[0].A = 5.0;
        var mate = Row("002", yoyaku: "LOAD    ", oyatno: "000", kiryoso: '1',
            doukkno: "01", denryu: "00000.00");
        LowerParameterGenerator.Process21McdtCsdt([mcdt, mate]);
        Assert.Equal(5.0, mate.Work.AccumulationSlots[0].A);
        Assert.Equal(0.0, mcdt.Work.AccumulationSlots[0].A);
    }

    [Fact]
    public void CT回路要素2は同一機器認識番号のCT回路要素1の通電電流値を自身にセットする()
    {
        var ct1 = Row("001", yoyaku: "CT      ", oyatno: "000", kiryoso: '1',
            doukkno: "05", denryu: "00012.00");
        var ct2 = Row("002", yoyaku: "CT      ", oyatno: "000", kiryoso: '2',
            doukkno: "05", denryu: "00000000");
        LowerParameterGenerator.SetMeterCircuitCurrent([ct1, ct2]);
        Assert.Equal("00012.00", ct2.Data.EnergizingCurrent);
    }

    [Fact]
    public void CT回路要素2の下流要素にも通電電流値がセットされる()
    {
        var ct1 = Row("001", yoyaku: "CT      ", oyatno: "000", kiryoso: '1',
            doukkno: "05", denryu: "00012.00");
        var ct2 = Row("002", yoyaku: "CT      ", oyatno: "000", kiryoso: '2',
            doukkno: "05", denryu: "00000000");
        var child = Row("003", yoyaku: "LOAD    ", oyatno: "002", kiryoso: '1',
            denryu: "00000000");
        LowerParameterGenerator.SetMeterCircuitCurrent([ct1, ct2, child]);
        Assert.Equal("00012.00", child.Data.EnergizingCurrent);
    }

    [Fact]
    public void ZCT回路要素2は親データ追番の通電電流値を自身にセットする()
    {
        var parent = Row("001", yoyaku: "MCB     ", oyatno: "000", kiryoso: '1',
            denryu: "00020.00");
        var zct = Row("002", yoyaku: "ZCT     ", oyatno: "001", kiryoso: '2',
            denryu: "00000000");
        LowerParameterGenerator.SetMeterCircuitCurrent([parent, zct]);
        Assert.Equal("00020.00", zct.Data.EnergizingCurrent);
    }

    [Fact]
    public void 同一機器認識番号が0のCT回路要素2は対象外()
    {
        var ct2 = Row("001", yoyaku: "CT      ", oyatno: "000", kiryoso: '2',
            doukkno: "00", denryu: "00000000");
        LowerParameterGenerator.SetMeterCircuitCurrent([ct2]);
        Assert.Equal("00000000", ct2.Data.EnergizingCurrent);
    }

    [Fact]
    public void ZCTの親データ追番が0は対象外()
    {
        var zct = Row("001", yoyaku: "ZCT     ", oyatno: "000", kiryoso: '2',
            denryu: "00000000");
        LowerParameterGenerator.SetMeterCircuitCurrent([zct]);
        Assert.Equal("00000000", zct.Data.EnergizingCurrent);
    }
}
