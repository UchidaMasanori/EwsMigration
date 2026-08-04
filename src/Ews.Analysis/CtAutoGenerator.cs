using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ＣＴの自動生成。【C原典】<c>Pre_CT_Make</c> / <c>Mainfile_CT_Make</c>(toku/sekkei/src/Fyss15.c)。
///
/// AM(電流計)で定格電流2が 30A を越える末端要素の前後に計器回路 CT を自動挿入する。
/// 本段階では検出・挿入位置リスト作成(<see cref="PrepareCtCreation"/>=Pre_CT_Make)までを移植する。
/// 実挿入(Mainfile_CT_Make)は後段で移植する。
/// </summary>
public static class CtAutoGenerator
{
    /// <summary>CT 作成情報。【C原典】<c>struct CTINF</c>(Fyss15.c)。</summary>
    public sealed class CtInfo
    {
        /// <summary>発生原因となる WH/AM のデータ追番(1 始まり)。【C原典】datano_WHAM。</summary>
        public int CauseDataNumber { get; set; }

        /// <summary>CT を挿入するべき要素の直前のデータ追番。【C原典】datano_CT。</summary>
        public int InsertBeforeDataNumber { get; set; }
    }

    /// <summary>予約語(8 バイト右詰め)。【C原典】memcmp(...,"AM      ",8)。</summary>
    private const string AmWord = "AM      ";

    /// <summary>定格電流(A2)フィールド幅。【C原典】sizeof(ep[1].epaa2)=9。</summary>
    private const int RatedCurrentWidth = 9;

    /// <summary>
    /// CT を作成する数・個所・原因要素のリストを作る。【C原典】<c>Pre_CT_Make</c>(Fyss15.c)。
    /// 予約語 AM・回路要素 '1'・定格電流2 が 30A 超の要素につき、直前(i)・直後(i+1)の 2 箇所へ
    /// CT を挿入する位置対を作り、同一対の重複を除いて datano_CT 昇順に整列して返す。
    /// (改訂&lt;2&gt;で WH 経路は削除済み。)
    /// </summary>
    /// <param name="mains">主回路エリア。【C原典】Pmaina[](件数 Pmainc)。</param>
    /// <returns>挿入位置対のリスト(2 件単位)。CT が無ければ空。【C原典】*p_CT / 戻り値 i_ct?1:0。</returns>
    public static IReadOnlyList<CtInfo> PrepareCtCreation(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        var list = new List<CtInfo>();

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;

            // 予約語 AM 以外は対象外(改訂<2>: WH 経路は削除済み)。
            if ((d.ReservedWord ?? string.Empty).PadRight(8)[..8] != AmWord)
            {
                continue;
            }

            if (d.CircuitElement != '1')
            {
                continue;
            }

            // AM: 定格電流2 が 30A 以下は対象外。
            double den = EquipmentParameterFormatter.Stof(d.ElectricalParameterSlots[1].A2, RatedCurrentWidth);
            if (den >= 0.0 && den <= 30.0)
            {
                continue;
            }

            // j:前挿入位置 / l:後挿入位置。
            int j = i;
            int l = i + 1;

            // 同一系列に既配置の CT が存在する場合は追加しない。
            bool existCt = false;
            for (int k = 0; k + 1 < list.Count; k += 2)
            {
                if (list[k].InsertBeforeDataNumber == j && list[k + 1].InsertBeforeDataNumber == l)
                {
                    existCt = true;
                    break;
                }
            }

            if (existCt)
            {
                continue;
            }

            list.Add(new CtInfo { CauseDataNumber = i + 1, InsertBeforeDataNumber = j });
            list.Add(new CtInfo { CauseDataNumber = i + 1, InsertBeforeDataNumber = l });
        }

        // 挿入順(datano_CT 昇順)にソートする。【C原典】qsort(..., SortCTINF)。
        list.Sort(static (a, b) => a.InsertBeforeDataNumber - b.InsertBeforeDataNumber);
        return list;
    }
}
