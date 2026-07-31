using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 負荷発生元設定(<c>Fyss31_FukaHassei_Set</c>)の負荷容量決定処理。負荷容量決定テーブル(FYRT812)を
/// 用いて候補機器の電気パラメータを優先順位順に評価し、負荷電流値を求める。
///
/// 【C原典】<c>set_fky</c>／<c>get_ep</c>(toku/sekkei/src/Fyss31.c, static)。
/// 係数×値(AT/A1/A2)または <see cref="EnergizingCurrentCalculator"/>(=set_denryu, W/VA)で電流化する。
/// </summary>
public static class LoadSourceSelector
{
    /// <summary>選定成功(電流値・優先順位を設定)。【C原典】set_fky 戻り値 0。</summary>
    public const int Selected = 0;

    /// <summary>電気パラメータの入力が無い。【C原典】set_fky 戻り値 1。</summary>
    public const int NoValue = 1;

    /// <summary>候補予約語の優先順位が現best より低い。【C原典】set_fky 戻り値 2。</summary>
    public const int LowerPriority = 2;

    /// <summary>負荷容量決定テーブルに予約語が無い(C原典は未 return=UB、本移行は明示コード)。</summary>
    public const int NotInTable = 3;

    /// <summary>
    /// 候補機器(candidateIndex)の負荷電流値と予約語優先順位を負荷容量決定テーブルから求める。
    /// 【C原典】set_fky(maina, fky)。fky[0].pry = <paramref name="bestPriority"/>、fky[1].fno = candidateIndex。
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】maina。</param>
    /// <param name="candidateIndex">候補の主回路添字(0始まり)。【C原典】fky[1].fno。</param>
    /// <param name="bestPriority">現時点で最良の予約語優先順位。【C原典】fky[0].pry。</param>
    /// <param name="priority">求めた候補の予約語優先順位。【C原典】fky[1].pry。</param>
    /// <param name="current">求めた負荷電流値。【C原典】fky[1].denryu。</param>
    public static int SelectLoadCurrent(
        IReadOnlyList<MainCircuitResult> mains,
        int candidateIndex,
        int bestPriority,
        out int priority,
        out double current)
    {
        ArgumentNullException.ThrowIfNull(mains);

        priority = 0;
        current = 0.0;

        MainCircuitResult candidate = mains[candidateIndex];
        LoadCapacityEntry? entry = LoadCapacityDecisionTable.Find(candidate.Data.ReservedWord);
        if (entry is null)
        {
            return NotInTable;
        }

        // 候補予約語の優先順位が best より低い(数値が大きい)なら不採用。
        if (entry.WordPriority > bestPriority)
        {
            return LowerPriority;
        }

        // 優先順位 1→3 の順に、対応する電気パラメータで電流化を試みる。
        for (int k = 1; k <= 3; k++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (entry.ElectricalPriority[j] != k)
                {
                    continue;
                }

                if (TryGetParameterCurrent(candidate, entry, j, out current))
                {
                    priority = entry.WordPriority;
                    return Selected;
                }

                break; // 当該優先順位の電気パラメータは入力無し → 次の優先順位へ。
            }
        }

        return NoValue;
    }

    /// <summary>
    /// 電気パラメータ種別(paramIndex: 0=AT/1=W/2=VA/3=A1/4=A2)から負荷電流値を求める。
    /// 入力が無ければ false。【C原典】get_ep(maina, fyrt812, fky, i, j)。
    /// </summary>
    private static bool TryGetParameterCurrent(MainCircuitResult candidate, LoadCapacityEntry entry, int paramIndex, out double current)
    {
        current = 0.0;
        ElectricalParameters ep = candidate.Data.ElectricalParameterSlots[0];
        string rw = candidate.Data.ReservedWord;

        switch (paramIndex)
        {
            case 0: // AT (トリップ電流)
                if (IsZero(ep.At, "00000.000", 9))
                {
                    return false;
                }

                if (Matches(ep.At, "99999.999", 9))
                {
                    // AT がサーチ上限値のときはフレーム電流(AF)を用いる。
                    if (IsZero(ep.Af, "00000.000", 9))
                    {
                        return false;
                    }

                    current = entry.Coefficient * EquipmentParameterFormatter.Stof(ep.Af, 9);
                    return true;
                }

                current = entry.Coefficient * EquipmentParameterFormatter.Stof(ep.At, 9);
                return true;

            case 1: // W (負荷容量)
                if (IsZero(ep.W1, "0000000.00", 10))
                {
                    return false;
                }

                {
                    double fuka = EquipmentParameterFormatter.Stof(ep.W1, 10);
                    string kind = Matches(rw, "MG", 8) || Matches(rw, "MMCB", 8) || Matches(rw, "ELMB", 8) ||
                                  Matches(rw, "RMMCB", 8) || Matches(rw, "RELMB", 8)
                        ? "M "
                        : "TR";
                    return EnergizingCurrentCalculator.TryCalculate(candidate.Data, fuka, kind, out current);
                }

            case 2: // VA (負荷容量)
                if (IsZero(ep.Va, "0000000.00", 10))
                {
                    return false;
                }

                {
                    double fuka = EquipmentParameterFormatter.Stof(ep.Va, 10);
                    string kind;
                    if (Matches(rw, "MMCB", 8) || Matches(rw, "ELMB", 8) || Matches(rw, "RMMCB", 8) || Matches(rw, "RELMB", 8))
                    {
                        kind = "M ";
                    }
                    else
                    {
                        kind = candidate.Data.CircuitPhaseCount == '3' ? "M " : "H ";
                    }

                    return EnergizingCurrentCalculator.TryCalculate(candidate.Data, fuka, kind, out current);
                }

            case 3: // A1 (定格電流1)
                if (IsZero(ep.A1, "00000.000", 9))
                {
                    return false;
                }

                current = entry.Coefficient * EquipmentParameterFormatter.Stof(ep.A1, 9);
                return true;

            default: // A2 (定格電流2)
                if (IsZero(ep.A2, "00000.000", 9))
                {
                    return false;
                }

                current = entry.Coefficient * EquipmentParameterFormatter.Stof(ep.A2, 9);
                return true;
        }
    }

    // 【C原典】strncmp(field, zero, width) == 0: ゼロ整形文字列と一致(=入力無し)。
    private static bool IsZero(string value, string zero, int width) => Matches(value, zero, width);

    // 【C原典】strncmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
