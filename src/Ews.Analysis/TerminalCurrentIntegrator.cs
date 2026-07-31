using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 通電電流積算(<c>Fyss37_I_Set_Sub</c>)の電流計算リーフ群。負荷容量決定テーブル(FYRT812)を用いた
/// 優先パラメータ解決・電流値算出・設定電流値算出を行う。
///
/// 【C原典】<c>Fyss37_Get_Fuka</c>／<c>Fyss37_Get_DenIa</c>／<c>Fyss37_Get_DenIb</c>／
///          <c>Fyss37_Kei_TR</c>／<c>Fyss37_Set_Tden</c>／<c>Fyss37_Set_Sden</c>
///          (toku/sekkei/src/Fyss37.c)。積算本体(<c>Fyss37_Seki_Tsumi</c>/<c>Fyss37_I_Set_Sub</c>)は後続。
/// </summary>
public static class TerminalCurrentIntegrator
{
    private const int MaxSekiEria = 6;

    /// <summary>
    /// 負荷容量決定テーブルを検索し、負荷電流算出係数と電気パラメータ優先順位(1:AT/2:W/3:VA/4:A1/5:A2)を得る。
    /// テーブル未登録なら false。【C原典】Fyss37_Get_Fuka(yoyaku, fkei, pry, syu)。
    /// </summary>
    public static bool TryGetLoadFactor(MainCircuitResult record, out double factor, out int priority)
    {
        ArgumentNullException.ThrowIfNull(record);

        factor = 0.0;
        priority = 0;

        LoadCapacityEntry? entry = LoadCapacityDecisionTable.Find(record.Data.ReservedWord);
        if (entry is null)
        {
            return false;
        }

        factor = entry.Coefficient;
        ElectricalParameters ep = record.Data.ElectricalParameterSlots[0];

        // 電気パラメータ優先順位(jpry=1,2,3…)の順に、対応する電気パラメータが非ゼロの最初の位置を採用。
        int jpry = 1;
        int kcnt = 0;
        int isave = 0;
        while (kcnt < 5)
        {
            int idx = kcnt;
            kcnt++;
            if (entry.ElectricalPriority[idx] != jpry)
            {
                continue;
            }

            switch (kcnt)
            {
                case 1: if (NonZero(ep.At, "00000.000", 9)) priority = kcnt; break;
                case 2: if (NonZero(ep.W1, "0000000.00", 10)) priority = kcnt; break;
                case 3: if (NonZero(ep.Va, "0000000.00", 10)) priority = kcnt; break;
                case 4: if (NonZero(ep.A1, "00000.000", 9)) priority = kcnt; break;
                default: if (NonZero(ep.A2, "00000.000", 9)) priority = kcnt; break;
            }

            if (priority > 0)
            {
                break;
            }

            isave = kcnt;
            jpry++;
            kcnt = 0;
        }

        if (priority == 0)
        {
            priority = isave;
        }

        return true;
    }

    /// <summary>
    /// 電気パラメータ優先順位で選ばれた電流(AT/W/VA/A1/A2)に係数 0.8 を乗じた値を得る。
    /// 【C原典】Fyss37_Get_DenIa。
    /// </summary>
    public static bool TryGetCurrentIa(MainCircuitResult record, out double current)
    {
        current = 0.0;
        if (!TryGetLoadFactor(record, out _, out int pry))
        {
            return false;
        }

        ElectricalParameters ep = record.Data.ElectricalParameterSlots[0];
        double at = pry switch
        {
            1 => EquipmentParameterFormatter.Stof(ep.At, 9),
            2 => EquipmentParameterFormatter.Stof(ep.W1, 10),
            3 => EquipmentParameterFormatter.Stof(ep.Va, 10),
            4 => EquipmentParameterFormatter.Stof(ep.A1, 9),
            _ => EquipmentParameterFormatter.Stof(ep.A2, 9),
        };

        current = at * 0.8;
        return true;
    }

