namespace Ews.Domain.Configuration;

/// <summary>
/// メモリ上の辞書を値源とする <see cref="IRuntimeParameterProvider"/> 実装。
/// 単体テストや、設定ファイル読込後の値保持に用いる。外部依存を持たない。
/// </summary>
public sealed class InMemoryRuntimeParameterProvider : IRuntimeParameterProvider
{
    // 【C原典】getenv は名前を大文字小文字区別するため Ordinal 比較。
    private readonly Dictionary<string, string?> _values;

    public InMemoryRuntimeParameterProvider(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new Dictionary<string, string?>(values, StringComparer.Ordinal);
    }

    public string? GetValue(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _values.TryGetValue(name, out string? value) ? value : null;
    }

    public string ZoneCode => GetValue(RuntimeParameterNames.ZoneCode) ?? string.Empty;
}
