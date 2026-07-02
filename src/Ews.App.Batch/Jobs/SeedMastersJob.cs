using Ews.Data.Seeding;

namespace Ews.App.Batch.Jobs;

/// <summary>
/// .cns/.inf マスタを SQL Server へシードするジョブ。
///
/// 【C原典】該当する単独バッチは無く、旧構成では .cns/.inf を実行時に直接読み込んでいた
/// (FyGetEigCons / FyGetFileName 等)。SQL Server 化に伴う初期データ投入用に新設。
///
/// 使用例:
///   dotnet run --project src/Ews.App.Batch -- --job seed-masters bumon=確認用/bumon.gai17.cns
/// </summary>
public sealed class SeedMastersJob : IBatchJob
{
    private readonly CnsMasterLoader _cnsLoader;

    public SeedMastersJob(CnsMasterLoader cnsLoader)
    {
        _cnsLoader = cnsLoader;
    }

    public string Name => "seed-masters";

    public string Description => "マスタ投入(.cns/.inf → SQL Server)";

    public Task<int> RunAsync(JobContext context, CancellationToken cancellationToken)
    {
        string bumonPath = context.Get("bumon");
        if (bumonPath.Length > 0)
        {
            int count = _cnsLoader.SeedDepartmentMaster(bumonPath);
            Console.WriteLine($"部門マスタ(bumon.cns)を投入: {count} 件");
        }
        else
        {
            Console.WriteLine("投入対象が指定されていません(例: bumon=<path>)。");
        }

        return Task.FromResult(0);
    }
}
