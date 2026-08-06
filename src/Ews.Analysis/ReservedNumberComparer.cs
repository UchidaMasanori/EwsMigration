namespace Ews.Analysis;

using System.Collections.Generic;
using Ews.Domain.Analysis;

/// <summary>
/// 予約語番号テーブル(YKNO)のソート比較子。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>sortcmp3</c>(改訂&lt;14&gt;)。
///
/// 同一機器認識番号設定(SetCkikiDkkno)で ykno[] を qsort する際の比較関数。
/// 予約語+予約語番号(memcmp 16 バイト)昇順のみ。
/// </summary>
public sealed class ReservedNumberComparer : IComparer<ReservedNumberEntry>
{
    /// <summary>共有インスタンス。【C原典】qsort(..., sortcmp3)。</summary>
    public static ReservedNumberComparer Instance { get; } = new();

    /// <summary>
    /// YKNO 2 エントリを予約語で昇順比較する。【C原典】sortcmp3(Fyss1k.c:1668)。
    /// </summary>
    /// <returns>-1/0/1。【C原典】return -1/0/1。</returns>
    public int Compare(ReservedNumberEntry? x, ReservedNumberEntry? y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        // 【C原典】memcmp( p1->yoyaku, p2->yoyaku, 16 )。16 バイト固定幅・'\0' 埋めで比較。
        int ret = string.CompareOrdinal(Pad16(x.ReservedKey), Pad16(y.ReservedKey));
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
