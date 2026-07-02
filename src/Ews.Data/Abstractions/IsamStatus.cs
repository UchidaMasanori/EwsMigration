namespace Ews.Data.Abstractions;

/// <summary>
/// ISAM アクセスの結果ステータス。
///
/// 【C原典】cmnisam.h のエラー定数群(ERR_*)。
/// C では LONG の負値で返却していたものを、C# では列挙体で表現する。
/// </summary>
public enum IsamStatus
{
    /// <summary>正常終了(0)。</summary>
    Ok = 0,

    /// <summary>指定データが見つからない。【C原典】ERR_ISAM_NOTHING (-10)。</summary>
    NotFound = -10,

    /// <summary>指定データは既に登録済み。【C原典】ERR_ISAM_EXIST (-11)。</summary>
    AlreadyExists = -11,

    /// <summary>インデックス登録数オーバー。【C原典】ERR_ISAM_INDEXFULL (-12)。</summary>
    IndexFull = -12,

    /// <summary>データ部が一杯。【C原典】ERR_ISAM_DATAFULL (-13)。</summary>
    DataFull = -13,

    /// <summary>データ無し(順次読込の終端)。【C原典】ERR_DATANOTHING (-9)。</summary>
    NoData = -9,

    /// <summary>レコードがロックされている。【C原典】ERR_RECLOCK (-41)。</summary>
    RecordLocked = -41,

    /// <summary>パラメータ指定エラー。【C原典】ERR_PARM (-99)。</summary>
    ParameterError = -99,
}

/// <summary>
/// 排他処理指定。【C原典】cmnisam.h の EXCLUSIVE_LOCK / EXCLUSIVE_ULOCK。
/// SQL Server 化後はトランザクション分離レベルで吸収するため、移行互換用の指定子として保持する。
/// </summary>
public enum LockMode
{
    /// <summary>ロックなし。【C原典】EXCLUSIVE_ULOCK (0)。</summary>
    Unlock = 0,

    /// <summary>ファイルロック。【C原典】EXCLUSIVE_LOCK (1)。</summary>
    Lock = 1,
}
