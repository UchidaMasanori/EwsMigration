using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 負荷発生元の変更処理。先頭機器(回路要素='1' かつ先頭機器フラグ='1')について、
/// 使用相に応じて積算エリアの相間データを振り替え、負荷発生元区分を立てる。
/// さらに下流(子孫)要素の負荷発生元区分をクリアする。
/// 【C原典】<c>Fyss3F_Fuka_Change</c>(toku/sekkei/src/Fyss3F.c)。外部依存なし。
/// </summary>
public static class LoadSourceChanger
{
    // 積算エリア seki_area の 7 相フィールド(a/b/c/d/e/m/s)へのアクセサ。
    private static readonly (Func<AccumulationArea, double> Get, Action<AccumulationArea, double> Set)[] Fields =
    [
        (a => a.A, (a, v) => a.A = v),
        (a => a.B, (a, v) => a.B = v),
        (a => a.C, (a, v) => a.C = v),
        (a => a.D, (a, v) => a.D = v),
        (a => a.E, (a, v) => a.E = v),
        (a => a.M, (a, v) => a.M = v),
        (a => a.S, (a, v) => a.S = v),
    ];

    /// <summary>
    /// 負荷発生元設定ルーチン。【C原典】<c>Fyss3F_Fuka_Change</c>(Pmainc, maina)。
    /// </summary>
    /// <param name="mains">主回路エリア(有効件数分)。</param>
    public static void ChangeLoadSource(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitResult m = mains[i];
            if (m.Data.CircuitElement != '1' || m.Work.LeadingEquipmentFlag != '1')
            {
                continue;
            }

            m.Data.LoadSourceKind = '1';

            AccumulationArea[] a = m.Work.AccumulationSlots;
            string phase = m.Data.UsedPhase;

            // 積算エリアの相変更(0:R 1:S 2:T 3:X 4:Y)。
            if (Matches(phase, "XN  ", 4))
            {
                XySet(a[3], a[4]);
                ClearPhase(a[4]);
            }
            else if (Matches(phase, "YN  ", 4))
            {
                XySet(a[4], a[3]);
                ClearPhase(a[3]);
            }
            else if (Matches(phase, "RN  ", 4))
            {
                RstSet(a[0], a[1], a[2]);
                ClearPhase(a[1]);
                ClearPhase(a[2]);
            }
            else if (Matches(phase, "SN  ", 4))
            {
                RstSet(a[1], a[0], a[2]);
                ClearPhase(a[0]);
                ClearPhase(a[2]);
            }
            else if (Matches(phase, "TN  ", 4))
            {
                RstSet(a[2], a[0], a[1]);
                ClearPhase(a[0]);
                ClearPhase(a[1]);
            }
            else if (Matches(phase, "RS  ", 4))
            {
                Rst2Set(a, 0, 0, 1, 1, 2, 1, 1, 0, 0, 2);
                ClearPhase(a[2]);
            }
            else if (Matches(phase, "ST  ", 4))
            {
                Rst2Set(a, 1, 1, 2, 2, 0, 2, 2, 1, 1, 0);
                ClearPhase(a[0]);
            }
            else if (Matches(phase, "TR  ", 4))
            {
                Rst2Set(a, 0, 0, 2, 2, 1, 2, 2, 0, 0, 1);
                ClearPhase(a[1]);
            }

            // 下流の要素の負荷発生区分をクリアする。
            ClearLoadSourceFlag(m.SequenceNumber, mains, 0);
        }
    }

    // 積算エリアのＸＹ相をセットする。dest の 0 の相へ origin を複写。【C原典】XY_set。
    private static void XySet(AccumulationArea dest, AccumulationArea origin)
    {
        foreach (var f in Fields)
        {
            if (f.Get(dest) == 0.0)
            {
                f.Set(dest, f.Get(origin));
            }
        }
    }

    // 積算エリアのＲＳＴ相をセットする。dest の 0 の相へ origin1 優先(0 なら origin2)を複写。【C原典】RST_set。
    private static void RstSet(AccumulationArea dest, AccumulationArea origin1, AccumulationArea origin2)
    {
        foreach (var f in Fields)
        {
            if (f.Get(dest) == 0.0)
            {
                if (f.Get(origin1) != 0.0)
                {
                    f.Set(dest, f.Get(origin1));
                }
                else if (f.Get(origin2) != 0.0)
                {
                    f.Set(dest, f.Get(origin2));
                }
            }
        }
    }

    // 積算エリアのＲＳＴ相をセットする(2 相補完)。【C原典】RST2_set。
    private static void Rst2Set(
        AccumulationArea[] a, int k1, int k2, int k3, int k4, int k5,
        int k6, int k7, int k8, int k9, int k10)
    {
        foreach (var f in Fields)
        {
            if (f.Get(a[k1]) == 0.0)
            {
                f.Set(a[k2], f.Get(a[k3]));
                f.Set(a[k4], f.Get(a[k5]));
            }
            else if (f.Get(a[k6]) == 0.0)
            {
                f.Set(a[k7], f.Get(a[k8]));
                f.Set(a[k9], f.Get(a[k10]));
            }
        }
    }

    // 積算エリアの相をクリアする。【C原典】sou_clear。
    private static void ClearPhase(AccumulationArea dest)
    {
        foreach (var f in Fields)
        {
            f.Set(dest, 0.0);
        }
    }

    // 親の追い番を元に子供の負荷設定フラグをクリアする(再帰)。【C原典】clear_ahassei。
    private static void ClearLoadSourceFlag(string oya, IReadOnlyList<MainCircuitResult> mains, int level)
    {
        if (level > 999) // 無限ループを防ぐ(nest level 999 まで)
        {
            return;
        }

        for (int i = 0; i < mains.Count; i++)
        {
            if (Matches(oya, mains[i].Data.ParentSequenceNumber, 3))
            {
                mains[i].Data.LoadSourceKind = ' ';
                ClearLoadSourceFlag(mains[i].SequenceNumber, mains, level + 1);
            }
        }
    }

    // 【C原典】strncmp(a, b, width)==0: 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
