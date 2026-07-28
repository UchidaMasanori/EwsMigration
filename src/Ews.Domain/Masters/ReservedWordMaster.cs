using Ews.Domain.Common;

namespace Ews.Domain.Masters;

/// <summary>
/// 予約語マスタ(タイプ使用有無チェック用)。
///
/// 【C原典】
///   - 構造体: struct FYDF810            (toku/include/common/fydf810.h)
///   - ファイルID: FYDF810 / 編成: EWS-ISAM / レコード長: 14980
///   - ロード : Fysk08_Get_YoyakugoFile() → Fysk08_CreYoyakuTbl() で YO_TABLE 化
///
/// レガシーでは予約語 ISAM ファイル全体(14980 バイト)を YO_TABLE へ展開して
/// メモリ常駐させる。本移行では、まずタイプ使用有無チェック
/// (<c>Fysk08_Usetype_Check</c>)が参照する最小フィールド、すなわち予約語キーと
/// タイプ枠(7 枠)ごとの機器選定要素区分(ksenkbn)のみを型付けする。
///
/// 【Usetype_Check の仕様】指定予約語に一致するレコードを走査し、
/// タイプ枠 k(0..6)の ksenkbn が ' '(=機器選定要素でない)の場合、
/// 対応するデータタイプ枠をブランクでクリアする。
/// </summary>
public sealed class ReservedWordMaster : IIsamRecord
{
    /// <summary>【C原典】fydf810.h コメント「ﾚｺｰﾄﾞ長 14980」。</summary>
    public static int RecordLength => 14980;

    /// <summary>タイプ枠数。【C原典】struct FYDF810 の tg[7] / YO_TABLE の typetjg[7]。</summary>
    public const int TypeSlotCount = 7;

    /// <summary>予約語。【C原典】key.yoyaku[8] (CHAR[8])。</summary>
    public string ReservedWord { get; set; } = string.Empty;

    /// <summary>
    /// タイプ枠(7 枠)ごとの機器選定要素区分。
    /// 【C原典】tg[i].ksenkbn ('1':機器選定要素 / ' ':以外)。
    /// Usetype_Check は ' ' の枠のデータタイプをクリアするため、
    /// 空白を区別する必要があり、生の 1 文字をそのまま保持する。
    /// </summary>
    public IReadOnlyList<char> SelectionElementKinds { get; set; } = [];

    // バイトオフセット(struct FYDF810 先頭から算出)
    private const int OffsetReservedWord = 0;   // key.yoyaku[8]
    // key(8) + yoyaknm[30] + ybuhcd[7] = 45、kg[20](各 70 バイト)= 1400 → tg 開始 = 1445
    private const int OffsetTypeTable = 1445;
    // typetjg 1 件 = typname[20] + ksenkbn(1) + yukoidx[2] + pdflt4[2]
    //             + po[40](各 ptype[7]+ptypenm[40]=47) + filler[10] = 1915
    private const int TypeTableEntrySize = 1915;
    // typetjg 内の ksenkbn は typname[20] の直後
    private const int OffsetKsenkbnInEntry = 20;

    /// <summary>
    /// 固定長 Shift-JIS レコードからドメインモデルを生成する。
    /// 【C原典】予約語ファイル読込(FyIsamSNextR で取得した struct FYDF810)。
    /// </summary>
    public static ReservedWordMaster FromFixedRecord(ReadOnlySpan<byte> record)
    {
        var kinds = new char[TypeSlotCount];
        for (int i = 0; i < TypeSlotCount; i++)
        {
            int offset = OffsetTypeTable + (i * TypeTableEntrySize) + OffsetKsenkbnInEntry;
            // ksenkbn は ' ' または '1' の半角 1 文字(ASCII)なので生バイトを保持する。
            kinds[i] = (char)record[offset];
        }

        return new ReservedWordMaster
        {
            ReservedWord = FixedFieldCodec.ReadText(record, OffsetReservedWord, 8),
            SelectionElementKinds = kinds,
        };
    }
}
