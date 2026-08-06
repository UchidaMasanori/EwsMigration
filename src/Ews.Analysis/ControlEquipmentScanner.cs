namespace Ews.Analysis;

using System;
using System.Collections.Generic;

/// <summary>
/// 制御機器データテーブル(SGKK)の予約語を走査して特定機器の件数を数えるリーフ群。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>CheckRmKiki</c> / <c>CheckTenmetu</c>。
///
/// C 原典はグローバルの制御機器データテーブル <c>Sgkk[SCnt]</c> を走査するが、SGKK 構造体は
/// 未移植のため、各エントリの予約語(<c>Sgkk[i].yoyaku</c>)の列を入力とする純粋関数として移植する。
/// 判定は strstr(部分一致)に忠実。
/// </summary>
public static class ControlEquipmentScanner
{
    // 【C原典】static CHAR *RM_sgkiki[] = { "RSW","TU","CU","PT", NULL };
    private static readonly string[] RemoteControlKeywords = { "RSW", "TU", "CU", "PT" };

    // 【C原典】static CHAR *sgkiki[] = { "TSU","SSWU", NULL };
    private static readonly string[] AutoFlashKeywords = { "TSU", "SSWU" };

    /// <summary>
    /// リモコン制御機器(予約語に RSW/TU/CU/PT のいずれかを含む)の件数を数える。
    /// 【C原典】CheckRmKiki(Fyss1k.c:1082)。
    /// </summary>
    /// <param name="reservedWords">制御機器データテーブルの予約語列。【C原典】Sgkk[i].yoyaku。</param>
    /// <returns>該当機器の件数。【C原典】cnt。</returns>
    public static int CountRemoteControlEquipment(IReadOnlyList<string?> reservedWords)
    {
        return CountContainingAny(reservedWords, RemoteControlKeywords);
    }

    /// <summary>
    /// 自動点滅タイマー・自動点滅増幅機(予約語に TSU/SSWU のいずれかを含む)の件数を数える。
    /// 【C原典】CheckTenmetu(Fyss1k.c:1105)。
    /// </summary>
    /// <param name="reservedWords">制御機器データテーブルの予約語列。【C原典】Sgkk[i].yoyaku。</param>
    /// <returns>該当機器の件数。【C原典】cnt。</returns>
    public static int CountAutoFlashEquipment(IReadOnlyList<string?> reservedWords)
    {
        return CountContainingAny(reservedWords, AutoFlashKeywords);
    }

    // 【C原典】各エントリの yoyaku にキーワードのいずれかが strstr で見つかれば 1 件と数える(見つかった時点で break)。
    private static int CountContainingAny(IReadOnlyList<string?> reservedWords, string[] keywords)
    {
        ArgumentNullException.ThrowIfNull(reservedWords);

        int count = 0;
        foreach (string? word in reservedWords)
        {
            string w = word ?? string.Empty;
            foreach (string keyword in keywords)
            {
                if (w.Contains(keyword, StringComparison.Ordinal))
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }
}
