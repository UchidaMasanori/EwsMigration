using Ews.Domain.Common;

namespace Ews.Domain.Masters;

/// <summary>
/// 機器マスター品名索引。
///
/// 【C原典】
///   - 構造体: struct FYDF817            (toku/include/common/fydf817.h)
///   - ファイルID: FYDF817 / 編成: EWS-ISAM / レコード長: 184
///
/// キー = 品名(hinmei) + データ追番(datano)。同一品名に追番(0001,0002,…)で複数
/// レコードを持ち、それぞれが機器マスター(FYDM805)の PRIMARY キー(pkey)を指す。
/// PT 機器選定(<c>Fysk01_Kikisearch_PT</c>)はこの索引を品名+追番で読み機器を引く。
/// </summary>
public sealed class EquipmentNameIndex : IIsamRecord
{
    /// <summary>【C原典】fydf817.h コメント「ﾚｺｰﾄﾞ長 184」。</summary>
    public static int RecordLength => 184;

    // ---- キー(struct key = hinmei + datano) ----

    /// <summary>品名。【C原典】key.hinmei[25] (CHAR[25])。</summary>
    public string ProductName { get; set; } = string.Empty;

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

    /// <summary>品番。【C原典】hinban[15] (CHAR[15])。</summary>
    public string PartNumber { get; set; } = string.Empty;

    // バイトオフセット(struct FYDF817 = key(29) + pkey(140) + hinban(15))
    private const int OffsetProductName = 0;      // key.hinmei[25]
    private const int OffsetDataNo = 25;          // key.datano[4]
    private const int OffsetReservedWord = 29;    // pkey.yoyaku[8]
    private const int OffsetMakerCode = 37;       // pkey.mkcd[3]
    private const int OffsetParameterType = 40;   // pkey.ptype[7][7]=49
    private const int OffsetRatingKey = 89;       // pkey.teikkey[80]
    private const int OffsetPartNumber = 169;     // hinban[15]

    /// <summary>
    /// 固定長 Shift-JIS レコード(184バイト)からドメインモデルを生成する。
    /// 【C原典】品名索引読込(FyIsamStartR で取得した struct FYDF817)。
    /// </summary>
    public static EquipmentNameIndex FromFixedRecord(ReadOnlySpan<byte> record)
    {
        return new EquipmentNameIndex
        {
            ProductName = FixedFieldCodec.ReadText(record, OffsetProductName, 25),
            DataNo = FixedFieldCodec.ReadText(record, OffsetDataNo, 4),
            ReservedWord = FixedFieldCodec.ReadText(record, OffsetReservedWord, 8),
            MakerCode = FixedFieldCodec.ReadText(record, OffsetMakerCode, 3),
            ParameterType = FixedFieldCodec.ReadText(record, OffsetParameterType, 49),
            RatingKey = FixedFieldCodec.ReadText(record, OffsetRatingKey, 80),
            PartNumber = FixedFieldCodec.ReadText(record, OffsetPartNumber, 15),
        };
    }
}
