using Ews.Domain.Common;

namespace Ews.Domain.Masters;

/// <summary>
/// 物件情報(FYDF801 物件共通情報レコード)。
///
/// 【C原典】
///   - 構造体: struct FYDF801            (toku/include/common/fydf801.h)
///     ・物件共通情報 struct kyoutu       (toku/include/common/fydf801k.h)
///   - ファイルID: FYDF801 / 編成: EWS-ISAM / レコード長: 1200
///
/// キー = 依頼明細番号(依頼番号 7 + 明細番号 2)。明細番号がブランクのレコードが
/// 「物件共通情報」レコードで、'01'～'99' のレコードは「盤明細情報」(union redefines)。
/// 本クラスは物件共通情報レコードのうち、回路解析エンジン(sekkei)が参照する
/// プロパティと物件を識別する主要項目を保持する。
///
/// レコード全体レイアウト(1200 バイト):
///   ・ヘッダ(キー/図番/データ属性) 50 バイト (offset 0-49)
///   ・物件共通情報 union(struct kyoutu) 1050 バイト (offset 50-1099)
///   ・データログ情報(新規登録/最終変更/転送) 100 バイト (offset 1100-1199)
///
/// エンジンが実際に参照するのは <see cref="FrequencyKind"/>(hzkbn) と
/// <see cref="ManufacturingSpecKind"/>(sshiykbn)。その他は物件識別・仕様表示用。
/// 未モデル化の作図系フィールド(塗装/ボックス/その他画面情報 etcgamen 等)は
/// 実データ検証フェーズで必要に応じて追加する。
/// </summary>
public sealed class ProjectInformation : IIsamRecord
{
    /// <summary>【C原典】fydf801.h ファイルレイアウト「1200」。</summary>
    public static int RecordLength => 1200;

    // ---- キー(依頼明細番号) ----

    /// <summary>依頼番号(営業所コード 2 + 番号 5)。【C原典】key.im.eigyocd[2]+filler1[5] (7)。</summary>
    public string RequestNumber { get; set; } = string.Empty;

    /// <summary>明細番号 ' '(物件共通) / '01'～'99'(盤明細)。【C原典】key.meisaino[2]。</summary>
    public string DetailNumber { get; set; } = string.Empty;

    // ---- 図番 ----

    /// <summary>図番(上 10 桁)。【C原典】zubanu10[10]。</summary>
    public string DrawingNumberUpper { get; set; } = string.Empty;

    /// <summary>図番(下 5 桁)。【C原典】zubanl5[5]。</summary>
    public string DrawingNumberLower { get; set; } = string.Empty;

    // ---- 物件共通情報(struct kyoutu, union offset 50 起点) ----

    /// <summary>営業所名。【C原典】com.kyo.eigyonm[30]。</summary>
    public string SalesOfficeName { get; set; } = string.Empty;

    /// <summary>担当者名。【C原典】com.kyo.tantonm[14]。</summary>
    public string StaffName { get; set; } = string.Empty;

    /// <summary>件名１。【C原典】com.kyo.kenmei1[30]。</summary>
    public string ProjectName1 { get; set; } = string.Empty;

    /// <summary>件名２。【C原典】com.kyo.kenmei2[30]。</summary>
    public string ProjectName2 { get; set; } = string.Empty;

    /// <summary>
    /// 製作仕様区分。【C原典】com.kyo.sshiykbn[2]。
    /// '01':河村標準 '02':建設省(準拠) '03':建設省(地建) '04':郵政省 '05':東京都
    /// '07'～'09':公団 '99':その他。<b>回路解析エンジンが改訂分岐で参照する。</b>
    /// </summary>
    public string ManufacturingSpecKind { get; set; } = string.Empty;

    /// <summary>仕様名称。【C原典】com.kyo.shiyonm[34]。</summary>
    public string SpecificationName { get; set; } = string.Empty;

