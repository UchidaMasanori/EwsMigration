using Ews.Domain.Common;

namespace Ews.Domain.Masters;

/// <summary>
/// 機器マスター品番索引。
///
/// 【C原典】
///   - 構造体: struct FYDF816            (toku/include/common/fydf816.h)
///   - ファイルID: FYDF816 / 編成: EWS-ISAM / レコード長: 184
///
/// キー = 品番(hinban) + データ追番(datano)。同一品番に追番(0001,0002,…)で複数
/// レコードを持ち、それぞれが機器マスター(FYDM805)の PRIMARY キー(pkey)を指す。
/// 品番読み(<c>FyMasFYDM805ByHinban</c>)はこの索引を追番順に走査して機器マスターを引く。
/// </summary>
public sealed class EquipmentPartNumberIndex : IIsamRecord
{
    /// <summary>【C原典】fydf816.h コメント「ﾚｺｰﾄﾞ長 184」。</summary>
    public static int RecordLength => 184;

    // ---- キー(struct key = hinban + datano) ----

    /// <summary>品番。【C原典】key.hinban[15] (CHAR[15])。</summary>
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>データ追番(0001,0002,…)。【C原典】key.datano[4] (CHAR[4])。</summary>
    public string DataNo { get; set; } = string.Empty;

    // ---- 機器マスター PRIMARY キー(struct p805_key pkey) ----

    /// <summary>予約語。【C原典】pkey.yoyaku[8] (CHAR[8])。</summary>
    public string ReservedWord { get; set; } = string.Empty;

    /// <summary>メーカーコード。【C原典】pkey.mkcd[3] (CHAR[3])。</summary>
    public string MakerCode { get; set; } = string.Empty;

    /// <summary>パラメータタイプ。【C原典】pkey.ptype[7][7] (CHAR[7][7]=49バイト)。</summary>
    public string ParameterType { get; set; } = string.Empty;

    /// <summary>定格キー。【C原典】pkey.teikkey[80] (CHAR[80])。</summary>
    public string RatingKey { get; set; } = string.Empty;

    /// <summary>品名。【C原典】hinmei[25] (CHAR[25])。</summary>
    public string PartName { get; set; } = string.Empty;

    // バイトオフセット(struct FYDF816 = key(19) + pkey(140) + hinmei(25))
    private const int OffsetPartNumber = 0;      // key.hinban[15]
    private const int OffsetDataNo = 15;         // key.datano[4]
    private const int OffsetReservedWord = 19;   // pkey.yoyaku[8]
    private const int OffsetMakerCode = 27;      // pkey.mkcd[3]
    private const int OffsetParameterType = 30;  // pkey.ptype[7][7]=49
    private const int OffsetRatingKey = 79;      // pkey.teikkey[80]
    private const int OffsetPartName = 159;      // hinmei[25]

    /// <summary>
    /// 固定長 Shift-JIS レコード(184バイト)からドメインモデルを生成する。
    /// 【C原典】品番索引読込(FyIsamRead で取得した struct FYDF816)。
    /// </summary>
    public static EquipmentPartNumberIndex FromFixedRecord(ReadOnlySpan<byte> record)
    {
        return new EquipmentPartNumberIndex
        {
            PartNumber = FixedFieldCodec.ReadText(record, OffsetPartNumber, 15),
            DataNo = FixedFieldCodec.ReadText(record, OffsetDataNo, 4),
            ReservedWord = FixedFieldCodec.ReadText(record, OffsetReservedWord, 8),
            MakerCode = FixedFieldCodec.ReadText(record, OffsetMakerCode, 3),
            ParameterType = FixedFieldCodec.ReadText(record, OffsetParameterType, 49),
            RatingKey = FixedFieldCodec.ReadText(record, OffsetRatingKey, 80),
            PartName = FixedFieldCodec.ReadText(record, OffsetPartName, 25),
        };
    }
}
