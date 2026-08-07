namespace Ews.Analysis;

/// <summary>
/// 構成機器エリア(FYRT804 配列)から検索キー先頭 10 バイトが一致する最初のレコード位置を得る。
/// 【C原典】Fysk01_Copy_Rec_Get(toku/sekkei/src/Fysk01.c:3482)。
///   for(i=0;i&lt;n;i++){ if(memcmp(&amp;(*p+i)-&gt;key, keyb, 10)==0){ ret=i; break; } } i==n なら ret=-1。
///   構成機器の copy レコード位置(電気パラメータ[1][2] を copy する対象)を得る。
///
/// FYRT804 完全モデルは未整備のため、<see cref="ComponentBufferInserter"/> と同じく
/// ジェネリック + キー抽出関数で表現する(キー先頭 10 桁の固定長前方一致)。
/// </summary>
public static class ComponentRecordFinder
{
    /// <summary>検索キー比較幅。【C原典】memcmp(..., 10)。</summary>
    public const int KeyWidth = 10;

    /// <summary>
    /// <paramref name="records"/> の先頭 <paramref name="count"/> 件を走査し、キー先頭 10 桁が
    /// <paramref name="searchKey"/> と一致する最初の位置を返す。該当なしは -1。
    /// 【C原典】Fysk01_Copy_Rec_Get(keyb, n, p)。
    /// </summary>
    /// <param name="records">構成機器エリア。【C原典】p(FYRT804 **)。</param>
    /// <param name="count">走査件数。【C原典】n(構成機器件数)。</param>
    /// <param name="searchKey">検索キー部。【C原典】keyb。</param>
    /// <param name="keySelector">レコードからキー(reckeyc key)を取り出す関数。【C原典】(*p+i)-&gt;key。</param>
    /// <returns>一致した位置(0 始まり)。該当なしは -1。</returns>
    public static int FindByKey<T>(IReadOnlyList<T> records, int count, string searchKey, Func<T, string> keySelector)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(searchKey);
        ArgumentNullException.ThrowIfNull(keySelector);

        string target = Take(searchKey, KeyWidth);
        for (int i = 0; i < count; i++)
        {
            if (Take(keySelector(records[i]), KeyWidth) == target)
            {
                return i;
            }
        }

        return -1;
    }

    private static string Take(string value, int width) => (value ?? string.Empty).PadRight(width)[..width];
}
