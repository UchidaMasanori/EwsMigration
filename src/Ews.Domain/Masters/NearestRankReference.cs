using Ews.Domain.Common;

namespace Ews.Domain.Masters;

/// <summary>
/// 直近上下位参照ファイル(機器選定の候補検索マスタ)。
///
/// 【C原典】
///   - 構造体: struct FYDF812            (toku/include/common/fydf812.h)
///   - ファイルID: FYDF812 / 編成: EWS-ISAM / レコード長: 300
///   - 検索: Fysk01_Chokkin_Read_Check(_ALL/_TMS)(toku/sekkei/src/Fysk01.c)が
///           FyIsamGStartR/FyIsamGNextR で走査し、キー先頭 <c>siz</c> バイト
///           (=予約語+メーカー+パラメータタイプ+電源区分+定格値の先頭)を
///           <c>memcmp</c> で前方一致検索して候補を絞り込む。
///
/// レガシーでは機器マスタ選定の直前に、要求仕様(予約語・変換形状タイプ・定格値キー)で
/// この ISAM を検索し、該当する実機器の定格キー(teikkey)・品名(hinmei)・
/// ハンドルロック区分(hlkbn)を得る。ep[2] の最終確定もこの検索結果に基づく。
///
/// 本移行では、まず検索の同定に使う KEY 部と、候補判定で直接参照する外側フィールド
/// (ハンドルロック区分・機器マスタ定格キー・品名・制御電圧適応範囲)を型付けする。
/// 共用情報部(kyoyojg: 主/制御電源共用区分・感度電流・各電圧)は、数値変換
/// <c>Fysk01_Change_Chokin</c> を移植する後続増分で追加する。
/// </summary>
public sealed class NearestRankReference : IIsamRecord
{
    /// <summary>【C原典】fydf812.h コメント「ﾚｺｰﾄﾞ長 300」。</summary>
    public static int RecordLength => 300;

    /// <summary>パラメータタイプ枠数。【C原典】key.tjg.ptype[7][7]。</summary>
    public const int ParameterTypeSlotCount = 7;

    // ---- KEY 部(struct reckeyc) ----

    /// <summary>予約語。【C原典】key.yoyaku[8]。</summary>
    public string ReservedWord { get; set; } = string.Empty;

    /// <summary>メーカーコード。【C原典】key.mkcd[3]。</summary>
    public string MakerCode { get; set; } = string.Empty;

    /// <summary>パラメータタイプ(7 枠)。【C原典】key.tjg.ptype[7][7]。</summary>
    public IReadOnlyList<string> ParameterTypes { get; set; } = [];

    /// <summary>主電源 AC/DC 区分。【C原典】key.sadkbn。</summary>
    public char MainPowerAcDc { get; set; } = ' ';

    /// <summary>制御電源 AC/DC 区分。【C原典】key.cadkbn。</summary>
    public char ControlPowerAcDc { get; set; } = ' ';

    /// <summary>
    /// 定格値キー。【C原典】key.kteichi[50]。
    /// Fysk04_Make_Teikakuchi(=<c>RatingKeyBuilder.MakeRatingKey</c>)が生成する
    /// 定格値キーと同形式で、検索の前方一致と定格値チェックに用いる。
    /// </summary>
    public string RatingKey { get; set; } = string.Empty;

    /// <summary>データ追番。【C原典】key.datano[4]。</summary>
    public string DataSequence { get; set; } = string.Empty;

    // ---- 外側フィールド(struct FYDF812) ----

    /// <summary>ハンドルロック区分('H'=ハンドルロック有)。【C原典】hlkbn。</summary>
    public char HandleLockKind { get; set; } = ' ';

    /// <summary>機器マスタ定格キー(選定結果として機器マスタ検索に渡すキー)。【C原典】teikkey[80]。</summary>
    public string EquipmentMasterRatingKey { get; set; } = string.Empty;

    /// <summary>品名。【C原典】hinmei[25]。</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>制御電圧適応範囲(from)。【C原典】vcfrom[3]。</summary>
    public string ControlVoltageRangeFrom { get; set; } = string.Empty;

    /// <summary>制御電圧適応範囲(to)。【C原典】vcto[3]。</summary>
    public string ControlVoltageRangeTo { get; set; } = string.Empty;

    // ---- バイトオフセット(struct FYDF812 先頭から算出) ----
    private const int OffsetReservedWord = 0;            // key.yoyaku[8]
    private const int OffsetMakerCode = 8;               // key.mkcd[3]
    private const int OffsetParameterTypes = 11;         // key.tjg.ptype[7][7]
    private const int ParameterTypeSize = 7;             // ptype[i][7]
    private const int OffsetMainPowerAcDc = 60;          // key.sadkbn
    private const int OffsetControlPowerAcDc = 61;       // key.cadkbn
    private const int OffsetRatingKey = 62;              // key.kteichi[50]
    private const int OffsetDataSequence = 112;          // key.datano[4]
    // 共用情報部 jg[59] = 116..174(ksadkbn/kcadkbn/kyoma[16]/kv1[11]/kv2[15]/kvc[15]) → 後続増分
    private const int OffsetHandleLockKind = 175;        // hlkbn
    private const int OffsetEquipmentMasterRatingKey = 176; // teikkey[80]
    private const int OffsetProductName = 256;           // hinmei[25]
    private const int OffsetControlVoltageRangeFrom = 281; // vcfrom[3]
    private const int OffsetControlVoltageRangeTo = 284;   // vcto[3]
    // filler[13] = 287..299

    /// <summary>
    /// 固定長 Shift-JIS レコードからドメインモデルを生成する。
    /// 【C原典】FyIsamGStartR/FyIsamGNextR で取得した struct FYDF812。
    /// </summary>
    public static NearestRankReference FromFixedRecord(ReadOnlySpan<byte> record)
    {
        var types = new string[ParameterTypeSlotCount];
        for (int i = 0; i < ParameterTypeSlotCount; i++)
        {
            types[i] = FixedFieldCodec.ReadText(record, OffsetParameterTypes + (i * ParameterTypeSize), ParameterTypeSize);
        }

        return new NearestRankReference
        {
            ReservedWord = FixedFieldCodec.ReadText(record, OffsetReservedWord, 8),
            // メーカーコードは "M  " など後続空白に意味があるため生 3 文字を保持する。
            MakerCode = FixedFieldCodec.ShiftJis.GetString(record.Slice(OffsetMakerCode, 3)),
            ParameterTypes = types,
            MainPowerAcDc = (char)record[OffsetMainPowerAcDc],
            ControlPowerAcDc = (char)record[OffsetControlPowerAcDc],
            RatingKey = FixedFieldCodec.ReadText(record, OffsetRatingKey, 50),
            DataSequence = FixedFieldCodec.ReadText(record, OffsetDataSequence, 4),
            HandleLockKind = (char)record[OffsetHandleLockKind],
            EquipmentMasterRatingKey = FixedFieldCodec.ReadText(record, OffsetEquipmentMasterRatingKey, 80),
            ProductName = FixedFieldCodec.ReadText(record, OffsetProductName, 25),
            ControlVoltageRangeFrom = FixedFieldCodec.ReadText(record, OffsetControlVoltageRangeFrom, 3),
            ControlVoltageRangeTo = FixedFieldCodec.ReadText(record, OffsetControlVoltageRangeTo, 3),
        };
    }
}
