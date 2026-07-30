namespace Ews.Domain.Configuration;

/// <summary>
/// 実行時パラメータ名(旧 AIX の環境変数名)の定数集約。
/// 【C原典】各プログラムの <c>getenv("...")</c> で使用されていた環境変数名。
/// </summary>
public static class RuntimeParameterNames
{
    /// <summary>地区(工場)コード。【C原典】"ZONECD"。</summary>
    public const string ZoneCode = "ZONECD";

    /// <summary>ログインホスト。【C原典】"LHOST"。</summary>
    public const string LoginHost = "LHOST";

    /// <summary>端末 ID。【C原典】"TERMID"。</summary>
    public const string TerminalId = "TERMID";

    /// <summary>シンボル ID。【C原典】"SYMID"。</summary>
    public const string SymbolId = "SYMID";

    /// <summary>ホストコンソールホスト。【C原典】"HCONHOST"。</summary>
    public const string HostConsoleHost = "HCONHOST";

    /// <summary>自動テストフラグ。【C原典】"AUTO_TEST"。</summary>
    public const string AutoTest = "AUTO_TEST";

    /// <summary>情報ファイルパス。【C原典】"INFPATH"。</summary>
    public const string InfPath = "INFPATH";

    /// <summary>データファイル。【C原典】"DATAFILE"。</summary>
    public const string DataFile = "DATAFILE";

    /// <summary>ログファイル。【C原典】"LOGFILE"。</summary>
    public const string LogFile = "LOGFILE";

    /// <summary>ログ出力フラグ。【C原典】"LOGFLAG"。</summary>
    public const string LogFlag = "LOGFLAG";

    /// <summary>ファイルパス。【C原典】"FILEPATH"。</summary>
    public const string FilePath = "FILEPATH";

    /// <summary>グループ名。【C原典】"GNAME"。</summary>
    public const string GroupName = "GNAME";
}
