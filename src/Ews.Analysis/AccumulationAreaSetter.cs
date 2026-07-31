using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 負荷発生元の通電電流値・負荷容量を積算エリア(sk_area)にセットし、末端側へ伝播する。末端回路の
/// 通電電流値算出(<c>Fyss36_MattanKairo_Iset</c>)の下請け。
///
/// 【C原典】<c>Fyss36_Set_Seki</c>＋<c>Fyss36_Get_Pdno</c>＋<c>Fyss36_Get_Are1</c>＋
///          <c>Fyss36_Get_Are2</c>＋<c>Fyss36_Get_Seki</c>(toku/sekkei/src/Fyss36.c)。
///
/// 相(R,S,T,X,Y,N の 6 スロット)×機器種別(A,B,C,D,E,M,S)の組み合わせで、A～E に通電電流値、
/// M/S に負荷容量を格納する。相の判定は回路相数(kpaph)・線式(kpawr)・極数(kpap)と、グループ親・
/// Ｐ系統の相数の組み合わせで決まる。
/// </summary>
public static class AccumulationAreaSetter
{
    private const int Seki = 6;

    /// <summary>
    /// 指定データ追番(負荷発生元)の通電電流値・負荷容量を積算エリアへセットする。
    /// 【C原典】Fyss36_Set_Seki(no, num, syu)。
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】syu(件数 num)。</param>
    /// <param name="dataNumber">対象データ追番(1始まり)。【C原典】no。</param>
    public static void SetLoadSourceAccumulation(IReadOnlyList<MainCircuitResult> mains, int dataNumber)
    {
        ArgumentNullException.ThrowIfNull(mains);

        int index = FindByDataNumber(mains, dataNumber);
        if (index < 0)
        {
            return;
        }

        MainCircuitData d = mains[index].Data;
        int goyano = EquipmentParameterFormatter.Stoi(
            Matches(d.GroupParentSequenceNumber, "000", 3) ? d.ParentSequenceNumber : d.GroupParentSequenceNumber, 3);
        int kno = EquipmentParameterFormatter.Stoi(d.SystemNumber, 3);
        double tden = EquipmentParameterFormatter.Stof(d.EnergizingCurrent, 8);
        double lw = EquipmentParameterFormatter.Stof(d.AttachedParameter.LoadCapacity, 7);

        int pdno = GetPdno(mains, kno);
        int[] area1 = GetArea1(mains, dataNumber, goyano, pdno);
        int[] area2 = GetArea2(mains, dataNumber);

        for (int i = 0; i < Seki; i++)
        {
            if (area1[i] != 1)
            {
                continue;
            }

            AccumulationArea slot = mains[index].Work.AccumulationSlots[i];
            for (int j = 0; j < 7; j++)
            {
                if (area2[j] != 1)
                {
                    continue;
                }

                switch (j)
                {
                    case 0: slot.A = tden; break;
                    case 1: slot.B = tden; break;
                    case 2: slot.C = tden; break;
                    case 3: slot.D = tden; break;
                    case 4: slot.E = tden; break;
                    case 5: slot.M = lw; break;
                    default: slot.S = lw; break;
                }
            }
        }
    }

    /// <summary>
    /// 指定データ追番の通電電流値が 0 のとき、負荷発生元区分='1' の機器まで上流を遡り、その通電電流値
    /// ・積算エリアを対象データ追番および途中の機器へセットする。
    /// 【C原典】Fyss36_Get_Seki(no, num, syu)。
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】syu(件数 num)。</param>
    /// <param name="dataNumber">対象データ追番(1始まり)。【C原典】no。</param>
    public static void PropagateCurrentFromLoadSource(IReadOnlyList<MainCircuitResult> mains, int dataNumber)
    {
        ArgumentNullException.ThrowIfNull(mains);

        int set = FindByDataNumber(mains, dataNumber);
        if (set < 0)
        {
            return;
        }

        // 通電電流値 != 0 なら既に確定済みで処理不要。
        if (EquipmentParameterFormatter.Stof(mains[set].Data.EnergizingCurrent, 8) != 0.0)
        {
            return;
        }

        // 上流に負荷発生元区分='1' の機器を探す。【C原典】第1 while ループ。
        int loadSource = FindUpstreamLoadSource(mains, mains[set].Data.ParentSequenceNumber);
        if (loadSource < 0)
        {
            // C原典は負荷発生元不在で配列外参照(UB)。本移行では何もせず返す(正常データでは必ず存在)。
            return;
        }

        // 負荷発生元 → 対象データ追番へ通電電流値・積算エリアをコピー。
        CopyCurrentAndArea(mains[loadSource], mains[set]);

        // 負荷発生元に至るまでの途中機器にも同様にセットする。【C原典】第2 while ループ(950124)。
        int oyano = EquipmentParameterFormatter.Stoi(mains[set].Data.ParentSequenceNumber, 3);
        for (int guard = 0; guard <= mains.Count; guard++)
        {
            int i = FindByDataNumber(mains, oyano);
            if (i < 0 || mains[i].Data.LoadSourceKind == '1')
            {
                break;
            }

            oyano = EquipmentParameterFormatter.Stoi(mains[i].Data.ParentSequenceNumber, 3);
            if (oyano == 0)
            {
                break;
            }

            CopyCurrentAndArea(mains[set], mains[i]);
        }
    }

