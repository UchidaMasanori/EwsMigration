namespace Ews.App.Batch.Jobs;

/// <summary>
/// バッチジョブの共通インターフェース。
///
/// 【C原典】
///   - 旧 AIX では各バッチが個別実行ファイル(例 FyAutoSinP, fyskews)だった。
///   - 本移行では単一 CLI ホスト配下のジョブとして集約し、--job &lt;名前&gt; で選択実行する。
/// </summary>
public interface IBatchJob
{
    /// <summary>CLI から指定するジョブ名(例 "circuit-analysis")。</summary>
    string Name { get; }

    /// <summary>ジョブ概要。</summary>
    string Description { get; }

    /// <summary>ジョブ本体。終了コードを返す(0:正常)。</summary>
    Task<int> RunAsync(JobContext context, CancellationToken cancellationToken);
}
