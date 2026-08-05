using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 親ブレーカ(MCB/ELB)配下の MC の負荷容量入力値を初期化する。
/// 【C原典】PropMcFukaReset(toku/sekkei/src/Fyss14.c:6826, 改訂&lt;31&gt;)。
///
/// 予約語 MC の各要素について、親データ追番 oyatno と行種グループ番号 gyoglno が
/// 一致する要素を全件から探し、その予約語が MCB / ELB を含むなら、当該 MC の
/// 負荷種類 fpalw1・負荷容量 fpalw2 を空白 / ゼロへ初期化する。
/// Fyss14_Make_UpperParm のループ後処理群の 1 つ。
/// </summary>
public static class McLoadCapacityResetter
{
    /// <summary>
    /// MC の負荷容量入力値を初期化する(in-place)。
    /// 【C原典】PropMcFukaReset(Fyss14.c:6826)。
    /// </summary>
    public static void Reset(IReadOnlyList<MainCircuitResult> mains)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            var mc = mains[i].Data;
            if (!Matches(mc.ReservedWord, "MC ", 3)) continue;

            for (int j = 0; j < mains.Count; j++)
            {
                var parent = mains[j];

                // 親ブレーカー(datano==oyatno)かつ行種グループ番号が同じ
                if (!Matches(parent.SequenceNumber, mc.ParentSequenceNumber, 3) ||
                    !Matches(parent.Data.LineTypeGroupNumber, mc.LineTypeGroupNumber, 3))
                {
                    continue;
                }

                string yoyaku = (parent.Data.ReservedWord ?? string.Empty).PadRight(8);
                if (!yoyaku.Contains("MCB ") && !yoyaku.Contains("ELB "))
                {
                    continue;
                }

                // 負荷容量入力値を初期化
                if (!Matches(mc.AttachedParameter.LoadKind, "  ", 2) ||
                    !Matches(mc.AttachedParameter.LoadCapacity, "0000000", 7))
                {
                    mc.AttachedParameter.LoadKind = "  ";
                    mc.AttachedParameter.LoadCapacity = "0000000";
                }
            }
        }
    }

    // 【C原典】memcmp/strncmp(a, b, width): 空白右詰めで先頭 width バイトを序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal((value ?? string.Empty).PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