    /// <summary>
    /// 上流(oyatno を辿る)で負荷発生元区分='1' の機器の添字を返す(無ければ-1)。
    /// 【C原典】Fyss36_Get_Seki の第1 while ループ(0ガード無しのため -1 で安全終了)。
    /// </summary>
    private static int FindUpstreamLoadSource(IReadOnlyList<MainCircuitResult> mains, string startParent)
    {
        int oyano = EquipmentParameterFormatter.Stoi(startParent, 3);
        for (int guard = 0; guard <= mains.Count; guard++)
        {
            int i = FindByDataNumber(mains, oyano);
            if (i < 0)
            {
                return -1;
            }

            if (mains[i].Data.LoadSourceKind == '1')
            {
                return i;
            }

            oyano = EquipmentParameterFormatter.Stoi(mains[i].Data.ParentSequenceNumber, 3);
        }

        return -1;
    }

    /// <summary>通電電流値(denryu)と積算エリア(sk_area 全 6 スロット)を複写する。【C原典】memcpy 群。</summary>
    private static void CopyCurrentAndArea(MainCircuitResult from, MainCircuitResult to)
    {
        to.Data.EnergizingCurrent = from.Data.EnergizingCurrent;
        for (int j = 0; j < Seki; j++)
        {
            AccumulationArea s = from.Work.AccumulationSlots[j];
            AccumulationArea d = to.Work.AccumulationSlots[j];
            d.A = s.A;
            d.B = s.B;
            d.C = s.C;
            d.D = s.D;
            d.E = s.E;
            d.M = s.M;
            d.S = s.S;
        }
    }

    /// <summary>系統番号が一致し予約語が 'P' のデータ追番を取得(無ければ0)。【C原典】Fyss36_Get_Pdno。</summary>
    private static int GetPdno(IReadOnlyList<MainCircuitResult> mains, int kno)
    {
        foreach (MainCircuitResult m in mains)
        {
            if (EquipmentParameterFormatter.Stoi(m.Data.SystemNumber, 3) == kno &&
                Matches(m.Data.ReservedWord, "P", 8))
            {
                return EquipmentParameterFormatter.Stoi(m.SequenceNumber, 3);
            }
        }

        return 0;
    }

