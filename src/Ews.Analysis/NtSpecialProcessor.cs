using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ＮＴ(中性線端子)の特殊処理。系統種別='1'・予約語='NT' の要素に対し、並列関係にある同一階層の
/// 予約語='MCB' で極数(P)=1 の合計を NT の電気パラメータ[2]極数へ、また MCB のトリップ電流(AT)の
/// ＭＡＸ値を NT の電気パラメータ[2]定格電流2(A2)へセットする。
/// 【C原典】<c>Fyss38_NT_Proc</c>＋<c>Fyss38_Get_epap</c>(toku/sekkei/src/Fyss38.c)。外部依存なし。
/// </summary>
public static class NtSpecialProcessor
{
    /// <summary>
    /// ＮＴの特殊処理を主回路エリア全件に対して実行する。
    /// 【C原典】<c>Fyss38_NT_Proc</c>(Pmainc, maina)。
    /// </summary>
    public static void ProcessNt(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        // 電気パラメータ[2]の極数(P)をセットする。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            if (d.SystemKind != '1' || !Matches(d.ReservedWord, "NT", 2))
            {
                continue;
            }

            int oyano = EquipmentParameterFormatter.Stoi(d.ParentSequenceNumber, 3);
            int kaino = EquipmentParameterFormatter.Stoi(d.HierarchyNumber, 3);
            int sum = GetPoleCountSum(mains, oyano, kaino);
            int epap = EquipmentParameterFormatter.Stoi(d.ElectricalParameterSlots[0].P, 3);

            if (epap != 0)
            {
                d.ElectricalParameterSlots[2].P = Fix3(d.ElectricalParameterSlots[0].P);
            }
            else
            {
                d.ElectricalParameterSlots[2].P = Fix3(sum.ToString("D3"));
            }
        }

        // 電気パラメータ[2]のトリップ電流(AT)のMAX値を求める。
        double epaatMax = 0.0;
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            if (d.SystemKind != '1' || !Matches(d.ReservedWord, "MCB", 3))
            {
                continue;
            }

            int epap = EquipmentParameterFormatter.Stoi(d.ElectricalParameterSlots[2].P, 3);
            if (epap != 1)
            {
                continue;
            }

            double epaat = EquipmentParameterFormatter.Stof(d.ElectricalParameterSlots[2].At, 9);
            if (epaat > epaatMax)
            {
                epaatMax = epaat;
            }

            d.UsedPhase = ReplacePhase(d.UsedPhase, 1, 1); // 使用相[1]をスペースクリア

            // 960322 直列関係にある機器も極数(P)入力が1なら使用相[1..3]を削る。
            for (int j = i + 1; j < mains.Count; j++)
            {
                MainCircuitData jd = mains[j].Data;
                // ★C原典 memcmp(chokuno, ...) は長さ引数欠落(K&R)で UB。直列番号を 3 バイト比較で決定化。
                if (Matches(d.HierarchyNumber, jd.HierarchyNumber, 3) &&
                    Matches(d.ParallelNumber, jd.ParallelNumber, 3) &&
                    CompareOrdinal3(d.SeriesNumber, jd.SeriesNumber) < 0)
                {
                    if (Matches(jd.ElectricalParameterSlots[0].P, "001", 3))
                    {
                        jd.UsedPhase = ReplacePhase(jd.UsedPhase, 1, 3);
                    }
                }
                else
                {
                    break;
                }
            }
        }

        string epaa2 = Fix9(EquipmentParameterFormatter.SprintfF("%09.3f", epaatMax));

        // 定格電流2(A2)にセットする。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            if (d.SystemKind != '1' || !Matches(d.ReservedWord, "NT", 2))
            {
                continue;
            }

            d.ElectricalParameterSlots[2].A2 = Matches(d.ElectricalParameterSlots[0].A2, "00000.000", 9)
                ? epaa2
                : Fix9(d.ElectricalParameterSlots[0].A2);
        }
    }

    /// <summary>
    /// 極数(P)合計取得。並列関係(親データ追番・階層番号一致)で予約語='MCB' かつ 電気パラメータ[2]の
    /// 極数(P)=1 の件数を合計し、奇数なら +1 する。【C原典】<c>Fyss38_Get_epap</c>(oya, kai, num, syu)。
    /// </summary>
    public static int GetPoleCountSum(IReadOnlyList<MainCircuitResult> mains, int oya, int kai)
    {
        ArgumentNullException.ThrowIfNull(mains);

        int sum = 0;
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            int oyano = EquipmentParameterFormatter.Stoi(d.ParentSequenceNumber, 3);
            int kaino = EquipmentParameterFormatter.Stoi(d.HierarchyNumber, 3);

            if (oyano == oya && kaino == kai && Matches(d.ReservedWord, "MCB", 3))
            {
                int epap = EquipmentParameterFormatter.Stoi(d.ElectricalParameterSlots[2].P, 3);
                if (epap == 1)
                {
                    sum++;
                }
            }
        }

        if (sum % 2 == 1) // 合計値が奇数の時 +1 する
        {
            sum++;
        }

        return sum;
    }

    // 4 バイト固定長の使用相の start から count 文字をスペースにする。
    private static string ReplacePhase(string phase, int start, int count)
    {
        char[] arr = phase.PadRight(4)[..4].ToCharArray();
        for (int k = 0; k < count; k++)
        {
            arr[start + k] = ' ';
        }

        return new string(arr);
    }

    private static int CompareOrdinal3(string a, string b) =>
        string.CompareOrdinal(a.PadRight(3)[..3], b.PadRight(3)[..3]);

    private static string Fix3(string s) => s.Length > 3 ? s[..3] : s;

    private static string Fix9(string s) => s.Length > 9 ? s[..9] : s;

    // 【C原典】memcmp(a, b, width)==0: 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