    /// <summary>
    /// 指定相(phaseIndex)の積算エリアから各相の通電電流値 a+b+(c+d+e)×0.8 を得る。
    /// テーブル未登録なら false。【C原典】Fyss37_Get_DenIb。
    /// </summary>
    public static bool TryGetCurrentIb(MainCircuitResult record, int phaseIndex, out double current)
    {
        current = 0.0;
        if (!TryGetLoadFactor(record, out _, out _))
        {
            return false;
        }

        AccumulationArea a = record.Work.AccumulationSlots[phaseIndex];
        current = a.A + a.B + ((a.C + a.D + a.E) * 0.8);
        return true;
    }

    /// <summary>予約語 'TR' の係数(定格電圧2 / 定格電圧1)を得る。【C原典】Fyss37_Kei_TR。</summary>
    public static double GetTrCoefficient(MainCircuitResult record)
    {
        ArgumentNullException.ThrowIfNull(record);

        ElectricalParameters ep = record.Data.ElectricalParameterSlots[0];

        int t1 = EquipmentParameterFormatter.Stoi(ep.V1Idx, 1);
        if (t1 == 0)
        {
            t1 = 1;
        }

        int t2 = EquipmentParameterFormatter.Stoi(ep.V2Idx, 1);
        if (t2 == 0)
        {
            t2 = 1;
        }

        double epav1 = EquipmentParameterFormatter.Stof(ep.V1[ClampIndex(t1 - 1)], 8);
        double epav2 = EquipmentParameterFormatter.Stof(ep.V2[ClampIndex(t2 - 1)], 8);
        return epav2 / epav1;
    }

    /// <summary>
    /// 通電電流値(denryu)が未設定(0)のとき、積算エリアから算出してセットする。機器選定区分='1' は
    /// 各相の最大値、それ以外は各相の a+b+(c+d+e)×0.8 の最大値。【C原典】Fyss37_Set_Tden。
    /// </summary>
    public static void SetEnergizingCurrent(MainCircuitResult record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (EquipmentParameterFormatter.Stof(record.Data.EnergizingCurrent, 8) != 0.0)
        {
            return;
        }

        double result;
        if (record.Work.EquipmentSelectionKind == '1')
        {
            double denMax = 0.0;
            foreach (AccumulationArea t in record.Work.AccumulationSlots)
            {
                denMax = Math.Max(denMax, Math.Max(Math.Max(t.A, t.B), Math.Max(Math.Max(t.C, t.D), t.E)));
            }

            result = denMax;
        }
        else
        {
            double ikMax = 0.0;
            foreach (AccumulationArea t in record.Work.AccumulationSlots)
            {
                double ik = t.A + t.B + ((t.C + t.D + t.E) * 0.8);
                ikMax = Math.Max(ikMax, ik);
            }

            result = ikMax;
        }

        record.Data.EnergizingCurrent = EquipmentParameterFormatter.SprintfF("%08.2f", result);
    }

    /// <summary>
    /// 機器選定区分が '2'/'3' のとき、積算エリアから設定電流値(Is)を算出してセットする。
    /// 【C原典】Fyss37_Set_Sden。
    /// </summary>
    public static void SetSetCurrent(MainCircuitResult record)
    {
        ArgumentNullException.ThrowIfNull(record);

        char kind = record.Work.EquipmentSelectionKind;
        if (kind != '2' && kind != '3')
        {
            return;
        }

        if (!TryGetLoadFactor(record, out _, out _))
        {
            return;
        }

        int kpav = EquipmentParameterFormatter.Stoi(record.Data.CircuitVoltage[0], 3);
        double kei = kpav > 220 && record.Data.CircuitPhaseCount == '3' ? 2.7 : 5.4;

        double isMax = 0.0;
        double wk2 = 0.0; // 【C原典】s==0 のとき未初期化のまま。wk1=pow(0)=0 で寄与が消えるため 0 で無害。
        for (int i = 0; i < MaxSekiEria; i++)
        {
            AccumulationArea t = record.Work.AccumulationSlots[i];
            double b = t.B;
            double c = t.C;
            double d = t.D;
            double e = t.E;
            double s = t.S / 1000.0;
            double m = t.M / 1000.0;

            double wk1 = Math.Pow(s, 0.94);
            if (s != 0.0)
            {
                wk2 = Math.Pow((s + m) / s, 0.8);
            }

            double wk3 = b * 1.25;
            double wk4 = (c + d + e) * 0.8;
            double isv = (wk1 * wk2 * kei) + wk3 + wk4;

            isMax = Math.Max(isMax, isv);
        }

        record.Work.SetCurrent = isMax;
    }