    /// <summary>
    /// 相(R,S,T,X,Y,N)積算エリアのフラグを立てる。データ追番・グループ親・Ｐ系統の相数/線式/極数の
    /// 組み合わせで判定する。【C原典】Fyss36_Get_Are1。
    /// </summary>
    private static int[] GetArea1(IReadOnlyList<MainCircuitResult> mains, int dno, int gno, int pno)
    {
        int[] area = new int[Seki];

        char dPh = '\0', dWr = '\0', dPap = '\0';
        char gPh = '\0', gWr = '\0';
        char pPh = '\0';
        int heino = 0;

        int di = FindByDataNumber(mains, dno);
        if (di >= 0)
        {
            MainCircuitData d = mains[di].Data;
            dPh = d.CircuitPhaseCount;
            dWr = d.CircuitWireType;
            dPap = d.CircuitPoleCount;
            heino = EquipmentParameterFormatter.Stoi(d.ParallelNumber, 3);
        }

        int gi = FindByDataNumber(mains, gno);
        if (gi >= 0)
        {
            gPh = mains[gi].Data.CircuitPhaseCount;
            gWr = mains[gi].Data.CircuitWireType;
        }

        int pi = FindByDataNumber(mains, pno);
        if (pi >= 0)
        {
            pPh = mains[pi].Data.CircuitPhaseCount;
        }

        // R=0,S=1,T=2,X=3,Y=4,N=5。以下は C 原典どおり独立した if の集合(複数成立で加算)。
        if (gPh == '1' && gWr == '2' && dPh == '1' && dWr == '2' && dPap == '1' && pPh == '1')
        {
            area[3] = 1;
        }

        if (gPh == '1' && gWr == '2' && dPh == '1' && dWr == '2' && dPap == '2' && pPh == '1')
        {
            area[3] = 1;
            area[4] = 1;
        }

        if (gPh == '1' && gWr == '2' && dPh == '1' && dWr == '2' && pPh == '3')
        {
            area[0] = 1;
            area[1] = 1;
        }

        if (gPh == '1' && gWr == '3' && dPh == '1' && dWr == '3' && pPh == '1')
        {
            area[3] = 1;
            area[4] = 1;
        }

        if (gPh == '1' && gWr == '3' && dPh == '1' && dWr == '2' && dPap == '1' && pPh == '1')
        {
            if (heino % 2 == 1)
            {
                area[3] = 1;
            }
            else
            {
                area[4] = 1;
            }
        }

        if (gPh == '1' && gWr == '3' && dPh == '1' && dWr == '2' && dPap == '2' && pPh == '1')
        {
            area[3] = 1;
            area[4] = 1;
        }

        if (gPh == '1' && gWr == '3' && dPh == '1' && dWr == '3' && pPh == '3')
        {
            area[0] = 1;
            area[1] = 1;
            area[3] = 1;
            area[4] = 1;
        }

        if (gPh == '1' && gWr == '3' && dPh == '1' && dWr == '2' && pPh == '3')
        {
            if (heino % 2 == 1)
            {
                area[0] = 1;
            }
            else
            {
                area[1] = 1;
            }
        }

        if (gPh == '3' && gWr == '3' && dPh == '3' && dWr == '3')
        {
            area[0] = 1;
            area[1] = 1;
            area[2] = 1;
        }

        if (gPh == '3' && gWr == '3' && dPh == '1' && dWr == '2')
        {
            if (heino % 3 == 1)
            {
                area[0] = 1;
                area[1] = 1;
            }
            else if (heino % 3 == 2)
            {
                area[1] = 1;
                area[2] = 1;
            }
            else
            {
                area[2] = 1;
                area[0] = 1;
            }
        }

        if (gPh == '3' && gWr == '4' && dPh == '3' && dWr == '3')
        {
            area[0] = 1;
            area[1] = 1;
            area[2] = 1;
        }

        if (gPh == '3' && gWr == '4' && dPh == '1' && dWr == '3')
        {
            area[0] = 1;
            area[1] = 1;
        }

        if (gPh == '3' && gWr == '4' && dPh == '1' && dWr == '2' && dPap == '1')
        {
            area[0] = 1;
        }

        if (gPh == '3' && gWr == '4' && dPh == '1' && dWr == '2' && dPap == '2')
        {
            area[0] = 1;
            area[1] = 1;
        }

        if (gPh == '0' && gWr == '0')
        {
            area[3] = 1;
            area[4] = 1;
        }

        return area;
    }

    /// <summary>
    /// 機器種別(A,B,C,D,E,M,S)積算エリアのフラグを立てる。負荷容量の有無・負荷種類・相数で判定。
    /// 【C原典】Fyss36_Get_Are2。
    /// </summary>
    private static int[] GetArea2(IReadOnlyList<MainCircuitResult> mains, int dno)
    {
        int[] area = new int[7];

        int di = FindByDataNumber(mains, dno);
        if (di < 0)
        {
            return area;
        }

        MainCircuitData d = mains[di].Data;
        char kpaph = d.CircuitPhaseCount;
        double fpalw2 = EquipmentParameterFormatter.Stof(d.AttachedParameter.LoadCapacity, 7);

        if (fpalw2 != 0.0)
        {
            if (Matches(d.AttachedParameter.LoadKind, "M ", 2))
            {
                if (kpaph == '1')
                {
                    area[1] = 1; // B
                }

                if (kpaph == '3')
                {
                    area[0] = 1; // A
                    area[5] = 1; // M
                    area[6] = 1; // S
                }
            }
            else
            {
                area[2] = 1; // C
            }
        }
        else
        {
            if (kpaph == '1')
            {
                area[4] = 1; // E
            }

            if (kpaph == '3')
            {
                area[3] = 1; // D
            }
        }

        return area;
    }

    /// <summary>データ追番(1始まり)に一致する主回路レコードの添字を返す(無ければ-1)。</summary>
    private static int FindByDataNumber(IReadOnlyList<MainCircuitResult> mains, int dataNumber)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            if (EquipmentParameterFormatter.Stoi(mains[i].SequenceNumber, 3) == dataNumber)
            {
                return i;
            }
        }

        return -1;
    }

    // 【C原典】memcmp/strncmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
