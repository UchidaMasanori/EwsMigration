namespace Ews.Analysis;

using System.Collections.Generic;
using Ews.Domain.Analysis;

/// <summary>
/// 制御機器テーブル(SGKK)のソート比較子。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>sortcmp2</c>(改訂&lt;14&gt;/&lt;17&gt;)。
///
/// 論理記述マスタ Key データ作成(SgCheckRonriKM)で Sgkk[] を qsort する際の比較関数。
/// 予約語(memcmp 16 バイト)昇順 → 内部機器個数(nkosu)昇順 → 外部機器個数(gkosu)昇順。
/// </summary>
public sealed class ControlEquipmentComparer : IComparer<ControlEquipmentEntry>
{
    /// <summary>共有インスタンス。【C原典】qsort(..., sortcmp2)。</summary>
    public static ControlEquipmentComparer Instance { get; } = new();

    /// <summary>
    /// SGKK 2 エントリを比較する。【C原典】sortcmp2(Fyss1k.c:1624)。
    /// </summary>
    /// <returns>-1/0/1。【C原典】return -1/0/1。</returns>
    public int Compare(ControlEquipmentEntry? x, ControlEquipmentEntry? y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        // 【C原典】予約語で昇順(memcmp p1->yoyaku, p2->yoyaku, 16)。
        int ret = CompareReservedWord(x.ReservedWord, y.ReservedWord);
        if (ret < 0)
        {
            return -1;
        }

        if (ret > 0)
        {
            return 1;
        }

        // 【C原典】機器数の多いほうが後ろ側にソート(nkosu 昇順)。
        ret = x.InternalCount - y.InternalCount;
        if (ret < 0)
        {
            return -1;
        }

        if (ret > 0)
        {
            return 1;
        }

        // 【C原典】gkosu 昇順。
        ret = x.ExternalCount - y.ExternalCount;
        if (ret < 0)
        {
            return -1;
        }

        if (ret > 0)
        {
            return 1;
        }

        return 0;
    }

    // 【C原典】memcmp( p1->yoyaku, p2->yoyaku, sizeof(yoyaku)=16 )。16 バイト固定幅・'\0' 埋めで比較。
    private static int CompareReservedWord(string? a, string? b)
    {
        return string.CompareOrdinal(Pad16(a), Pad16(b));
    }

    private static string Pad16(string? s)
    {
        string v = s ?? string.Empty;
        if (v.Length > 16)
        {
            v = v[..16];
        }

        return v.PadRight(16, '\0');
    }
}