    // ── 積算本体(Fyss37_I_Set_Sub / Seki_Tsumi / Chk_Break / Mat_flg) ──────────

    /// <summary>
    /// 指示データ追番(oiban)の下流の負荷発生元から通電電流値を上流へ積み上げ、対象・下流機器へ
    /// 通電電流値・設定電流値をセットする。回路要素が主回路('1')でなければ false。
    /// 【C原典】Fyss37_I_Set_Sub(Pmainc, maina, oiban)。
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】maina(件数 Pmainc)。</param>
    /// <param name="oiban">積算対象データ追番(1始まり)。【C原典】oiban。</param>
    public static bool IntegrateCurrent(IReadOnlyList<MainCircuitResult> mains, int oiban)
    {
        ArgumentNullException.ThrowIfNull(mains);

        if (oiban < 1 || oiban > mains.Count || mains[oiban - 1].Data.CircuitElement != '1')
        {
            return false;
        }

        // 積算エリア積み上げフラグ(seki_flag)。データ追番-1 で索引。【C原典】seki_flag[1000]。
        bool[] sekiFlag = new bool[mains.Count];

        IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(mains, oiban);
        if (downstream is null)
        {
            return false;
        }

        // 負荷発生元('1')の積算エリアを上流へ積み上げる。
        foreach (int no in downstream)
        {
            if (no >= 1 && no <= mains.Count && mains[no - 1].Data.LoadSourceKind == '1')
            {
                SekiTsumi(mains, oiban, no, sekiFlag);
            }
        }

        // 積算対象の通電電流値・設定電流値をセットする。
        foreach (int no in downstream)
        {
            if (no >= 1 && no <= mains.Count)
            {
                SetEnergizingCurrent(mains[no - 1]);
                SetSetCurrent(mains[no - 1]);
            }
        }

        SetEnergizingCurrent(mains[oiban - 1]);
        SetSetCurrent(mains[oiban - 1]);
        return true;
    }

