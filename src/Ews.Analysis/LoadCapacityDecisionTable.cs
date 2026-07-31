namespace Ews.Analysis;

/// <summary>
/// 負荷容量決定テーブル(FYRT812)の 1 エントリ。
/// 【C原典】<c>FYRT812</c>(toku/include/sekkei/fyrt812.h)。
/// </summary>
/// <param name="ReservedWord">予約語(8 バイト右詰め比較)。【C原典】yoyaku。</param>
/// <param name="ElectricalPriority">電気パラメータ優先順位[5]。【C原典】ep_pry[5]。</param>
/// <param name="WordPriority">予約語優先順位。【C原典】pry。</param>
/// <param name="Coefficient">負荷電流算出係数。【C原典】kei。</param>
public sealed record LoadCapacityEntry(string ReservedWord, int[] ElectricalPriority, int WordPriority, double Coefficient);

/// <summary>
/// 負荷容量決定テーブル(FYRT812)。予約語ごとに、負荷電流算出に用いる電気パラメータの優先順位・
/// 予約語優先順位・算出係数を保持する。負荷発生元設定(Fyss31)・通電電流積算(Fyss37)が参照する。
///
/// 【C原典】<c>fyrt812[FYRT812_NO]</c>(toku/include/sekkei/fyrt812.h, 41 エントリ)。
/// </summary>
public static class LoadCapacityDecisionTable
{
    /// <summary>テーブル全エントリ(宣言順)。【C原典】fyrt812[]。</summary>
    public static readonly IReadOnlyList<LoadCapacityEntry> Entries =
    [
        new("MCB", [1, 0, 0, 0, 0], 2, 0.8),
        new("ELB", [1, 0, 0, 0, 0], 2, 0.8),
        new("MMCB", [1, 2, 0, 0, 0], 1, 1.0),
        new("ELMB", [1, 2, 0, 0, 0], 1, 1.0),
        new("SB", [1, 0, 0, 0, 0], 2, 0.8),
        new("RMCB", [1, 0, 0, 0, 0], 2, 0.8),
        new("RELB", [1, 0, 0, 0, 0], 2, 0.8),
        new("RMMCB", [1, 2, 0, 0, 0], 1, 1.0),
        new("RELMB", [1, 2, 0, 0, 0], 1, 1.0),
        new("MC", [0, 0, 0, 0, 1], 2, 0.8),
        new("THR", [1, 2, 0, 0, 0], 1, 1.0),
        new("MG", [1, 2, 0, 0, 3], 1, 1.0),
        new("WH", [0, 0, 0, 1, 2], 2, 0.8),
        new("AM", [0, 0, 0, 1, 2], 3, 0.8),
        new("CT", [0, 0, 0, 1, 0], 3, 0.8),
        new("TB", [0, 0, 0, 0, 1], 2, 0.8),
        new("CON", [0, 0, 0, 0, 1], 2, 0.8),
        new("TR", [0, 0, 1, 0, 0], 2, 1.0),
        new("RTR", [0, 0, 1, 0, 0], 2, 1.0),
        new("HPSB", [1, 0, 0, 0, 0], 2, 0.8),
        new("HSB", [1, 0, 0, 0, 0], 2, 0.8),
        new("RRY", [0, 0, 0, 0, 1], 2, 0.8),
        new("MCDT", [0, 0, 0, 0, 1], 2, 1.0),
        new("F", [0, 0, 0, 0, 1], 2, 0.8),
        new("DCPW", [0, 0, 0, 0, 1], 2, 1.0),
        new("CP", [1, 0, 0, 0, 0], 2, 0.8),
        new("2ERY", [1, 2, 0, 0, 0], 1, 1.0),
        new("3ERY", [1, 2, 0, 0, 0], 1, 1.0),
        new("4ERY", [1, 2, 0, 0, 0], 1, 1.0),
        new("CKS", [0, 0, 0, 0, 1], 2, 0.8),
        new("CSDT", [0, 0, 0, 0, 1], 2, 0.8),
        new("NHMB", [2, 1, 0, 0, 0], 1, 1.0),
        new("LGT", [0, 0, 0, 0, 1], 2, 0.8),
        new("MCFR", [1, 2, 0, 0, 3], 2, 0.8),
        new("MGFR", [1, 2, 0, 0, 3], 1, 1.0),
        new("MCSD", [1, 2, 0, 0, 3], 2, 0.8),
        new("MGSD", [1, 2, 0, 0, 3], 1, 1.0),
        new("MCFRSD", [1, 2, 0, 0, 3], 2, 0.8),
        new("MGFRSD", [1, 2, 0, 0, 3], 1, 1.0),
        new("DCSIR", [0, 0, 0, 0, 1], 1, 1.0),
        new("DCNI", [0, 0, 0, 0, 1], 1, 1.0),
    ];

    /// <summary>予約語(8 バイト右詰め)が一致するエントリを返す(無ければ null)。【C原典】memcmp(yoyaku,8)。</summary>
    public static LoadCapacityEntry? Find(string reservedWord)
    {
        string key = (reservedWord ?? string.Empty).PadRight(8)[..8];
        foreach (LoadCapacityEntry e in Entries)
        {
            if (e.ReservedWord.PadRight(8)[..8] == key)
            {
                return e;
            }
        }

        return null;
    }
}
