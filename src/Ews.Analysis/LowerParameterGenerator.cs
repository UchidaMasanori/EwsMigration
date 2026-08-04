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

    /// <summary>
    /// 1-2 型 MCDT/CSDT の処理。下流の負荷を算出し、同一機器認識番号のペアで通電電流値が
    /// 小さい方のテーブル要素に上流積み上げ区分をセットする。対象要素とその下流の通電電流値・
    /// 積算エリアはクリアする。
    /// 【C原典】<c>Fyss3E_12_MCDT_CSDT</c>(toku/sekkei/src/Fyss3E.c, 940727)。通電電流値積算は
    /// <see cref="TerminalCurrentIntegrator.IntegrateCurrent"/>(=Fyss37_I_Set_Sub)、下流抽出は
    /// <see cref="DownstreamSelector.SelectDownstream"/>(=Fyss35_Select_Karyu_Sub)を再利用。
    /// </summary>
    /// <param name="mains">主回路エリア(FYRT800 配列相当)。</param>
    public static void Process12McdtCsdt(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        const int MaxNo = 100; // 【C原典】MAX_NO=100。datano/noflag の固定長。
        int[] datano = new int[MaxNo];
        bool[] noflag = new bool[MaxNo];

        // 回路要素'1' で予約語 'MCDT'/'CSDT' かつ切り換えタイプ'1'(1-2型)を取得し、通電電流値を積算する。
        int num = 0;
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            if (d.CircuitElement != '1')
            {
                continue;
            }

            if (!Matches(d.ReservedWord, "MCDT", 4) && !Matches(d.ReservedWord, "CSDT", 4))
            {
                continue;
            }

            if (d.SwitchType == '1')
            {
                int no1 = EquipmentParameterFormatter.Stoi(mains[i].SequenceNumber, 3);
                datano[num] = no1;
                num++;
                TerminalCurrentIntegrator.IntegrateCurrent(mains, no1);
            }
        }

        // 同一機器認識番号が同じテーブル要素同士で、通電電流値の小さい方に上流積み上げ区分をセットする。
        for (int i = 0; i < num; i++)
        {
            if (noflag[i])
            {
                continue;
            }

            int no1 = datano[i];
            int kiki1 = EquipmentParameterFormatter.Stoi(mains[no1 - 1].Data.IdentityNumber, 2);

            int no2 = no1;
            int j = 0;
            for (; j < num; j++)
            {
                no2 = datano[j];
                if (no1 == no2)
                {
                    continue;
                }

                int kiki2 = EquipmentParameterFormatter.Stoi(mains[no2 - 1].Data.IdentityNumber, 2);
                if (kiki1 == kiki2)
                {
                    break;
                }
            }

            noflag[i] = true;
            noflag[j] = true; // 【C原典】break 未成立時は j==num(未使用領域)を立てる。UB を忠実再現。

            double tden1 = EquipmentParameterFormatter.Stof(mains[no1 - 1].Data.EnergizingCurrent, 8);
            double tden2 = EquipmentParameterFormatter.Stof(mains[no2 - 1].Data.EnergizingCurrent, 8);

            if (tden1 > tden2)
            {
                mains[no2 - 1].Data.StackKind = '1';
            }
            else
            {
                mains[no1 - 1].Data.StackKind = '1';
            }
        }

        // 下流テーブル要素の通電電流値・積算エリアをクリアする。
        for (int i = 0; i < num; i++)
        {
            int no1 = datano[i];
            ClearAccumulation(mains[no1 - 1]);

            // 【C原典】ret を無視して knum を使用。null(ret!=0)は knum==0 と等価でループしない。
            IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(mains, no1);
            if (downstream is null)
            {
                continue;
            }

            foreach (int no2 in downstream)
            {
                // 負荷発生元区分 == '1' の時は打ち切る。
                if (mains[no2 - 1].Data.LoadSourceKind == '1')
                {
                    break;
                }

                mains[no2 - 1].Data.EnergizingCurrent = "00000.00";
                ClearAccumulation(mains[no2 - 1]);
            }
        }
    }

    // 積算エリア(6 スロット)の全機器種別値をクリアする。【C原典】sk_area[m].?_area = 0.0。
    private static void ClearAccumulation(MainCircuitResult record)
    {
        foreach (AccumulationArea a in record.Work.AccumulationSlots)
        {
            a.A = 0.0;
            a.B = 0.0;
            a.C = 0.0;
            a.D = 0.0;
            a.E = 0.0;
            a.M = 0.0;
            a.S = 0.0;
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
