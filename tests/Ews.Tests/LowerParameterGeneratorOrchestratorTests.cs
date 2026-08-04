using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 下流パラメータ生成 MAIN(<see cref="LowerParameterGenerator.MakeLowerParameters"/>)の
/// オーケストレーション(呼出順・戻り値・分岐)検証。
/// 【C原典】Fyss15_Make_LowerParm(toku/sekkei/src/Fyss15.c:132-390)。
/// </summary>
public sealed class LowerParameterGeneratorOrchestratorTests
{
    // 負荷発生元決定(Fyss31)を通過できる有効な末端主回路 1 件を作る。負荷容量で電流を確定する。
    private static MainCircuitResult Row(
        string datano = "001",
        string yoyaku = "MCB     ")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                SystemKind = '1',
                CircuitElement = '1',
                TerminalKind = '1',
                ReservedWord = yoyaku,
                ParallelNumber = "001",
                HierarchyNumber = "001",
                IncomingNumber = "001",
                ParentSequenceNumber = "000",
                LineTypeGroupNumber = "001",
                LineTypeCode = "AA",
                CircuitPhaseCount = '1',
            },
        };
        r.Data.CircuitVoltage[0] = "100";
        r.Data.AttachedParameter.LoadCapacity = "0000050";
        r.Data.AttachedParameter.LoadKind = "H ";
        // 電気パラメータは整形後「値無し」= 小数点付きゼロで初期化する。
        foreach (ElectricalParameters ep in r.Data.ElectricalParameterSlots)
        {
            ep.At = "00000.000";
            ep.Af = "00000.000";
            ep.A1 = "00000.000";
            ep.A2 = "00000.000";
        }
        return r;
    }

    // ＣＴ生成対象になる AM(電流計)。負荷容量で Fyss31 を通過しつつ、定格電流 2(ep[1].A2)を 30A 超にする。
    // 先頭以外(datano "002")に置くことで前後 2 箇所に CT が挿入される。
    private static MainCircuitResult Am()
    {
        MainCircuitResult r = Row(datano: "002", yoyaku: "AM      ");
        r.Data.ElectricalParameterSlots[1].A2 = "00050.000";
        return r;
    }

    // 既定デリゲート/テーブルで MAIN を呼び出すヘルパー。equipmentSearch の戻り値だけ差し替える。
    private static LowerParameterGenerator.LowerParameterResult Run(
        IReadOnlyList<MainCircuitResult> mains,
        int breakerRet = 0,
        char hycpskbn = '1',
        string zoneCode = "00000")
    {
        return LowerParameterGenerator.MakeLowerParameters(
            mains,
            panelCompositionKind: hycpskbn,
            branchArrayDesignationKind: '1',
            autoKick: ' ',
            zoneCode: zoneCode,
            manufacturingSpecKind: "01",
            reservedWords: new List<ReservedWordMaster>(),
            components: new List<ComponentEquipment>(),
            parameterSettingTable: new List<ParameterSettingType>(),
            wireSizeTable: new List<WireSizeSetting>(),
            ratedCurrent2Table: new List<RatedCurrent2Setting>(),
            ratedCurrent1Table: new List<RatedCurrent1Setting>(),
            majorClassResolver: _ => ' ',
            equipmentSearch: _ => breakerRet,
            hasNothingInFreeText: _ => true,
            findParent: _ => null);
    }

    [Fact]
    public void 主回路が空なら戻り値0で入力と同じ配列を返す()
    {
        var mains = new List<MainCircuitResult>();

        LowerParameterGenerator.LowerParameterResult result = Run(mains);

        Assert.Equal(0, result.ReturnCode);
        Assert.False(result.ComponentsCleared);
        Assert.Same(mains, result.Mains);
    }

    [Fact]
    public void 末端ブレーカ選定のretが3なら戻り値3で打ち切る()
    {
        var mains = new List<MainCircuitResult> { Row() };

        LowerParameterGenerator.LowerParameterResult result = Run(mains, breakerRet: 3);

        Assert.Equal(3, result.ReturnCode);
        Assert.False(result.ComponentsCleared);
    }

    [Fact]
    public void 末端ブレーカ選定のretが3以外の非0なら戻り値1で打ち切る()
    {
        var mains = new List<MainCircuitResult> { Row() };

        LowerParameterGenerator.LowerParameterResult result = Run(mains, breakerRet: 2);

        Assert.Equal(1, result.ReturnCode);
    }

    [Fact]
    public void 機器サーチデリゲートへ主回路を渡す()
    {
        var mains = new List<MainCircuitResult> { Row() };
        bool searched = false;

        LowerParameterGenerator.MakeLowerParameters(
            mains,
            panelCompositionKind: '1',
            branchArrayDesignationKind: '1',
            autoKick: ' ',
            zoneCode: "00000",
            manufacturingSpecKind: "01",
            reservedWords: new List<ReservedWordMaster>(),
            components: new List<ComponentEquipment>(),
            parameterSettingTable: new List<ParameterSettingType>(),
            wireSizeTable: new List<WireSizeSetting>(),
            ratedCurrent2Table: new List<RatedCurrent2Setting>(),
            ratedCurrent1Table: new List<RatedCurrent1Setting>(),
            majorClassResolver: _ => ' ',
            equipmentSearch: _ => { searched = true; return 0; },
            hasNothingInFreeText: _ => true,
            findParent: _ => null);

        Assert.True(searched);
    }

    [Fact]
    public void 特注盤でも例外なく戻り値0で完了する()
    {
        var mains = new List<MainCircuitResult> { Row() };

        LowerParameterGenerator.LowerParameterResult result = Run(mains, hycpskbn: '3');

        Assert.Equal(0, result.ReturnCode);
    }

    [Fact]
    public void CT自動生成時は戻り値マイナス1で主回路を再構築し構成機器クリアを指示する()
    {
        var mains = new List<MainCircuitResult> { Row("001"), Am() };

        LowerParameterGenerator.LowerParameterResult result = Run(mains);

        Assert.Equal(-1, result.ReturnCode);
        Assert.True(result.ComponentsCleared);
        // AM の前後 2 箇所に CT が挿入され件数が増える。
        Assert.True(result.Mains.Count > mains.Count);
    }
}
