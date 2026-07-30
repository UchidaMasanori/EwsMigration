using System.Text;
using System.Text.Json;
using Ews.Domain.Configuration;

namespace Ews.Data.Configuration;

/// <summary>
/// JSON 設定ファイルを値源とする <see cref="IRuntimeParameterProvider"/> 実装。
/// 旧 AIX の環境変数(ZONECD 等)を OS ではなく設定ファイルで定義する。
///
/// 受理形式(いずれも UTF-8):
///   1. 直下オブジェクト                { "ZONECD": "78007", ... }
///   2. RuntimeParameters セクション付き { "RuntimeParameters": { "ZONECD": "78007", ... } }
/// </summary>
public sealed class FileRuntimeParameterProvider : IRuntimeParameterProvider
{
    private const string SectionName = "RuntimeParameters";

    private readonly InMemoryRuntimeParameterProvider _inner;

    private FileRuntimeParameterProvider(InMemoryRuntimeParameterProvider inner) => _inner = inner;

    /// <summary>設定ファイルを読み込みプロバイダを生成する。</summary>
    public static FileRuntimeParameterProvider LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"パラメータ設定ファイルが見つかりません: {path}", path);
        }

        string json = File.ReadAllText(path, Encoding.UTF8);
        return FromJson(json);
    }

    /// <summary>JSON 文字列からプロバイダを生成する。</summary>
    public static FileRuntimeParameterProvider FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        Dictionary<string, string?> values = Parse(json);
        return new FileRuntimeParameterProvider(new InMemoryRuntimeParameterProvider(values));
    }

    private static Dictionary<string, string?> Parse(string json)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        JsonElement section = root.TryGetProperty(SectionName, out JsonElement nested) &&
                              nested.ValueKind == JsonValueKind.Object
            ? nested
            : root;

        foreach (JsonProperty property in section.EnumerateObject())
        {
            // ネストしたオブジェクト(例: ConnectionStrings)は対象外。
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                continue;
            }

            result[property.Name] = property.Value.ValueKind == JsonValueKind.Null
                ? null
                : property.Value.ToString();
        }

        return result;
    }

    public string? GetValue(string name) => _inner.GetValue(name);

    public string ZoneCode => _inner.ZoneCode;
}
