namespace Ews.Analysis;

/// <summary>
/// 構成機器エリア(FYRT804 配列)の指定位置へ 1 レコードを割り込み挿入する下請け。
/// 機器選定結果を定格キー順で保持するための、ソート挿入の最下位ヘルパ。
/// 【C原典】Fysk01_Mem_Control(toku/sekkei/src/Fysk01.c:4046)。
///
/// C原典は 1 レコード size バイトの生バッファ buf2 に対し、
///   for(i=allkensu-1; i>=wkkensu; i--) memcpy(buf2[i+1], buf2[i]);
///   memcpy(buf2[wkkensu], buf1);
/// で [wkkensu, allkensu-1] を 1 つ後方へずらし、割り込み位置 wkkensu へ buf1 を置く。
/// 呼び出し側(Fysk01_Make_Koukiki 系)が事前に realloc で容量を確保している前提。
/// </summary>
public static class ComponentBufferInserter
{
    /// <summary>
    /// <paramref name="buffer"/> の <paramref name="insertIndex"/> 番目へ <paramref name="record"/> を
    /// 割り込み挿入する。既存の [insertIndex, totalCount-1] を 1 つ後方へずらす。
    /// 【C原典】Fysk01_Mem_Control(size, buf1, wkkensu, allkensu, buf2)。
    /// </summary>
    /// <param name="buffer">全メモリバッファ(有効レコード数 = totalCount)。【C原典】buf2。</param>
    /// <param name="record">割り込む入力レコード。【C原典】buf1。</param>
    /// <param name="insertIndex">割り込み位置(0 始まり)。【C原典】wkkensu。</param>
    /// <param name="totalCount">割り込み前の全件数。【C原典】allkensu。</param>
    public static void Insert<T>(IList<T> buffer, T record, int insertIndex, int totalCount)
    {
        // C原典は事前 realloc 済みの生バッファ前提。C# では末尾を 1 件伸ばして空きを作る。
        buffer.Add(default!);

        for (int i = totalCount - 1; i >= insertIndex; i--)
        {
            buffer[i + 1] = buffer[i];
        }

        buffer[insertIndex] = record;
    }
}
