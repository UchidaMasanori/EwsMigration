using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 末端回路ブレーカの機器選定。
/// 【C原典】Fyss3B_Breaker_Sentei(toku/sekkei/src/Fyss3B.c)。
///
/// 主回路エリア(<see cref="MainCircuitResult"/> の一覧)を走査し、負荷容量決定テーブルに
/// 存在するブレーカ系予約語の機器へ機器選定指示フラグ(<c>ksflg</c>)と機器サーチフラグ
/// (<c>kikisflg</c>)を設定してから、主回路機器サーチ(<c>Fysk00_Kikisearch_SY</c>)を呼び出す。
///
/// 【移植範囲(重要)】本移植はフラグ設定部(<see cref="PrepareSelectionFlags"/>)のみを対象とする。
/// C 原典末尾の主回路機器サーチ <c>Fysk00_Kikisearch_SY</c>(Fysk00.c, 大規模 ISAM 依存の機器選定
/// 本体)は未移植のため、その呼び出しは移植境界とする(leaf-not-wired)。機器サーチ移植後に
/// 本メソッドの後段へ結線する。
/// </summary>
public static class TerminalBreakerSelector
{
    /// <summary>
    /// 主回路機器サーチの前段としてブレーカ系機器の機器選定指示フラグ・機器サーチフラグを設定する。
    /// 【C原典】Fyss3B_Breaker_Sentei の前半 2 ループ(Fyss3B.c:106-152)。
    /// </summary>
    /// <param name="records">主回路エリア。【C原典】maina(件数 Pmainc)。</param>
    public static void PrepareSelectionFlags(IReadOnlyList<MainCircuitResult> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        // 【C原典】機器選定指示フラグ(ksflg)・機器サーチフラグ(kikisflg)をスペースクリア。
        foreach (MainCircuitResult record in records)
        {
            record.Work.SelectionInstructionFlag = ' ';
            record.Data.EquipmentSearchFlag = ' ';
        }

        foreach (MainCircuitResult record in records)
        {
            MainCircuitData dt = record.Data;

            // 【C原典】予約語を負荷容量決定テーブルで検索。未存在ならスキップ。
            if (!IsLoadCapacityBreaker(dt.ReservedWord))
            {
                continue;
            }

            // 【C原典】機器選定区分=='1' AND 外部取付区分==' ' AND 負荷種類!="  " AND 予約語!="MC"。
            if (record.Work.EquipmentSelectionKind == '1' &&
                dt.AttachedParameter.ExternalMountKind == ' ' &&
                Pad2(dt.AttachedParameter.LoadKind) != "  " &&
                Pad8(dt.ReservedWord) != "MC      ")
            {
                record.Work.SelectionInstructionFlag = '1';
                dt.EquipmentSearchFlag = '1';
            }

            // 【C原典】末端回路行種先頭機器フラグ=='1' AND 外部取付区分==' '(上と独立した if)。
            if (record.Work.LeadingEquipmentFlag == '1' &&
                dt.AttachedParameter.ExternalMountKind == ' ')
            {
                record.Work.SelectionInstructionFlag = '1';
                dt.EquipmentSearchFlag = '1';
            }
        }

        // 【C原典 移植境界】ret = Fysk00_Kikisearch_SY(...) は未移植のためここで結線しない。
    }

    /// <summary>
    /// 予約語が負荷容量決定テーブル対象(ブレーカ系 14 予約語＋MC)かを判定する。
    /// 【C原典】Fyss3B_Get_Fuka(Fyss3B.c:170)。0(存在)/1(未存在)を返す代わりに真偽で返す。
    /// C 原典 940820 で fyrt812 走査から 15 予約語の memcmp(8) 直接判定へ変更されている。
    /// </summary>
    private static bool IsLoadCapacityBreaker(string reservedWord)
    {
        return LoadCapacityBreakerWords.Contains(Pad8(reservedWord));
    }

    /// <summary>【C原典】Fyss3B_Get_Fuka の 15 予約語(8 バイト右詰め)。</summary>
    private static readonly HashSet<string> LoadCapacityBreakerWords = new(StringComparer.Ordinal)
    {
        "MCB     ", "ELB     ", "MMCB    ", "ELMB    ", "SB      ",
        "RMCB    ", "RELB    ", "RMMCB   ", "RELMB   ", "NHMB    ",
        "HPSB    ", "HSB     ", "CP      ", "CKS     ", "MC      ",
    };

    /// <summary>予約語を 8 バイト右詰め(空白詰め)に整える。【C原典】memcmp(…,8)。</summary>
    private static string Pad8(string? value) =>
        (value ?? string.Empty).PadRight(8)[..8];

    /// <summary>負荷種類を 2 バイト右詰め(空白詰め)に整える。【C原典】memcmp(fpalw1,…,2)。</summary>
    private static string Pad2(string? value) =>
        (value ?? string.Empty).PadRight(2)[..2];
}
