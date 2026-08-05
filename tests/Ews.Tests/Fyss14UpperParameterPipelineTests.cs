using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="Fyss14UpperParameterPipeline"/>(【C原典】Fyss14.c Fyss14_Make_UpperParm)の統合テスト。
/// 周辺設定段→自動生成 f/r ループ→後処理群の結線と、致命エラー時の早期 return を検証する。
/// </summary>
public sealed class Fyss14UpperParameterPipelineTests
{
    private static CircuitDescriptionArea EmptyArea() =>
        new(Array.Empty<CircuitDescriptionLine>());

    private static IReadOnlyList<ReservedWordMaster> EmptyMaster() =>
        Array.Empty<ReservedWordMaster>();

    // 単純な P 入線(P 系統)を組む。生成判定に掛からず 1 巡で完走する。
    private static MainCircuitResult Incoming(int datano)
    {
        var r = new MainCircuitResult { SequenceNumber = datano.ToString("D3") };
        MainCircuitData d = r.Data;
        d.SystemKind = '1';
        d.ReservedWord = "P";
        d.LineTypeCode = "P";
        d.CircuitPhaseCount = '3';
        d.CircuitWireType = '3';
        d.CircuitVoltage[0] = "210";
        d.CircuitVoltage[1] = "105";
        d.CircuitVoltage[2] = "000";
        return r;
    }

    [Fact]
    public void Run_生成が無ければ1巡で完走し致命エラーは無い()
    {
        var mains = new List<MainCircuitResult> { Incoming(1) };

        UpperParameterPipelineResult result =
            Fyss14UpperParameterPipeline.Run(mains, EmptyArea(), EmptyMaster(), UpperParameterBuilder.Hz1);

        Assert.Null(result.FatalError);
        Assert.Same(mains, result.Records);   // 生成なし → 同一参照(件数不変)
        Assert.Empty(result.DesignErrors);
    }

    [Fact]
    public void Run_計器回路の並び異常なら致命エラーを返す()
    {
        // AS は回路要素区分='2' が必須。既定 ' ' なら Keiki_Kairo_Check が FY-645E を返す。
        var bad = new MainCircuitResult { SequenceNumber = "001" };
        bad.Data.ReservedWord = "AS";
        var mains = new List<MainCircuitResult> { bad };

        UpperParameterPipelineResult result =
            Fyss14UpperParameterPipeline.Run(mains, EmptyArea(), EmptyMaster(), UpperParameterBuilder.Hz1);

        Assert.NotNull(result.FatalError);
        Assert.Equal("FY-645E", result.FatalError!.ErrorCode);
        Assert.Empty(result.DesignErrors);
    }

    [Fact]
    public void Run_後処理の切り換えタイプ設定まで到達する()
    {
        // CSDT の対(同一予約語・同一 ysno・切り換えタイプ未設定)は後処理 CS_MCDT_12_21_SET で
        // 親データ追番一致(系統外は共に 000 へリセットされる)により 1-2 型('1')が双方へ設定される。
        var a = new MainCircuitResult { SequenceNumber = "001" };
        a.Data.ReservedWord = "CSDT";
        a.Data.DesignationNumber = "01";
        var b = new MainCircuitResult { SequenceNumber = "002" };
        b.Data.ReservedWord = "CSDT";
        b.Data.DesignationNumber = "01";
        var mains = new List<MainCircuitResult> { a, b };

        UpperParameterPipelineResult result =
            Fyss14UpperParameterPipeline.Run(mains, EmptyArea(), EmptyMaster(), UpperParameterBuilder.Hz1);

        Assert.Null(result.FatalError);
        Assert.Equal('1', a.Data.SwitchType);
        Assert.Equal('1', b.Data.SwitchType);
    }

    [Fact]
    public void Run_主回路がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Fyss14UpperParameterPipeline.Run(null!, EmptyArea(), EmptyMaster(), UpperParameterBuilder.Hz1));
    }

    [Fact]
    public void Run_回路内容記述がnullなら例外()
    {
        var mains = new List<MainCircuitResult> { Incoming(1) };

        Assert.Throws<ArgumentNullException>(() =>
            Fyss14UpperParameterPipeline.Run(mains, null!, EmptyMaster(), UpperParameterBuilder.Hz1));
    }

    [Fact]
    public void Run_予約語マスタがnullなら例外()
    {
        var mains = new List<MainCircuitResult> { Incoming(1) };

        Assert.Throws<ArgumentNullException>(() =>
            Fyss14UpperParameterPipeline.Run(mains, EmptyArea(), null!, UpperParameterBuilder.Hz1));
    }
}
