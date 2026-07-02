namespace Ews.App.Batch.Jobs;

/// <summary>
/// ジョブ実行コンテキスト。CLI 引数・パラメータファイル等を保持する。
///
/// 【C原典】各バッチの main(argc, argv) が受け取っていた引数群
/// (例 AutoSinMain.c のパラメータファイル名)に相当。
/// </summary>
public sealed class JobContext
{
    /// <summary>--job 以降に渡された任意のオプション(key=value)。</summary>
    public required IReadOnlyDictionary<string, string> Options { get; init; }

    /// <summary>
    /// オプション値を取得する。未指定時は <paramref name="defaultValue"/>。
    /// </summary>
    public string Get(string key, string defaultValue = "")
        => Options.TryGetValue(key, out string? value) ? value : defaultValue;

    /// <summary>必須オプション値を取得する。未指定時は例外。</summary>
    public string Require(string key)
        => Options.TryGetValue(key, out string? value) && value.Length > 0
            ? value
            : throw new ArgumentException($"必須オプション --{key} が指定されていません。");
}