    /// <summary>
    /// 対象データ追番(no)の積算エリアを、上流の負荷発生元/上流積上区分/積上フラグに到達するまで
    /// 親へ積み上げる。ブレーカ定格を超える相は定格電流で置き換える。【C原典】Fyss37_Seki_Tsumi。
    /// </summary>
    private static void SekiTsumi(IReadOnlyList<MainCircuitResult> mains, int dno, int no, bool[] sekiFlag)
    {
        int sekiKaisi = 0;
        double[,] fuka = new double[MaxSekiEria, 7];
        double[,] fFuka = new double[MaxSekiEria, 7];
        double[] sIa = new double[MaxSekiEria];
        double[] sFuka = new double[MaxSekiEria];

        // 対象の積算エリアを取得する。【C原典】fuka[i][0..6] = TAI。
        for (int i = 0; i < MaxSekiEria; i++)
        {
            AccumulationArea tai = mains[no - 1].Work.AccumulationSlots[i];
            fuka[i, 0] = tai.A;
            fuka[i, 1] = tai.B;
            fuka[i, 2] = tai.C;
            fuka[i, 3] = tai.D;
            fuka[i, 4] = tai.E;
            fuka[i, 5] = tai.M;
            fuka[i, 6] = tai.S;
            sIa[i] = 0.0;
        }

        while (true)
        {
            if (!TryChkBreak(mains, no, sekiFlag, out int oyano))
            {
                break;
            }

            if (sekiKaisi == 0)
            {
                // 積算前の親の積算エリアを退避。【C原典】f_fuka[i][0..5] = OYA(a,b,c,d,e,s)。
                for (int i = 0; i < MaxSekiEria; i++)
                {
                    AccumulationArea oya = mains[oyano - 1].Work.AccumulationSlots[i];
                    fFuka[i, 0] = oya.A;
                    fFuka[i, 1] = oya.B;
                    fFuka[i, 2] = oya.C;
                    fFuka[i, 3] = oya.D;
                    fFuka[i, 4] = oya.E;
                    fFuka[i, 5] = oya.S;
                }
            }

            int matanFlg = MatFlg(mains, oyano, sekiFlag);

            if (matanFlg == 1 && sekiKaisi == 0)
            {
                TryGetCurrentIa(mains[oyano - 1], out double ia);
                for (int i = 0; i < MaxSekiEria; i++)
                {
                    AccumulationArea oya = mains[oyano - 1].Work.AccumulationSlots[i];
                    oya.A += fuka[i, 0];
                    oya.B += fuka[i, 1];
                    oya.C += fuka[i, 2];
                    oya.D += fuka[i, 3];
                    oya.E += fuka[i, 4];
                    oya.S += fuka[i, 6];

                    TryGetCurrentIb(mains[oyano - 1], i, out double ib);

                    if (ia < ib)
                    {
                        // ブレーカ定格未満 → 当該相を定格電流で置き換える。
                        sIa[i] = ia;
                        fuka[i, 0] = ia; oya.A = ia;
                        fuka[i, 1] = 0.0; oya.B = 0.0;
                        fuka[i, 2] = 0.0; oya.C = 0.0;
                        fuka[i, 3] = 0.0; oya.D = 0.0;
                        fuka[i, 4] = 0.0; oya.E = 0.0;
                        sekiFlag[oyano - 1] = true;
                        sekiKaisi = 1;

                        for (int j = 0; j < MaxSekiEria; j++)
                        {
                            AccumulationArea oyaj = mains[oyano - 1].Work.AccumulationSlots[j];
                            if (oyaj.B != 0.0 || oyaj.C != 0.0 || oyaj.D != 0.0 || oyaj.E != 0.0)
                            {
                                sIa[j] = ia;
                                oyaj.A = ia;
                                oyaj.B = 0.0;
                                oyaj.C = 0.0;
                                oyaj.D = 0.0;
                                oyaj.E = 0.0;
                                fuka[j, 0] = ia;
                                fuka[j, 1] = 0.0;
                                fuka[j, 2] = 0.0;
                                fuka[j, 3] = 0.0;
                                fuka[j, 4] = 0.0;
                            }
                        }

                        break;
                    }

                    if (oya.M < mains[no - 1].Work.AccumulationSlots[i].M)
                    {
                        oya.M = fuka[i, 5];
                    }
                }
            }
            else if (sekiKaisi == 0)
            {
                for (int i = 0; i < MaxSekiEria; i++)
                {
                    AccumulationArea oya = mains[oyano - 1].Work.AccumulationSlots[i];
                    oya.A += fuka[i, 0];
                    oya.B += fuka[i, 1];
                    oya.C += fuka[i, 2];
                    oya.D += fuka[i, 3];
                    oya.E += fuka[i, 4];
                    oya.S += fuka[i, 5]; // 【C原典】s_area += fuka[i][5](=m)。

                    if (oya.M < mains[no - 1].Work.AccumulationSlots[i].M)
                    {
                        oya.M = fuka[i, 5];
                    }
                }
            }
            else
            {
                TryGetCurrentIa(mains[oyano - 1], out double ia);
                for (int i = 0; i < MaxSekiEria; i++)
                {
                    AccumulationArea oya = mains[oyano - 1].Work.AccumulationSlots[i];
                    sFuka[0] = oya.A;
                    sFuka[1] = oya.B;
                    sFuka[2] = oya.C;
                    sFuka[3] = oya.D;
                    sFuka[4] = oya.E;
                    sFuka[5] = oya.S;

                    oya.A = oya.A - fFuka[i, 0] + sIa[i];
                    oya.B -= fFuka[i, 1];
                    oya.C -= fFuka[i, 2];
                    oya.D -= fFuka[i, 3];
                    oya.E -= fFuka[i, 4];
                    oya.S -= fFuka[i, 5];

                    if (oya.M < mains[no - 1].Work.AccumulationSlots[i].M)
                    {
                        oya.M = fuka[i, 5];
                    }

                    double ib = oya.A + oya.B + ((oya.C + oya.D + oya.E) * 0.8);
                    if (ia != 0.0 && ia < ib)
                    {
                        sIa[i] = ia;
                        for (int j = 0; j < 6; j++)
                        {
                            fFuka[i, j] = sFuka[j];
                        }

                        oya.A = ia;
                        oya.B = 0.0;
                        oya.C = 0.0;
                        oya.D = 0.0;
                        oya.E = 0.0;
                        oya.S = 0.0;
                    }
                }
            }

            // 予約語が 'TR' の時は積算エリアを係数倍する。
            if (Matches(mains[oyano - 1].Data.ReservedWord, "TR", 8))
            {
                double kei = GetTrCoefficient(mains[oyano - 1]);
                for (int i = 0; i < MaxSekiEria; i++)
                {
                    AccumulationArea oya = mains[oyano - 1].Work.AccumulationSlots[i];
                    oya.A *= kei; fuka[i, 0] = oya.A;
                    oya.B *= kei; fuka[i, 1] = oya.B;
                    oya.C *= kei; fuka[i, 2] = oya.C;
                    oya.D *= kei; fuka[i, 3] = oya.D;
                    oya.E *= kei; fuka[i, 4] = oya.E;
                    oya.M *= kei; fuka[i, 5] = oya.M;
                    oya.S *= kei; fuka[i, 6] = oya.S;
                }
            }

            if (oyano == dno)
            {
                break;
            }

            no = oyano;
        }
    }

