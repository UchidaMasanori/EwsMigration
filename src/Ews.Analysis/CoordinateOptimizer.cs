using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 並列関係を持たない座標を階層 1 つ上げ、直列関係へ最適化する。
/// 【C原典】OptimZahyo / HeiretuCheck / EditZahyo(toku/sekkei/src/Fyss14.c:6089, 941226)。
///
/// 各要素について並列関係の有無を判定し(HeiretuCheck)、並列関係が無い(irc==1)場合に
/// 座標(階層 kaisono・直列 chokuno・並列 heino・上流並列 joheino・グループ親 goyano)を
/// 編集する(EditZahyo)。Fyss14_Make_UpperParm のループ後処理群の 1 つ。
/// </summary>
public static class CoordinateOptimizer
{
    /// <summary>
    /// 座標の最適化を行う(in-place)。
    /// 【C原典】OptimZahyo(Fyss14.c:6089)。
    /// </summary>
    public static void Optimize(IReadOnlyList<MainCircuitResult> mains)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            // irc==1(並列関係なし)のとき座標を編集
            if (HasNoParallel(i, mains))
            {
                EditCoordinate(i, mains);
            }
        }
    }

    // 【C原典】HeiretuCheck: 戻り値 0=並列関係あり / 1=並列関係なし。true=並列関係なし。
    private static bool HasNoParallel(int iNo, IReadOnlyList<MainCircuitResult> mains)
    {
        var d = mains[iNo].Data;

        // 階層番号 000 と 001 はチェックを行わない
        if (Eq3(d.HierarchyNumber, "000")) return false;
        if (Eq3(d.HierarchyNumber, "001")) return false;

        // 直列追番が 001 のものだけチェックする
        if (!Eq3(d.SeriesNumber, "001")) return false;

        // LA 機器は親機器と別階層のままにするため最適化させない(改訂19)
        if (EqTrim(d.ReservedWord, "LA")) return false;

        for (int i = 0; i < mains.Count; i++)
        {
            if (iNo == i) continue;
            var e = mains[i].Data;

            // 同階層で並列追番が異なる=並列関係あり
            if (Eq3(d.IncomingNumber, e.IncomingNumber) &&
                Eq3(d.HierarchyNumber, e.HierarchyNumber) &&
                !Eq3(d.ParallelNumber, e.ParallelNumber))
            {
                return false;
            }
        }
        return true;
    }

    // 【C原典】EditZahyo: 座標の編集。
    private static void EditCoordinate(int iNo, IReadOnlyList<MainCircuitResult> mains)
    {
        var origin = mains[iNo].Data;
        string kaisono = Pad3(origin.HierarchyNumber);   // 基準階層(取得時点で確定)
        int ioya = P3(origin.ParentSequenceNumber) - 1;

        for (int i = 0; i < mains.Count; i++)
        {
            var e = mains[i].Data;

            if (!Eq3(origin.IncomingNumber, e.IncomingNumber) ||
                Cmp3(e.HierarchyNumber, kaisono) < 0)
            {
                continue;
            }

            // 階層番号の振りなおし
            e.HierarchyNumber = Fmt3(P3(e.HierarchyNumber) - 1);

            // 振りなおし後も基準階層以上なら以降の編集は行わない
            if (Cmp3(e.HierarchyNumber, kaisono) >= 0) continue;

            // 直列追番の振りなおし
            if (i > 0)
            {
                e.SeriesNumber = Fmt3(P3(mains[i - 1].Data.SeriesNumber) + 1);
            }

            // 並列追番の振りなおし
            if (Eq3(e.UpperParallelNumber, "000"))
            {
                e.ParallelNumber = "001";
            }
            else
            {
                e.ParallelNumber = Pad3(e.UpperParallelNumber);
            }

            // 上流並列の振りなおし
            for (int j = 0; j < i; j++)
            {
                var f = mains[j].Data;
                if (Eq3(e.IncomingNumber, f.IncomingNumber) &&
                    Eq3(e.HierarchyNumber, f.HierarchyNumber) &&
                    Eq3(e.ParallelNumber, f.ParallelNumber) &&
                    Eq3("001", f.SeriesNumber))
                {
                    e.UpperParallelNumber = Pad3(f.UpperParallelNumber);
                    break;
                }
            }

            // グループ親データ追番の振りなおし
            if (ioya >= 0 && ioya < mains.Count)
            {
                e.GroupParentSequenceNumber = Pad3(mains[ioya].Data.GroupParentSequenceNumber);
            }
        }
    }

    // 3 桁空白右詰め(memcpy 3 バイト相当)。
    private static string Pad3(string? s) => (s ?? string.Empty).PadRight(3)[..3];

    // 【C原典】memcmp(a, b, 3): 先頭 3 バイトを序数比較。
    private static int Cmp3(string? a, string? b) => string.CompareOrdinal(Pad3(a), Pad3(b));

    private static bool Eq3(string? a, string? b) => Cmp3(a, b) == 0;

    // 【C原典】strncmp(yoyaku, "XX ", 3): 予約語(トリム済み)の一致。
    private static bool EqTrim(string? value, string expected) =>
        string.Equals((value ?? string.Empty).Trim(), expected, StringComparison.Ordinal);

    // 【C原典】atoi: 先頭数値を整数化。解釈不能は 0。
    private static int P3(string? s) => int.TryParse((s ?? string.Empty).Trim(), out int v) ? v : 0;

    // 【C原典】sprintf("%03d", v): 3 桁ゼロ埋め。4 桁以上は先頭 3 バイトを採用(memcpy 3)。
    private static string Fmt3(int v)
    {
        string s = v.ToString("000");
        return s.Length > 3 ? s[..3] : s;
    }
}
