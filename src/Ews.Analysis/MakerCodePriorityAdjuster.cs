namespace Ews.Analysis;

/// <summary>
/// メーカーコード選定順位から、保存値(元の順位)に含まれないコードを取り除いて前詰めする。
/// 元々持っていないメーカーコードを選定順位から除去する用途。
/// 【C原典】<c>PropAdjustMakerCode</c>(toku/sekkei/src/Fysk00.c:8100 改訂&lt;122&gt;)。
/// </summary>
public static class MakerCodePriorityAdjuster
{
    /// <summary>メーカーコード選定順位のスロット数。【C原典】mcod[4][3]。</summary>
    private const int SlotCount = 4;

    /// <summary>メーカーコード桁数。【C原典】strncmp(...,3)。</summary>
    private const int CodeWidth = 3;

    /// <summary>空スロット。【C原典】memset(mcod_tmp,' ',...)。</summary>
    private const string BlankCode = "   ";

    /// <summary>
    /// 保存値に含まれるメーカーコードのみを順序を保って残し、前詰めする。
    /// 【C原典】<c>PropAdjustMakerCode(mcod, mcod_org)</c>。
    /// </summary>
    /// <param name="priority">現行のメーカーコード選定順位(各 3 桁)。【C原典】mcod[][3]。</param>
    /// <param name="originalPriority">保存値のメーカーコード選定順位。【C原典】mcod_org[][3]。</param>
    /// <returns>調整後の選定順位(4 スロット固定・空きは空白)。</returns>
    public static IReadOnlyList<string> RemoveUnlistedCodes(
        IReadOnlyList<string> priority,
        IReadOnlyList<string> originalPriority)
    {
        ArgumentNullException.ThrowIfNull(priority);
        ArgumentNullException.ThrowIfNull(originalPriority);

        var result = new List<string>(SlotCount);

        // 【C原典】mcod[i] が mcod_org のいずれかに一致すれば前詰めで残す(最初の一致で break)。
        for (int i = 0; i < SlotCount; i++)
        {
            string code = SlotAt(priority, i);
            for (int j = 0; j < SlotCount; j++)
            {
                if (string.CompareOrdinal(code, SlotAt(originalPriority, j)) == 0)
                {
                    result.Add(code);
                    break;
                }
            }
        }

        // 【C原典】残りは mcod_tmp の初期値(空白)のまま。
        while (result.Count < SlotCount)
        {
            result.Add(BlankCode);
        }

        return result;
    }

    /// <summary>指定スロットのコードを 3 桁(右空白詰め)へ正規化する。範囲外は空白。</summary>
    private static string SlotAt(IReadOnlyList<string> codes, int index)
    {
        string value = index < codes.Count ? codes[index] ?? string.Empty : string.Empty;
        return value.PadRight(CodeWidth)[..CodeWidth];
    }
}