    /// <summary>
    /// 図面種別。【C原典】com.kyo.zumenkbn。
    /// '1':承認図 '2':打合せ図 '3':決定図 '4':完成図 '5':製作図。
    /// </summary>
    public string DrawingKind { get; set; } = string.Empty;

    /// <summary>図面ランク 1:K 2:A 3:B' 4:B。【C原典】com.kyo.zumenrnk。</summary>
    public string DrawingRank { get; set; } = string.Empty;

    /// <summary>
    /// 周波数区分。【C原典】com.kyo.hzkbn。
    /// '1':50Hz '2':60Hz '3':50/60Hz。<b>回路解析エンジンが回路周波数決定で参照する。</b>
    /// </summary>
    public string FrequencyKind { get; set; } = string.Empty;

    // ---- バイトオフセット ----
    // ヘッダ 50 バイト(0-49) + 物件共通情報 union 1050 バイト(50-1099) + データログ 100(1100-1199)。
    private const int OffsetRequestNumber = 0;         // key.im (eigyocd[2]+filler1[5])
    private const int OffsetDetailNumber = 7;          // key.meisaino[2]
    private const int OffsetDrawingNumberUpper = 9;    // zubanu10[10]
    private const int OffsetDrawingNumberLower = 19;   // zubanl5[5]
    private const int UnionBase = 50;                  // 物件共通情報 union 開始
    private const int OffsetSalesOfficeName = UnionBase + 5;         // kyo.eigyonm[30]  → 55
    private const int OffsetStaffName = UnionBase + 35;              // kyo.tantonm[14]  → 85
    private const int OffsetProjectName1 = UnionBase + 49;           // kyo.kenmei1[30]  → 99
    private const int OffsetProjectName2 = UnionBase + 79;           // kyo.kenmei2[30]  → 129
    private const int OffsetManufacturingSpecKind = UnionBase + 109; // kyo.sshiykbn[2]  → 159
    private const int OffsetSpecificationName = UnionBase + 111;     // kyo.shiyonm[34]  → 161
    private const int OffsetDrawingKind = UnionBase + 145;           // kyo.zumenkbn     → 195
    private const int OffsetDrawingRank = UnionBase + 146;           // kyo.zumenrnk     → 196
    private const int OffsetFrequencyKind = UnionBase + 149;         // kyo.hzkbn        → 199

    /// <summary>
    /// 固定長 Shift-JIS レコード(1200 バイト)からドメインモデルを生成する。
    /// 【C原典】物件情報読込(FyIsamRead で取得した struct FYDF801)。
    /// </summary>
    public static ProjectInformation FromFixedRecord(ReadOnlySpan<byte> record)
    {
        return new ProjectInformation
        {
            RequestNumber = FixedFieldCodec.ReadText(record, OffsetRequestNumber, 7),
            DetailNumber = FixedFieldCodec.ReadText(record, OffsetDetailNumber, 2),
            DrawingNumberUpper = FixedFieldCodec.ReadText(record, OffsetDrawingNumberUpper, 10),
            DrawingNumberLower = FixedFieldCodec.ReadText(record, OffsetDrawingNumberLower, 5),
            SalesOfficeName = FixedFieldCodec.ReadText(record, OffsetSalesOfficeName, 30),
            StaffName = FixedFieldCodec.ReadText(record, OffsetStaffName, 14),
            ProjectName1 = FixedFieldCodec.ReadText(record, OffsetProjectName1, 30),
            ProjectName2 = FixedFieldCodec.ReadText(record, OffsetProjectName2, 30),
            ManufacturingSpecKind = FixedFieldCodec.ReadText(record, OffsetManufacturingSpecKind, 2),
            SpecificationName = FixedFieldCodec.ReadText(record, OffsetSpecificationName, 34),
            DrawingKind = FixedFieldCodec.ReadText(record, OffsetDrawingKind, 1),
            DrawingRank = FixedFieldCodec.ReadText(record, OffsetDrawingRank, 1),
            FrequencyKind = FixedFieldCodec.ReadText(record, OffsetFrequencyKind, 1),
        };
    }
}
