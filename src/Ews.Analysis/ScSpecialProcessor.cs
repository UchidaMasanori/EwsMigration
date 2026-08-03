using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ＳＣ(進相コンデンサ)の特殊処理。予約語='SC' の時、電気パラメータ[2]の静電容量(UF)または
/// 定格容量(KVAR)を生成する。入力済み(UF/KVAR≠0)はそのまま[2]へ、未入力(UF=KVAR=0)は
/// 並列電動機容量から静電容量を算出する。
///
/// 【C原典】<c>Fyss39_SC_Proc</c>＋<c>Fyss39_Chk_Yoyaku</c>＋<c>Fyss39_Srt_Kaisno</c>＋
///          <c>Fyss39_Get_Seiden</c>(toku/sekkei/src/Fyss39.c)。
///
/// ★C原典の並列ＳＣ分岐(<c>Fyss39_Get_ParmSC</c>/<c>Fyss39_Set_ParmSC</c>/<c>Fyss39_Chk_Heiret</c>/
/// <c>Fyss39_Chg_KvarUf</c>)は、負荷容量合計ループ手前の 951002 改訂 <c>continue</c> により
/// <c>sc_flag</c> が常に 0 となるため到達不能(デッドコード)。本移植は生存パス(sc_flag==0)のみを
/// 移植する。C原典は配列添字ベース(データ追番が 1 始まりで配列順に連続する前提)で no-1 を添字とする。
/// </summary>
public static class ScSpecialProcessor
{
    /// <summary>
    /// ＳＣの特殊処理を主回路エリア全件に対して実行する。
    /// 【C原典】<c>Fyss39_SC_Proc</c>(Pmainc, maina)。
    /// </summary>
    /// <param name="mains">主回路エリア(有効件数分)。【C原典】maina[0..Pmainc)。</param>
    public static void ProcessSc(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        // 全テーブル要素のＳＣ処理済フラグをスペースクリアする。
        for (int i = 0; i < mains.Count; i++)
        {
            mains[i].Work.ScProcessedFlag = ' ';
        }

        // 静電容量!=0 OR 定格容量!=0 の時は電気パラメータ[2]へコピー。0 の時はデータ追番を記録する。
        var dataNumbers = new List<int>();
        for (int i = 0; i < mains.Count; i++)
        {
            (int ret, double uf, double kvar) = CheckReservedWord(mains, i);
            if (ret != 0)
            {
                continue;
            }

            MainCircuitData d = mains[i].Data;
            if (uf != 0.0 || kvar != 0.0)
            {
                d.ElectricalParameterSlots[2].Uf = Uf8(uf);      // 静電容量(UF) [2]へ
                d.ElectricalParameterSlots[2].Kvar = Kvar6(kvar); // 定格容量(KVAR) [2]へ
                mains[i].Work.ScProcessedFlag = '1';
            }
            else
            {
                dataNumbers.Add(EquipmentParameterFormatter.Stoi(mains[i].SequenceNumber, 3));
            }
        }

        // 階層番号ソート処理。
        SortByHierarchy(dataNumbers, mains);

        // 'SC' のテーブル要素の電気パラメータ[2]の静電容量(UF)を生成する。
        for (int di = 0; di < dataNumbers.Count; di++)
        {
            double pm = 0.0;               // 負荷容量合計
            int no = dataNumbers[di];      // データ追番(1始まり)

            if (no - 1 < 0 || no - 1 >= mains.Count)
            {
                continue; // C原典は配列添字前提。範囲外は決定的にスキップ。
            }

            MainCircuitResult target = mains[no - 1];
            if (target.Work.ScProcessedFlag != ' ')
            {
                continue; // 既に処理済
            }

            AttachedParameters tfp = target.Data.AttachedParameter;
            if (Matches(tfp.LoadName[1], "0KW", 3))
            {
                continue; // 951002 負荷名称=0KW は対象外
            }

            // 950928 直近機器の負荷容量(電動機容量)を求める。
            double fyo = 0.0;
            if (Matches(tfp.LoadKind, "M ", 2)) // 負荷種類='M'(電動機)
            {
                fyo = EquipmentParameterFormatter.Stof(tfp.LoadCapacity, 7);
                if (no >= 2)
                {
                    mains[no - 2].Work.ScProcessedFlag = '1';
                }
            }
            else if (Matches(tfp.LoadKind, "  ", 2)) // 負荷種類=空白
            {
                double maw1 = 1.0E9;
                bool ihit = false;
                for (int j = 0; j < mains.Count; j++)
                {
                    if (di == j) // C原典どおり datano 添字 di と配列添字 j を比較
                    {
                        continue;
                    }

                    MainCircuitData jd = mains[j].Data;
                    if (Matches(target.Data.LineTypeCode, jd.LineTypeCode, 3) &&
                        Matches(target.Data.LineTypeGroupNumber, jd.LineTypeGroupNumber, 3))
                    {
                        if (!Matches(jd.ElectricalParameterSlots[0].W1, "0000000.00", 10))
                        {
                            double aw1 = EquipmentParameterFormatter.Stof(jd.ElectricalParameterSlots[0].W1, 10);
                            if (maw1 > aw1)
                            {
                                maw1 = aw1;
                            }

                            ihit = true;
                        }

                        mains[j].Work.ScProcessedFlag = '1';
                    }
                }

                if (!ihit) // 同一行種の負荷が無い時は上流の電動機を遡り検索する。
                {
                    maw1 = 0.0;
                    for (int j = no - 2; j > 0; j--)
                    {
                        MainCircuitData jd = mains[j].Data;
                        if (Matches(target.Data.LineTypeCode, jd.LineTypeCode, 3) &&
                            Matches(target.Data.LineTypeGroupNumber, jd.LineTypeGroupNumber, 3))
                        {
                            if (Matches(jd.AttachedParameter.LoadKind, "M ", 2))
                            {
                                maw1 = EquipmentParameterFormatter.Stof(jd.AttachedParameter.LoadCapacity, 7);
                                mains[j].Work.ScProcessedFlag = '1';
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                fyo = maw1;
            }

            pm += fyo;

            // ＳＣの静電容量(UF)を算出する。SCuf = (Pkm)^a * b。
            // sc_flag は常に 0(並列ＳＣ分岐はデッドコード)のため、ここで[2]へ直接セットする。
            double scUf = GetCapacitance(mains, no, pm);
            target.Data.ElectricalParameterSlots[2].Uf = Uf8(scUf);
            target.Work.ScProcessedFlag = '1';
        }

        // 2003.05.13 add ＳＣの容量が設定できない(0)時、他のＳＣからむりやりセットする。
        FillMissingCapacitanceForward(mains);
        FillMissingCapacitanceBackward(mains);
    }

    /// <summary>
    /// 対象データチェック。系統種別='1' かつ予約語='SC' の時、電気パラメータ[0]の静電容量(UF)・
    /// 定格容量(KVAR)を取得する。【C原典】<c>Fyss39_Chk_Yoyaku</c>(syu_no, syu, uf, kvar)。
    /// </summary>
    /// <returns>Ret: 0=対象/1=対象外。Uf/Kvar: 電気パラメータ[0]の静電容量・定格容量。</returns>
    public static (int Ret, double Uf, double Kvar) CheckReservedWord(
        IReadOnlyList<MainCircuitResult> mains, int index)
    {
        ArgumentNullException.ThrowIfNull(mains);

        MainCircuitData d = mains[index].Data;

        if (d.SystemKind != '1') // 系統種別!='1'
        {
            return (1, 0.0, 0.0);
        }

        if (!Matches(d.ReservedWord, "SC", 8)) // 予約語!='SC'
        {
            return (1, 0.0, 0.0);
        }

        double uf = EquipmentParameterFormatter.Stof(d.ElectricalParameterSlots[0].Uf, 8);
        double kvar = EquipmentParameterFormatter.Stof(d.ElectricalParameterSlots[0].Kvar, 6);
        return (0, uf, kvar);
    }

    /// <summary>
    /// 階層番号ソート処理。データ追番配列を階層番号(kaisono)の降順に並べ替える。
    /// 【C原典】<c>Fyss39_Srt_Kaisno</c>(num, no, maina)。
    /// </summary>
    private static void SortByHierarchy(List<int> dataNumbers, IReadOnlyList<MainCircuitResult> mains)
    {
        for (int i = 0; i < dataNumbers.Count; i++)
        {
            for (int j = i + 1; j < dataNumbers.Count; j++)
            {
                string ki = HierarchyOf(mains, dataNumbers[i]);
                string kj = HierarchyOf(mains, dataNumbers[j]);
                if (string.CompareOrdinal(ki, kj) < 0)
                {
                    (dataNumbers[i], dataNumbers[j]) = (dataNumbers[j], dataNumbers[i]);
                }
            }
        }
    }

    // 【C原典】maina[dno-1].dt.kaisono(3 バイト)。範囲外は末尾側扱いの最小値。
    private static string HierarchyOf(IReadOnlyList<MainCircuitResult> mains, int dataNumber)
    {
        int index = dataNumber - 1;
        return index >= 0 && index < mains.Count
            ? mains[index].Data.HierarchyNumber.PadRight(3)[..3]
            : "\0\0\0";
    }

    /// <summary>
    /// ＳＣの容量の設定。SCuf = (Pkm)^a * b。電動機容量 Pm[W]/1000 と回路相数・電圧・周波数から
    /// 係数 a/b を決めて静電容量(UF)を算出する。【C原典】<c>Fyss39_Get_Seiden</c>(no, syu, pm, SCuf)。
    /// </summary>
    private static double GetCapacitance(IReadOnlyList<MainCircuitResult> mains, int no, double pm)
    {
        MainCircuitData d = mains[no - 1].Data;

        int tpaph = d.CircuitPhaseCount is >= '0' and <= '9' ? d.CircuitPhaseCount - '0' : 0;
        int tpav = EquipmentParameterFormatter.Stoi(d.CircuitVoltage[0], 3);
        int tpahz = EquipmentParameterFormatter.Stoi(d.CircuitFrequency, 2);

        double pkm = pm / 1000.0;
        double a;
        double b;

        if (tpaph == 3)
        {
            if (tpav <= 220)
            {
                if (tpahz == 50)
                {
                    (a, b) = pkm <= 4.00 ? (0.56, 37.00)
                        : pkm <= 40.00 ? (0.89, 26.00)
                        : (1.00, 18.00);
                }
                else
                {
                    // 1998.05.19 chg 4<Pkm<=40 の乗数変更 (0.90/20.00 → 0.91/18.71)
                    (a, b) = pkm <= 4.00 ? (0.52, 28.00)
                        : pkm <= 40.00 ? (0.91, 18.71)
                        : (1.00, 15.00);
                }
            }
            else
            {
                if (tpahz == 50)
                {
                    (a, b) = pkm <= 1.50 ? (0.38, 9.40)
                        : pkm <= 5.50 ? (0.83, 7.80)
                        : pkm <= 22.00 ? (0.90, 6.60)
                        : pkm <= 55.00 ? (1.26, 1.80)
                        : (1.00, 4.50);
                }
                else
                {
                    (a, b) = pkm <= 1.50 ? (0.22, 7.20)
                        : pkm <= 5.50 ? (0.86, 5.80)
                        : pkm <= 22.00 ? (1.02, 3.90)
                        : pkm <= 55.00 ? (1.31, 1.19)
                        : (1.00, 3.75);
                }
            }
        }
        else if (tpaph == 1 && tpav <= 105)
        {
            a = tpahz == 50 ? 0.16 : 0.28;
            b = 112.00;
        }
        else
        {
            (a, b) = tpahz == 50 ? (0.39, 54.00) : (0.41, 48.00);
        }

        return Math.Pow(pkm, a) * b;
    }

    // 2003.05.13 前方(j+1..)の非0ＳＣ静電容量を、0のＳＣへコピーする。
    private static void FillMissingCapacitanceForward(IReadOnlyList<MainCircuitResult> mains)
    {
        for (int j = 0; j < mains.Count; j++)
        {
            if (!IsZeroCapacitanceSc(mains[j]))
            {
                continue;
            }

            for (int i = j + 1; i < mains.Count; i++)
            {
                if (!Matches(mains[i].Data.ReservedWord, "SC", 8) || IsZeroCapacitanceSc(mains[i]))
                {
                    continue;
                }

                mains[j].Data.ElectricalParameterSlots[2].Uf = Fix8(mains[i].Data.ElectricalParameterSlots[2].Uf);
                break;
            }
        }
    }

    // 2003.05.13 後方(j-1..1)の非0ＳＣ静電容量を、0のＳＣへコピーする。
    private static void FillMissingCapacitanceBackward(IReadOnlyList<MainCircuitResult> mains)
    {
        for (int j = 0; j < mains.Count; j++)
        {
            if (!IsZeroCapacitanceSc(mains[j]))
            {
                continue;
            }

            for (int i = j - 1; i > 0; i--)
            {
                if (!Matches(mains[i].Data.ReservedWord, "SC", 8) || IsZeroCapacitanceSc(mains[i]))
                {
                    continue;
                }

                mains[j].Data.ElectricalParameterSlots[2].Uf = Fix8(mains[i].Data.ElectricalParameterSlots[2].Uf);
                break;
            }
        }
    }

    // 予約語='SC' かつ電気パラメータ[2]静電容量(UF)が整形ゼロ "000000.0"。
    private static bool IsZeroCapacitanceSc(MainCircuitResult m) =>
        Matches(m.Data.ReservedWord, "SC", 8) &&
        Matches(m.Data.ElectricalParameterSlots[2].Uf, "000000.0", 8);

    // 【C原典】sprintf(work,"%08.1lf",uf); memcpy(epauf,work,8)。8 バイト固定長へ整形。
    private static string Uf8(double value) => Fix8(EquipmentParameterFormatter.SprintfF("%08.1f", value));

    // 【C原典】sprintf(work,"%06.2lf",kvar); memcpy(epakvar,work,6)。6 バイト固定長へ整形。
    private static string Kvar6(double value)
    {
        string s = EquipmentParameterFormatter.SprintfF("%06.2f", value);
        return s.Length > 6 ? s[..6] : s;
    }

    private static string Fix8(string s) => s.Length > 8 ? s[..8] : s;

    // 【C原典】memcmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
