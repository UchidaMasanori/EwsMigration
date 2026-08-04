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
        char ksyubetu = '1')
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
}
