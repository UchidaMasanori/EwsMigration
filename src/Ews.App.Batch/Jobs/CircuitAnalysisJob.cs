using Ews.Analysis;
using Ews.Data.SqlServer;
using Ews.Domain.Circuits;
using Ews.Domain.Projects;

namespace Ews.App.Batch.Jobs;

/// <summary>
/// 回路解析ジョブ。
///
/// 【C原典】toku/qrespo/sekkei/fyskews/src/FyskEwsMain.c (回路解析処理 / fyskews) の main()。
/// 旧実行ファイル fyskews の起動に相当し、依頼明細番号を指定して回路解析を実行する。
///
/// 原典の main() は「データ読込(Fysk_Set_data) → fyskews 固有の行種別前処理(Fysk_* 群) →
/// ライブラリ本体 Fysk10_Main の呼び出し」の順で処理する。本ジョブも同じ責務分担とし、
/// 回路記述の読込と <see cref="CircuitLineNormalizer"/> による正規化をここで行ってから、
/// <see cref="CircuitAnalyzer"/>(= Fysk10_Main)へ前処理済みデータを渡す。
///
/// 使用例:
///   dotnet run --project src/Ews.App.Batch -- --job circuit-analysis request=AB12345 item=01
/// </summary>
public sealed class CircuitAnalysisJob : IBatchJob
{
    private readonly SqlCircuitDescriptionRepository _circuitRepository;
    private readonly CircuitAnalyzer _analyzer;

    public CircuitAnalysisJob(
        SqlCircuitDescriptionRepository circuitRepository,
        CircuitAnalyzer analyzer)
    {
        _circuitRepository = circuitRepository;
        _analyzer = analyzer;
    }

    public string Name => "circuit-analysis";

    public string Description => "回路解析(C原典: fyskews / Fysk10_Main)";

    public Task<int> RunAsync(JobContext context, CancellationToken cancellationToken)
    {
        string requestNumber = context.Require("request");
        string itemNumber = context.Get("item", "01");

        // 【C原典】Fysk_Set_data 相当: 回路内容記述(FYDF805)を読み込む。
        //         FyIsamStartR(FYDF805) → FyIsamNextR ループに相当。
        var key = new CircuitLineKey(requestNumber, itemNumber);
        var lines = _circuitRepository.ReadSequential(key).ToList();

        // 【C原典】FyskEwsMain.c main() が Fysk10_Main を呼ぶ前に実行する fyskews 固有の
        //         行種別前処理(コンマ整理・行結合・行種変換 等)を一括適用する。
        CircuitLineNormalizer.Normalize(lines);

        // 物件情報(bukken1/bukken2)は本パイロットでは未取得のため暫定インスタンスを渡す。
        var bukken = new ProjectInfo { NewRequestNumber = requestNumber, NewItemNumber = itemNumber };

        // 【C原典】Fysk10_Main(&bukken1, &bukken2, imagec, imagea, ...) の呼び出し。
        CircuitAnalysisResult result = _analyzer.Analyze(bukken, bukken, lines);

        Console.WriteLine($"回路解析: 依頼={result.RequestNumber} 明細={result.ItemNumber}");
        Console.WriteLine($"  主回路結果 {result.MainCircuits.Count} 件");
        Console.WriteLine($"  系統テーブル {result.ParseResult.Systems.Count} 件 / 行種 {result.ParseResult.LineTypes.Count} 件 / 仕様 {result.ParseResult.Specs.Count} 件 / 機器 {result.ParseResult.MainEquipment.Count} 件");
        foreach (string warning in result.Warnings)
        {
            Console.WriteLine($"  [警告] {warning}");
        }

        return Task.FromResult(result.Warnings.Count == 0 ? 0 : 1);
    }
}
