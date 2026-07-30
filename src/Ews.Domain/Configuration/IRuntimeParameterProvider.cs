namespace Ews.Domain.Configuration;

/// <summary>
/// 実行時パラメータ(旧 AIX の環境変数 ZONECD 等)の取得を抽象化する。
/// 【C原典】各プログラムが <c>getenv("ZONECD")</c> 等で直接参照していた OS 環境変数を、
/// OS 非依存の設定源(設定ファイル)へ集約するための境界。
/// </summary>
public interface IRuntimeParameterProvider
{
    /// <summary>指定名のパラメータ値を取得する。未定義は null。【C原典】getenv(name)。</summary>
    string? GetValue(string name);

    /// <summary>地区(工場)コード。【C原典】getenv("ZONECD") / FyGetZoneCD()。未定義は空文字。</summary>
    string ZoneCode { get; }
}
