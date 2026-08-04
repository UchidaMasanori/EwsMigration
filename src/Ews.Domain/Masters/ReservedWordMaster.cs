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

    /// <summary>
    /// 機器大分類。【C原典】data->kikirui → YO_TABLE.kikirui。
    /// SortIndex の KEY1 で使用('1'→機器種別'1' / それ以外→'2')。
    /// </summary>
    public char Kikirui { get; set; } = ' ';

    /// <summary>
    /// タイプ枠(7 枠)。【C原典】YO_TABLE.typetjg[7]。
    /// SortIndex の KEY9(付属機能)で有効インデックス数とパラメータタイプ記号を走査する。
    /// </summary>
    public IReadOnlyList<ReservedWordTypeSlot> TypeSlots { get; set; } = [];

    /// <summary>パラメータタイプ記号の登録数。【C原典】PARMKGO_NUM(改訂&lt;2&gt; = 40)。</summary>
    public const int ParameterTypeCount = 40;

    // バイトオフセット(struct FYDF810 先頭から算出)
    private const int OffsetReservedWord = 0;   // key.yoyaku[8]
    // key(8) + yoyaknm[30] + ybuhcd[7] = 45、kg[20](各 70 バイト)= 1400 → tg 開始 = 1445
    private const int OffsetTypeTable = 1445;
    // typetjg 1 件 = typname[20] + ksenkbn(1) + yukoidx[2] + pdflt4[2]
    //             + po[40](各 ptype[7]+ptypenm[40]=47) + filler[10] = 1915
    private const int TypeTableEntrySize = 1915;
    // typetjg 内の ksenkbn は typname[20] の直後
    private const int OffsetKsenkbnInEntry = 20;
    // ksenkbn(1) の直後が yukoidx[2](有効インデックス数、数値2桁)
    private const int OffsetYukoidxInEntry = 21;
    // typname(20)+ksenkbn(1)+yukoidx(2)+pdflt4(2) = 25 の直後から po[40] が並ぶ
    private const int OffsetParameterTypesInEntry = 25;
    // po 1 件 = ptype[7] + ptypenm[40] = 47。ptype はその先頭 7 バイト。
    private const int ParameterEntrySize = 47;
    private const int ParameterTypeWidth = 7;
    // 記述区分ブロック内 kikirui のオフセット
    //   tg 終端 = 1445 + 1915*7 = 14850、以降 hyonm[16]/hyohou(1)/kikimkbn..douskkbn(12)。
    //   14850 + 16 + 1 + 12 = 14879 → 直前の kikirui は 14878。
    private const int OffsetKikirui = 14878;

    /// <summary>
    /// 固定長 Shift-JIS レコードからドメインモデルを生成する。
    /// 【C原典】予約語ファイル読込(FyIsamSNextR で取得した struct FYDF810)。
    /// </summary>
    public static ReservedWordMaster FromFixedRecord(ReadOnlySpan<byte> record)
    {
        var kinds = new char[TypeSlotCount];
        var slots = new ReservedWordTypeSlot[TypeSlotCount];
        for (int i = 0; i < TypeSlotCount; i++)
        {
            int entry = OffsetTypeTable + (i * TypeTableEntrySize);
            // ksenkbn は ' ' または '1' の半角 1 文字(ASCII)なので生バイトを保持する。
            kinds[i] = (char)record[entry + OffsetKsenkbnInEntry];
            slots[i] = ReadTypeSlot(record, entry);
        }

        return new ReservedWordMaster
        {
            ReservedWord = FixedFieldCodec.ReadText(record, OffsetReservedWord, 8),
            SelectionElementKinds = kinds,
            Kikirui = (char)record[OffsetKikirui],
            TypeSlots = slots,
        };
    }

    /// <summary>
    /// タイプ枠 1 件を読み取る。
    /// 【C原典】Fysk08_CreYoyakuTbl: yukoidx は 2 桁数値を sscanf("%hd")、
    /// ptype[j] は po[j].ptype の 7 バイトを memcpy でパック格納する。
    /// </summary>
    private static ReservedWordTypeSlot ReadTypeSlot(ReadOnlySpan<byte> record, int entry)
    {
        // 有効インデックス数(2 桁数値)。空白時は変換されず 0 扱いとなる。
        string yukoText = FixedFieldCodec.ReadText(record, entry + OffsetYukoidxInEntry, 2);
        int yukoidx = int.TryParse(yukoText, out int v) ? v : 0;

        var ptypes = new string[ParameterTypeCount];
        for (int j = 0; j < ParameterTypeCount; j++)
        {
            int po = entry + OffsetParameterTypesInEntry + (j * ParameterEntrySize);
            // ptype は空白パディングを保持する必要があるためトリムせず生の 7 文字で保持。
            ptypes[j] = FixedFieldCodec.ShiftJis.GetString(record.Slice(po, ParameterTypeWidth));
        }

        return new ReservedWordTypeSlot
        {
            EffectiveIndexCount = yukoidx,
            ParameterTypes = ptypes,
        };
    }
}

/// <summary>
/// 予約語マスタのタイプ枠 1 件。【C原典】YO_TABLE.typetjg[i]。
/// </summary>
public sealed class ReservedWordTypeSlot
{
    /// <summary>有効インデックス数。【C原典】typetjg.yukoidx。0 で走査打ち切り。</summary>
    public int EffectiveIndexCount { get; set; }

    /// <summary>
    /// パラメータタイプ記号(40 件)。各要素は空白パディングを含む生の 7 文字。
    /// 【C原典】typetjg.ptype[PARMKGO_NUM][7](パック配列)。
    /// </summary>
    public IReadOnlyList<string> ParameterTypes { get; set; } = [];
}
