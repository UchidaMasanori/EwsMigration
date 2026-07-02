namespace Ews.Domain.Analysis;

/// <summary>
/// 主回路エリア(回路解析の出力 = 主回路1データ分の解析結果)。
///
/// 【C原典】
///   - 構造体: struct FYRT800            (toku/include/common/fyrt800.h)
///   - ファイルID: FYRT800「主回路エリア」/ メイン定義は struct fydf806 を内包。
///
/// FYRT 系はファイル固定長レコードというより、回路解析中のメモリ上の作業/結果領域
/// (DOUBLE を含む積算エリア sk_work)である。本クラスは解析結果を保持する POCO とし、
/// 永続化が必要な項目のみ後段で複合回路ファイル(FYDF807)等へ書き出す。
/// </summary>
public sealed class MainCircuitResult
{
    /// <summary>データ追番 001～(予約語毎の追番)。【C原典】FYRT800.datano[3]。</summary>
    public string SequenceNumber { get; set; } = string.Empty;

    /// <summary>主回路データ定義部。【C原典】struct syukairo dt。</summary>
    public MainCircuitData Data { get; set; } = new();

    /// <summary>積算用ワークエリア。【C原典】struct sk_work wk。</summary>
    public CircuitWork Work { get; set; } = new();
}

/// <summary>
/// 主回路データ定義部のプレースホルダ。
/// 【C原典】struct syukairo (toku/include/common/ 配下) ? 段階移植のため要素は順次追加。
/// </summary>
public sealed class MainCircuitData
{
    /// <summary>未展開の主回路データ(Shift-JIS 固定長)を一時保持。</summary>
    public byte[] RawRecord { get; set; } = [];
}

/// <summary>
/// 回路解析の積算用ワークエリア。
/// 【C原典】struct sk_work (fyrt800.h)。
/// </summary>
public sealed class CircuitWork
{
    /// <summary>機器選定区分。【C原典】sk_work.kikiskbn。</summary>
    public char EquipmentSelectionKind { get; set; } = ' ';

    /// <summary>始動回路区分。【C原典】sk_work.startkbn。</summary>
    public char StartCircuitKind { get; set; } = ' ';

    /// <summary>設定電流値。【C原典】sk_work.setteii (DOUBLE)。</summary>
    public double SetCurrent { get; set; }

    /// <summary>機器マスター補助情報の定格容量(W, VA)。【C原典】sk_work.teiwva (DOUBLE)。</summary>
    public double RatedCapacity { get; set; }
}
