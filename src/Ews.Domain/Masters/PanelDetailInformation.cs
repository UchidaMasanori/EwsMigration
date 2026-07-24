using Ews.Domain.Common;

namespace Ews.Domain.Masters;

/// <summary>
/// 盤明細情報(FYDF801 盤明細情報レコード)。
///
/// 【C原典】
///   - 構造体: struct FYDF801            (toku/include/common/fydf801.h)
///     ・盤明細情報 struct bmeisai        (toku/include/common/fydf801m.h)
///   - ファイルID: FYDF801 / 編成: EWS-ISAM / レコード長: 1200
///
/// FYDF801 の union <c>com</c> は「物件共通情報(kyoutu)」と「盤明細情報(bmeisai)」の
/// REDEFINES で、明細番号(meisaino)がブランクのレコードは物件共通情報
/// (<see cref="ProjectInformation"/>)、'01'～'99'(実データには '0A'～'0D' もあり)の
/// レコードが盤明細情報である。本クラスは盤明細情報レコードの主要項目を保持する。
///
/// レコード全体レイアウト(1200 バイト)はヘッダ 50 + union 1050 + データログ 100。
/// bmeisai の各フィールドは union 起点(offset 50)からの相対位置に配置される。
///
/// 回路解析(Fyss12)の SEP 追加判定 <c>PropChkSEPBox</c> は
/// <see cref="BoxDepth"/>(boxsund, ボックスフカサ)を参照する。
/// 未モデル化の作図系フィールド(塗装/取付/太陽光等)は必要に応じて追加する。
/// </summary>
public sealed class PanelDetailInformation : IIsamRecord
{
    /// <summary>【C原典】fydf801.h ファイルレイアウト「1200」。</summary>
    public static int RecordLength => 1200;

    // ---- キー(依頼明細番号) ----

    /// <summary>依頼番号(営業所コード 2 + 番号 5)。【C原典】key.im (7)。</summary>
    public string RequestNumber { get; set; } = string.Empty;

    /// <summary>明細番号 '01'～'99'(実データに '0A'～'0D' もあり)。【C原典】key.meisaino[2]。</summary>
    public string DetailNumber { get; set; } = string.Empty;

    // ---- 盤明細情報(struct bmeisai, union offset 50 起点) ----

    /// <summary>盤名称１。【C原典】com.mei.bannm[30]。</summary>
    public string PanelName { get; set; } = string.Empty;

    /// <summary>盤名称２。【C原典】com.mei.bannmkng[30]。</summary>
    public string PanelNameKana { get; set; } = string.Empty;

    /// <summary>
    /// 標準・コンポ盤選定区分。【C原典】com.mei.hycpskbn。
    /// '1':自動 '2':手動 '3':特注盤 '4':標準図 '5':アラーム盤 '6':太陽光 '7':ブロックコンポ 'P':集電箱。
    /// </summary>
    public string StandardCompoSelectionKind { get; set; } = string.Empty;

    /// <summary>数量。【C原典】com.mei.suuryo[2]。</summary>
    public string Quantity { get; set; } = string.Empty;

    /// <summary>ボックス品番。【C原典】com.mei.boxhinbn[15]。</summary>
    public string BoxPartNumber { get; set; } = string.Empty;

    /// <summary>ボックスタイプ。【C原典】com.mei.boxtype[8]。</summary>
    public string BoxType { get; set; } = string.Empty;

    /// <summary>ボックス寸法 タテ(mm)。【C原典】com.mei.boxsunh[5]。</summary>
    public string BoxHeight { get; set; } = string.Empty;

    /// <summary>ボックス寸法 ヨコ(mm)。【C原典】com.mei.boxsunw[5]。</summary>
    public string BoxWidth { get; set; } = string.Empty;

    /// <summary>
    /// ボックス寸法 フカサ(mm)。【C原典】com.mei.boxsund[5]。
    /// <b>Fyss12 の SEP 作図 BOX チェック(PropChkSEPBox)が参照する(JBR/JOC かつ深さ 350 が対象)。</b>
    /// </summary>
    public string BoxDepth { get; set; } = string.Empty;

    // ---- バイトオフセット ----
    // ヘッダ 50 バイト(0-49) + 盤明細情報 union(50-1099)。union 内相対位置に UnionBase を加算。
    private const int OffsetRequestNumber = 0;   // key.im (eigyocd[2]+filler1[5])
    private const int OffsetDetailNumber = 7;    // key.meisaino[2]
    private const int UnionBase = 50;            // 盤明細情報 union 開始
    private const int OffsetPanelName = UnionBase + 5;         // mei.bannm[30]     → 55
    private const int OffsetPanelNameKana = UnionBase + 35;    // mei.bannmkng[30]  → 85
    private const int OffsetStandardCompoSelectionKind = UnionBase + 65; // mei.hycpskbn → 115
    private const int OffsetQuantity = UnionBase + 66;         // mei.suuryo[2]     → 116
    private const int OffsetBoxPartNumber = UnionBase + 202;   // mei.boxhinbn[15]  → 252
    private const int OffsetBoxType = UnionBase + 217;         // mei.boxtype[8]    → 267
    private const int OffsetBoxHeight = UnionBase + 225;       // mei.boxsunh[5]    → 275
    private const int OffsetBoxWidth = UnionBase + 230;        // mei.boxsunw[5]    → 280
    private const int OffsetBoxDepth = UnionBase + 235;        // mei.boxsund[5]    → 285

    /// <summary>
    /// 固定長 Shift-JIS レコード(1200 バイト)からドメインモデルを生成する。
    /// 【C原典】盤明細情報読込(FyIsamRead で取得した struct FYDF801, meisaino 非ブランク)。
    /// </summary>
    public static PanelDetailInformation FromFixedRecord(ReadOnlySpan<byte> record)
    {
        return new PanelDetailInformation
        {
            RequestNumber = FixedFieldCodec.ReadText(record, OffsetRequestNumber, 7),
            DetailNumber = FixedFieldCodec.ReadText(record, OffsetDetailNumber, 2),
            PanelName = FixedFieldCodec.ReadText(record, OffsetPanelName, 30),
            PanelNameKana = FixedFieldCodec.ReadText(record, OffsetPanelNameKana, 30),
            StandardCompoSelectionKind = FixedFieldCodec.ReadText(record, OffsetStandardCompoSelectionKind, 1),
            Quantity = FixedFieldCodec.ReadText(record, OffsetQuantity, 2),
            BoxPartNumber = FixedFieldCodec.ReadText(record, OffsetBoxPartNumber, 15),
            BoxType = FixedFieldCodec.ReadText(record, OffsetBoxType, 8),
            BoxHeight = FixedFieldCodec.ReadText(record, OffsetBoxHeight, 5),
            BoxWidth = FixedFieldCodec.ReadText(record, OffsetBoxWidth, 5),
            BoxDepth = FixedFieldCodec.ReadText(record, OffsetBoxDepth, 5),
        };
    }
}
