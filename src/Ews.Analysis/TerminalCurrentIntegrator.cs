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

    private static int ClampIndex(int index) => Math.Clamp(index, 0, 2);

    // 【C原典】memcmp(field, zero, width) != 0: ゼロ整形文字列と一致しない(=値がある)。
    private static bool NonZero(string value, string zero, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], zero) != 0;
}
