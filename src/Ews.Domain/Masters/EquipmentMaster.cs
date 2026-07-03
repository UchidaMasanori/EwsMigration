using Ews.Domain.Common;

namespace Ews.Domain.Masters;

/// <summary>
/// 機器マスター。
///
/// 【C原典】
///   - 構造体: struct FYDM805            (toku/include/common/fydm805.h)
///   - ファイルID: FYDM805 / 編成: EWS-ISAM / レコード長: 579
///
/// 回路解析(<c>Fysk10_Main</c>)が品番・電気パラメータ・補助情報・外形寸法等を
/// 参照するためのマスター。本クラスは固定長 Shift-JIS レコードと SQL Server の
/// EquipmentMaster テーブルの双方にマッピングされる。
///
/// 本パイロットでは主キーと識別系フィールドのみを型付けし、その他の領域は
/// <see cref="RawRecord"/> としてバイト列を保持して段階的に展開する。
/// </summary>
public sealed class EquipmentMaster : IIsamRecord
{
    /// <summary>【C原典】fydm805.h コメント「ﾚｺｰﾄﾞ長 579」。</summary>
    public static int RecordLength => 579;

    // ---- PRIMARY キー(struct p805_key pkey = yoyaku + mkcd + ptype + teikkey) ----

    /// <summary>予約語。【C原典】pkey.yoyaku[8] (CHAR[8])。</summary>
    public string ReservedWord { get; set; } = string.Empty;

    /// <summary>メーカーコード。【C原典】pkey.mkcd[3] (CHAR[3])。</summary>
    public string MakerCode { get; set; } = string.Empty;

    /// <summary>パラメータタイプ。【C原典】pkey.ptype[7][7] (CHAR[7][7]=49バイト)。</summary>
    public string ParameterType { get; set; } = string.Empty;

    /// <summary>定格キー。【C原典】pkey.teikkey[80] (CHAR[80])。</summary>
    public string RatingKey { get; set; } = string.Empty;

    // ---- ALTERNATE キー1 ----

    /// <summary>品番(電算品番5桁+3桁)。【C原典】hinban[15] (CHAR[15])。</summary>
    public string PartNumber { get; set; } = string.Empty;

    // ---- ALTERNATE キー2 ----

    /// <summary>品名。【C原典】hinmei[25] (CHAR[25])。</summary>
    public string PartName { get; set; } = string.Empty;

    /// <summary>電気パラメータ文字列(タイプパラメータを除く)。【C原典】pstring[64] (CHAR[64])。</summary>
    public string ElectricalParameters { get; set; } = string.Empty;

    /// <summary>未展開領域を含むレコード全体のバイト列(Shift-JIS, 固定長)。</summary>
    public byte[] RawRecord { get; set; } = [];

    // バイトオフセット(struct p805_key と後続フィールドから算出)
    private const int OffsetReservedWord = 0;   // yoyaku[8]
    private const int OffsetMakerCode = 8;      // mkcd[3]
    private const int OffsetParameterType = 11; // ptype[7][7]=49
    private const int OffsetRatingKey = 60;     // teikkey[80]
    // pkey は 140 バイト(0..139)
    private const int OffsetPartNumber = 140;   // hinban[15]
    private const int OffsetPartName = 155;     // hinmei[25]
    private const int OffsetElectricalParameters = 180; // pstring[64]

    /// <summary>
    /// 固定長 Shift-JIS レコードからドメインモデルを生成する。
    /// 【C原典】機器マスター読込(FyIsamRead で取得した struct FYDM805)。
    /// </summary>
    public static EquipmentMaster FromFixedRecord(ReadOnlySpan<byte> record)
    {
        return new EquipmentMaster
        {
            ReservedWord = FixedFieldCodec.ReadText(record, OffsetReservedWord, 8),
            MakerCode = FixedFieldCodec.ReadText(record, OffsetMakerCode, 3),
            ParameterType = FixedFieldCodec.ReadText(record, OffsetParameterType, 49),
            RatingKey = FixedFieldCodec.ReadText(record, OffsetRatingKey, 80),
            PartNumber = FixedFieldCodec.ReadText(record, OffsetPartNumber, 15),
            PartName = FixedFieldCodec.ReadText(record, OffsetPartName, 25),
            ElectricalParameters = FixedFieldCodec.ReadText(record, OffsetElectricalParameters, 64),
            RawRecord = record.ToArray(),
        };
    }
}
