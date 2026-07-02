using Ews.Analysis;

namespace Ews.App.Batch.Jobs;

/// <summary>
/// 回路解析ジョブ。
///
/// 【C原典】toku/qrespo/sekkei/fyskews/src/FyskEwsMain.c (回路解析処理 / fyskews)。
/// 旧実行ファイル fyskews の起動に相当し、依頼明細番号を指定して回路解析を実行する。
///
/// 使用例:
///   dotnet run --project src/Ews.App.Batch -- --job circuit-analysis request=AB12345 item=01
/// </summary>
public sealed class CircuitAnalysisJob : IBatchJob
{
    private readonly CircuitAnalyzer _analyzer;

    public CircuitAnalysisJob(CircuitAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public string Name => "circuit-analysis";

    public string Description => "回路解析(C原典: fyskews / Fysk10_Main)";

    public Task<int> RunAsync(JobContext context, CancellationToken cancellationToken)
    {
        string requestNumber = context.Require("request");
        string itemNumber = context.Get("item", "01");

        CircuitAnalysisResult result = _analyzer.Analyze(requestNumber, itemNumber);

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
