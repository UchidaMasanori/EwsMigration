using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 下流からのパラメータ生成(MAIN)。【C原典】<c>Fyss15_Make_LowerParm</c>(toku/sekkei/src/Fyss15.c)。
/// </summary>
public static class LowerParameterGenerator
{
    /// <summary>
    /// ＮＴに直接つながる MCB1P/RMCB1P の使用相に N 相を追加する。
    /// 【C原典】<c>Fyss15_MCB1P_NT</c>(Fyss15.c:404, 950531)。下流探索は移植済みの
    /// <see cref="DownstreamSelector.SelectDownstream"/>(=Fyss35_Select_Karyu_Sub)を再利用。
    /// </summary>
    /// <param name="mains">主回路エリア(FYRT800 配列相当)。</param>
    /// <param name="phase">使用相 2 文字目に設定する相文字。【C原典】呼出側は 'N'。</param>
    public static void AdjustMcb1PhaseForNt(IReadOnlyList<MainCircuitResult> mains, char phase)
    {
        ArgumentNullException.ThrowIfNull(mains);

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;

            if (Matches(di.ReservedWord, "MCB     ", 8) &&
                Matches(di.ElectricalParameterSlots[0].P, "001", 3))
            {
                IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(mains, i + 1);
                if (downstream is null)
                {
                    continue; // 下流抽出エラー(ret != 0)
                }

                if (downstream.Count == 0)
                {
                    di.UsedPhase = SetPhaseChar(di.UsedPhase, 1, phase); // N 相を追加
                }
            }

            // 1996.01.08: RMCB も MCB と同様に処理する。
            if (Matches(di.ReservedWord, "RMCB    ", 8) &&
                Matches(di.ElectricalParameterSlots[0].P, "001", 3))
            {
                IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(mains, i + 1);
                if (downstream is null)
                {
                    continue; // 下流抽出エラー(ret != 0)
                }

                if (downstream.Count == 0)
                {
                    di.UsedPhase = SetPhaseChar(di.UsedPhase, 1, phase); // N 相を追加
                }
            }
        }
    }

    // strncmp(a, b, width) == 0 相当。
    private static bool Matches(string value, string expected, int width) =>
        (value ?? string.Empty).PadRight(width)[..width] == expected.PadRight(width)[..width];

    // 4 桁固定の使用相の index 番目を c に差し替える(他桁は保持)。
    private static string SetPhaseChar(string phase, int index, char c)
    {
        char[] arr = (phase ?? string.Empty).PadRight(4)[..4].ToCharArray();
        arr[index] = c;
        return new string(arr);
    }
}
