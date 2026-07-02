using Ews.Analysis;
using Ews.App.Batch.Jobs;
using Ews.Data.Seeding;
using Ews.Data.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// =====================================================================================
//  EWS 移行バッチ CLI ホスト
//
//  【C原典】旧 AIX の個別バッチ実行ファイル群(fyskews, FyAutoSinP 等)を集約した
//           単一エントリポイント。--job <名前> で実行するジョブを選択する。
//
//  使用例:
//    dotnet run --project src/Ews.App.Batch -- --job circuit-analysis request=AB12345 item=01
//    dotnet run --project src/Ews.App.Batch -- --job seed-masters bumon=確認用/bumon.gai17.cns
// =====================================================================================

IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables(prefix: "EWS_")
    .Build();

string connectionString = configuration.GetConnectionString("EwsDatabase")
    ?? throw new InvalidOperationException("接続文字列 ConnectionStrings:EwsDatabase が未設定です。");

ServiceProvider services = new ServiceCollection()
    // データ層(SQL Server)
    .AddSingleton(new SqlConnectionFactory(connectionString))
    .AddSingleton<SqlEquipmentMasterRepository>()
    .AddSingleton<SqlCircuitDescriptionRepository>()
    .AddSingleton<CnsMasterLoader>()
    // 解析層
    .AddSingleton<CircuitStringChecker>()
    .AddSingleton<CircuitAnalyzer>()
    // ジョブ
    .AddSingleton<IBatchJob, CircuitAnalysisJob>()
    .AddSingleton<IBatchJob, SeedMastersJob>()
    .BuildServiceProvider();

(string? jobName, IReadOnlyDictionary<string, string> options) = ParseArguments(args);

if (jobName is null)
{
    Console.WriteLine("使い方: --job <ジョブ名> [key=value ...]");
    Console.WriteLine("利用可能なジョブ:");
    foreach (IBatchJob registered in services.GetServices<IBatchJob>())
    {
        Console.WriteLine($"  {registered.Name,-20} {registered.Description}");
    }

    return 2;
}

IBatchJob? job = services.GetServices<IBatchJob>().FirstOrDefault(j => j.Name == jobName);
if (job is null)
{
    Console.Error.WriteLine($"ジョブが見つかりません: {jobName}");
    return 2;
}

var context = new JobContext { Options = options };
try
{
    return await job.RunAsync(context, CancellationToken.None);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ジョブ実行エラー: {ex.Message}");
    return 1;
}

// --job <name> key=value key=value ... を解析する。
static (string? JobName, IReadOnlyDictionary<string, string> Options) ParseArguments(string[] args)
{
    string? jobName = null;
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < args.Length; i++)
    {
        string arg = args[i];
        if (arg == "--job" && i + 1 < args.Length)
        {
            jobName = args[++i];
            continue;
        }

        int eq = arg.IndexOf('=');
        if (eq > 0)
        {
            options[arg[..eq]] = arg[(eq + 1)..];
        }
    }

    return (jobName, options);
}
