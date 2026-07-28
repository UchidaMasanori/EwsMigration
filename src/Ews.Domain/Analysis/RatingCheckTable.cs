namespace Ews.Domain.Analysis;

/// <summary>
/// 予約語(機器種別)別の定格値チェック情報。【C原典】<c>TCHI_TBL</c>
/// (toku/include/sekkei/fyrt814.h:39、テーブル実体 <c>tchi_tbl[]</c> は fyrt817.h:777)。
///
/// 直近上下位参照ファイル(FYDF812)検索の制御情報を保持する。C 原典のフィールド並びは
/// <c>{yoyaku[8], proc_no, cpsize, flag, seten, tchi_t*}</c>。
/// <c>Fysk01_Chokkin_Read_Check</c> はこの情報で検索方式(通常/TM・THSW)・前方一致サイズ・
/// 定格値チェック種別・接点計算要否を切り替える。
/// </summary>
public sealed class RatingCheckTable
{
    /// <summary>予約語(機器種別)。【C原典】yoyaku[8]。</summary>
    public string ReservedWord { get; }

    /// <summary>プロセス番号(検索方式の分岐に用いる)。【C原典】proc_no。PC_5/PC_6 で TM/THSW 専用検索。</summary>
    public short ProcessNumber { get; }

    /// <summary>入力なし時の前方一致有効サイズ(kteichi 側)。【C原典】cpsize。</summary>
    public short ReadSize { get; }

    /// <summary>定格値チェック種別。【C原典】flag。0=通常 1～13=特殊予約語。</summary>
    public short Flag { get; }

    /// <summary>接点計算要否(制御回路のみ)。【C原典】seten。0=しない !=0=する。</summary>
    public short ContactCalculation { get; }

    /// <summary>定格値展開情報(項目テーブル)。【C原典】tchi_t(TCHI_T*)。</summary>
    public RatingKeyTableEntry[] Entries { get; }

    /// <summary>TCHI_TBL の 1 レコードを生成する。</summary>
    public RatingCheckTable(
        string reservedWord,
        short processNumber,
        short readSize,
        short flag,
        short contactCalculation,
        RatingKeyTableEntry[] entries)
    {
        ReservedWord = reservedWord;
        ProcessNumber = processNumber;
        ReadSize = readSize;
        Flag = flag;
        ContactCalculation = contactCalculation;
        Entries = entries;
    }
}
