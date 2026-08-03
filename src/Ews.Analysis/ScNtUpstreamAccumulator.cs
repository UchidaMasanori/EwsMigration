using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ＳＣ(進相コンデンサ)／ＮＴ(中性線)の上流積算処理。回路要素='1' の予約語='SC' OR 'NT' の時、
/// 上流をさかのぼり上流積上区分='1' のテーブル要素まで通電電流値を積算(セット)する。
///
/// 【C原典】<c>Fyss3A_SC_NT_Sekisan</c>＋<c>Fyss3A_Chk_Yoyaku</c>＋<c>Fyss3A_Get_Tsuden</c>＋
///          <c>Fyss3A_Chg_KvarUf</c>＋<c>Fyss3A_Prc_Seksan</c>＋<c>Fyss3A_Get_SC_Pc</c>
///          (toku/sekkei/src/Fyss3A.c)。
///
/// C原典は主回路エリアを配列添字で直接走査し、親データ追番(oyatno)-1 を次の添字とする
/// (データ追番が 1 始まりで配列順に連続する前提)。本移行もこの添字ベースの挙動を踏襲する。
/// 負荷発生元設定(Fyss31)・末端回路の通電電流値算出(Fyss36)が <see cref="CheckReservedWord"/>／
/// <see cref="ProcessAccumulation"/> をデリゲート境界越しに利用する。
/// </summary>
public static class ScNtUpstreamAccumulator
{
    /// <summary>
    /// ＳＣ／ＮＴ の上流積算処理を主回路エリア全件に対して実行する。
    /// 【C原典】<c>Fyss3A_SC_NT_Sekisan</c>(Pmainc, maina)。
    /// </summary>
    /// <param name="mains">主回路エリア(有効件数分)。【C原典】maina[0..Pmainc)。</param>
    public static void AccumulateScNt(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        // 回路要素='1' で予約語='SC' OR 'NT' の時、通電電流値を求めて上流へ積算する。
        for (int i = 0; i < mains.Count; i++)
        {
            (int ret, int yflag) = CheckReservedWord(mains, i);
            if (ret != 0)
            {
                continue;
            }

            if (Matches(mains[i].Data.AttachedParameter.LoadName[1], "0KW", 3)) // 951002
            {
                continue;
            }

            double tsuden = GetEnergizingCurrent(mains, i, yflag);
            ProcessAccumulation(mains, i, yflag, tsuden);
        }
    }

    /// <summary>
    /// 対象データチェック。回路要素='1' かつ予約語='NT' OR 'SC' かどうかを判定する。
    /// 【C原典】<c>Fyss3A_Chk_Yoyaku</c>(syu_no, syu, yoyaku)。
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】syu。</param>
    /// <param name="index">主回路エリア対象番号。【C原典】syu_no。</param>
    /// <returns>Ret: 0=対象/1=対象外。Flag: 予約語番号 0=なし/1=NT/2=SC。</returns>
    public static (int Ret, int Flag) CheckReservedWord(IReadOnlyList<MainCircuitResult> mains, int index)
    {
        ArgumentNullException.ThrowIfNull(mains);

        MainCircuitData d = mains[index].Data;

        if (d.CircuitElement != '1') // 回路要素='1' 以外
        {
            return (1, 0);
        }

        if (Matches(d.ReservedWord, "NT", 8)) // 予約語='NT'
        {
            return (0, 1);
        }

        if (Matches(d.ReservedWord, "SC", 8)) // 予約語='SC'
        {
            return (0, 2);
        }

        return (1, 0);
    }

    /// <summary>
    /// 通電電流値取得。予約語フラグ=1(NT) は電気パラメータ[2]の定格電流２(Ａ２)を、
    /// =2(SC) は電気パラメータ[2]の静電容量(ＵＦ)を回路電圧・相数で電流換算する。
    /// 【C原典】<c>Fyss3A_Get_Tsuden</c>(no, syu, flag, tsuden)。
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】syu。</param>
    /// <param name="index">対象データ番号。【C原典】no。</param>
    /// <param name="flag">予約語フラグ 1:NT 2:SC。【C原典】flag。</param>
    /// <returns>通電電流値。【C原典】*tsuden。</returns>
    public static double GetEnergizingCurrent(IReadOnlyList<MainCircuitResult> mains, int index, int flag)
    {
        ArgumentNullException.ThrowIfNull(mains);

        MainCircuitData d = mains[index].Data;

        // 予約語フラグ=1 (NT): 定格電流２(Ａ２)を取得する。
        if (flag == 1)
        {
            return EquipmentParameterFormatter.Stof(d.ElectricalParameterSlots[2].A2, 9);
        }

        // 予約語フラグ=2 (SC): 静電容量(ＵＦ)を取得する。
        double uf = EquipmentParameterFormatter.Stof(d.ElectricalParameterSlots[2].Uf, 8);

        // 静電容量=0.0 の時、定格容量(KVAR)→静電容量(UF)変換をする。
        if (uf == 0.0)
        {
            uf = ConvertKvarToUf(mains, index);
        }

        // ＳＣ の1次側に MC が接続されているか ?(index>=1 で C の syu[-1] UB を回避)
        if (index >= 1 && Matches(mains[index - 1].Data.ReservedWord, "MC", 8))
        {
            uf = GetScPrimaryCurrent(mains, index, uf); // ＳＣ の1次電流を算出する
        }

        double kpav = EquipmentParameterFormatter.Stof(d.CircuitVoltage[0], 3);

        return d.CircuitPhaseCount switch // 回路相数
        {
            '1' => uf / kpav,
            '3' => uf / (1.732 * kpav),
            _ => uf,
        };
    }

