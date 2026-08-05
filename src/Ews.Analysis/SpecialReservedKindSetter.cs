using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 特殊予約語(MGSH シャッター回路 / 27A・27B・27C)の区分(tokkbn)を自由文字から設定する。
/// 【C原典】toku/sekkei/src/Fyss14.c <c>Parm_Set_MGSH</c>(6882,改訂&lt;33&gt;) /
/// <c>Parm_Set_27</c>(7022,改訂&lt;35&gt;)。
///
/// シャッター回路(予約語 MGSH)は MG として、27A/27B/27C は CR として主回路設計エリアに
/// 書き換えられているため予約語では判断できない。回路内容記述(FYDF805)の自由文字から
/// キーワードを判定して特殊予約語区分(<see cref="MainCircuitData.SpecialReservedWordKind"/>)を設定する。
/// 回路内容記述の取得は <see cref="CircuitDescriptionArea"/>(=Fysk11_FYDF805_KkGet)へ境界注入する。
/// </summary>
public static class SpecialReservedKindSetter
{
    /// <summary>
    /// MGSH(シャッター回路)の区分を設定する。【C原典】Parm_Set_MGSH(Fyss14.c:6882)。
    ///
    /// 予約語 MG の要素につき自由文字を取得し、"MGSH" を含むもののみ対象として
    /// "3P" 記述なら '1'、"2P" 記述なら '2'、いずれも無ければ '1' を設定する。
    /// 自由文字取得に失敗(空)した場合は C 原典同様に以降の処理を打ち切る。
    /// </summary>
    /// <param name="mains">主回路レコード列。対象要素の区分を in-place で書き換える。【C原典】maina(件数 Pmainc)。</param>
    /// <param name="descriptions">回路内容記述エリア(=Fysk11_FYDF805_KkGet)。</param>
    public static void SetMgshKind(IReadOnlyList<MainCircuitResult> mains, CircuitDescriptionArea descriptions)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(descriptions);

        foreach (MainCircuitResult record in mains)
        {
            MainCircuitData d = record.Data;

            // 【C原典】予約語 MG 以外は対象外(strncmp(yoyaku,"MG",2))。
            if (!d.ReservedWord.StartsWith("MG", StringComparison.Ordinal))
            {
                continue;
            }

            // 【C原典】MG の記述行を取得。取得 NG(空)なら以降打ち切り。
            string kairoar = descriptions.GetDescriptionAt(d.DescriptionRow, d.DescriptionColumn);
            if (kairoar.Length == 0)
            {
                return;
            }

            // 【C原典】MGSH 以外は対象外。
            if (!kairoar.Contains("MGSH", StringComparison.Ordinal))
            {
                continue;
            }

            if (kairoar.Contains("3P", StringComparison.Ordinal))        // MGSH+(3P)
            {
                d.SpecialReservedWordKind = '1';
            }
            else if (kairoar.Contains("2P", StringComparison.Ordinal))   // MGSH+(2P)
            {
                d.SpecialReservedWordKind = '2';
            }
            else                                                          // 3P,2P の入力なし
            {
                d.SpecialReservedWordKind = '1';
            }
        }
    }

    /// <summary>
    /// 27A/27B/27C の区分を設定する。【C原典】Parm_Set_27(Fyss14.c:7022)。
    ///
    /// 予約語 CR の要素につき自由文字を取得し、"27A" なら '3'、"27B" なら '4'、"27C" なら '5' を設定する。
    /// いずれのキーワードも無ければ区分は変更しない。
    /// 自由文字取得に失敗(空)した場合は C 原典同様に以降の処理を打ち切る。
    /// </summary>
    /// <param name="mains">主回路レコード列。対象要素の区分を in-place で書き換える。【C原典】maina(件数 Pmainc)。</param>
    /// <param name="descriptions">回路内容記述エリア(=Fysk11_FYDF805_KkGet)。</param>
    public static void Set27Kind(IReadOnlyList<MainCircuitResult> mains, CircuitDescriptionArea descriptions)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(descriptions);

        foreach (MainCircuitResult record in mains)
        {
            MainCircuitData d = record.Data;

            // 【C原典】予約語 CR 以外は対象外(strncmp(yoyaku,"CR",2))。
            if (!d.ReservedWord.StartsWith("CR", StringComparison.Ordinal))
            {
                continue;
            }

            // 【C原典】CR の記述行を取得。取得 NG(空)なら以降打ち切り。
            string kairoar = descriptions.GetDescriptionAt(d.DescriptionRow, d.DescriptionColumn);
            if (kairoar.Length == 0)
            {
                return;
            }

            if (kairoar.Contains("27A", StringComparison.Ordinal))       // 27A
            {
                d.SpecialReservedWordKind = '3';
            }
            else if (kairoar.Contains("27B", StringComparison.Ordinal))  // 27B
            {
                d.SpecialReservedWordKind = '4';
            }
            else if (kairoar.Contains("27C", StringComparison.Ordinal))  // 27C
            {
                d.SpecialReservedWordKind = '5';
            }
        }
    }
}