    /// <summary>
    /// 積み上げ処理の継続可否を判定する。親が負荷発生元/上流積上区分/積上済みなら中断(false)。
    /// 【C原典】Fyss37_Chk_Break。継続時は <paramref name="oyano"/> に親データ追番を返す。
    /// </summary>
    private static bool TryChkBreak(IReadOnlyList<MainCircuitResult> mains, int no, bool[] sekiFlag, out int oyano)
    {
        oyano = EquipmentParameterFormatter.Stoi(mains[no - 1].Data.ParentSequenceNumber, 3);
        if (oyano < 1 || oyano > mains.Count)
        {
            return false;
        }

        MainCircuitData parent = mains[oyano - 1].Data;
        return parent.LoadSourceKind != '1' && parent.StackKind != '1' && !sekiFlag[oyano - 1];
    }

    /// <summary>
    /// 末端回路行種先頭機器フラグの確認。先頭機器フラグが空白かつ未積算で、電流系パラメータが
    /// あれば 1。【C原典】Fyss37_Mat_flg。
    /// </summary>
    private static int MatFlg(IReadOnlyList<MainCircuitResult> mains, int datano, bool[] sekiFlag)
    {
        if (datano < 1 || datano > mains.Count)
        {
            return 0;
        }

        MainCircuitResult rec = mains[datano - 1];
        if (rec.Work.LeadingEquipmentFlag != ' ' || sekiFlag[datano - 1])
        {
            return 0;
        }

        if (!TryGetLoadFactor(rec, out _, out _))
        {
            return 0;
        }

        ElectricalParameters ep = rec.Data.ElectricalParameterSlots[0];
        double at = EquipmentParameterFormatter.Stof(ep.At, 9);
        double a1 = EquipmentParameterFormatter.Stof(ep.A1, 9);
        double a2 = EquipmentParameterFormatter.Stof(ep.A2, 9);

        if (Matches(rec.Data.ReservedWord, "CT", 8))
        {
            return a1 != 0.0 ? 1 : 0;
        }

        return at != 0.0 || a1 != 0.0 || a2 != 0.0 ? 1 : 0;
    }

    private static int ClampIndex(int index) => Math.Clamp(index, 0, 2);

    // 【C原典】memcmp(field, zero, width) != 0: ゼロ整形文字列と一致しない(=値がある)。
    private static bool NonZero(string value, string zero, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], zero) != 0;

    // 【C原典】memcmp(a, b, width) == 0: 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