    /// <summary>
    /// 定格容量(KVAR) → 静電容量(UF) 変換。
    /// UF = ( KVAR * 1000 ) / ( 2 * 3.14 * 回路周波数 * 回路電圧^2 * 0.000001 )。
    /// 【C原典】<c>Fyss3A_Chg_KvarUf</c>(no, syu, uf)。
    /// </summary>
    private static double ConvertKvarToUf(IReadOnlyList<MainCircuitResult> mains, int index)
    {
        MainCircuitData d = mains[index].Data;

        double kvar = EquipmentParameterFormatter.Stof(d.ElectricalParameterSlots[2].Kvar, 6);
        double kpav = EquipmentParameterFormatter.Stof(d.CircuitVoltage[0], 3);
        double kpahz = EquipmentParameterFormatter.Stof(d.CircuitFrequency, 2);

        double wkei1 = kvar * 1000.0;
        double wkei2 = Math.Pow(kpav, 2);
        double wkei3 = 2.0 * 3.14 * kpahz * wkei2 * 0.000001;

        return wkei1 / wkei3;
    }

    /// <summary>
    /// ＳＣの1次電流取得。UF = 2 * 3.14159 * 回路周波数 * UF * 回路電圧^2 * 1E-6。
    /// 【C原典】<c>Fyss3A_Get_SC_Pc</c>(no, syu, uf)(960329)。
    /// </summary>
    private static double GetScPrimaryCurrent(IReadOnlyList<MainCircuitResult> mains, int index, double uf)
    {
        MainCircuitData d = mains[index].Data;

        double kpav = EquipmentParameterFormatter.Stof(d.CircuitVoltage[0], 3);
        double kpahz = EquipmentParameterFormatter.Stof(d.CircuitFrequency, 2);

        return 2.0 * 3.14159 * kpahz * uf * Math.Pow(kpav, 2) * 1E-6;
    }

    /// <summary>
    /// 積算処理。上流積み上げ区分='1' のテーブル要素まで通電電流値をセットする。
    /// 【C原典】<c>Fyss3A_Prc_Seksan</c>(no, syu, flag, tsuden)。
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】syu。</param>
    /// <param name="index">対象データ番号。【C原典】no。</param>
    /// <param name="flag">予約語フラグ 1:NT 2:SC。【C原典】flag。</param>
    /// <param name="energizingCurrent">通電電流値。【C原典】tsuden。</param>
    public static void ProcessAccumulation(
        IReadOnlyList<MainCircuitResult> mains, int index, int flag, double energizingCurrent)
    {
        ArgumentNullException.ThrowIfNull(mains);

        int setno = index;

        while (true)
        {
            MainCircuitData d = mains[setno].Data;

            d.EnergizingCurrent = Denryu8(energizingCurrent); // 通電電流値セット

            if (flag == 1) // 予約語='NT' の時、使用相 "N   " セット
            {
                d.UsedPhase = "N   ";
            }

            if (d.StackKind == '1') // 上流積み上げ区分='1' の時
            {
                break;
            }

            int oyano = EquipmentParameterFormatter.Stoi(d.ParentSequenceNumber, 3);
            if (oyano == 0)
            {
                break;
            }

            setno = oyano - 1;
        }
    }

    // 【C原典】sprintf(work,"%08.2lf",tsuden); memcpy(denryu,work,8)。8 バイト固定長へ整形。
    private static string Denryu8(double value)
    {
        string s = EquipmentParameterFormatter.SprintfF("%08.2f", value);
        return s.Length > 8 ? s[..8] : s;
    }

    // 【C原典】memcmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
