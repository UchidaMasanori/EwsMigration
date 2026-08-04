using System.Globalization;
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

    /// <summary>予約語 CT(8 バイト右詰め)。【C原典】"CT      "。</summary>
    private const string CtWord = "CT      ";

    /// <summary>CT の 2 次側電流。【C原典】"00005.000"。</summary>
    private const string CtSecondaryCurrent = "00005.000";

    /// <summary>定格電流1 の未入力値。【C原典】"00000.000"。</summary>
    private const string ZeroCurrent = "00000.000";

    /// <summary>
    /// CTINF を元に主回路データブロックへ CT を挿入し関連情報を再作成する。
    /// 【C原典】<c>Mainfile_CT_Make</c>(Fyss15.c)。
    ///
    /// (1) WH/AM の定格電流1が未入力なら定格電流2→1へ写し定格電流2を 5A にする。
    /// (2) 旧主回路を新リストへ複写しつつ CT 要素を挿入位置対の直前へ挿入し、
    ///     データ追番・親/グループ親データ追番を新採番へ付け替える。挿入対の間に挟まれる
    ///     WH/AM 本体および偶数番目の CT は回路要素を '2'(自動生成 CT 群)にする。
    /// (3) 挿入で階層番号が重なる要素の階層番号を繰り上げ、CT と同階層・同並列の要素の
    ///     並び替え機器区分を 1 戻す。
    /// </summary>
    /// <param name="mains">旧主回路エリア。【C原典】*Pmaina(件数 *Pmainc)。要素は再利用され採番等が書き換わる。</param>
    /// <param name="ctList"><see cref="PrepareCtCreation"/> が返す挿入位置対(datano_CT 昇順)。【C原典】p_CT(件数 i_CT)。</param>
    /// <returns>CT 挿入後の新主回路エリア。【C原典】*Pmaina(件数 *Pmainc=mainc+i_CT)。</returns>
    public static IReadOnlyList<MainCircuitResult> InsertCtIntoMainCircuit(
        IReadOnlyList<MainCircuitResult> mains, IReadOnlyList<CtInfo> ctList)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(ctList);

        // (1) WH/AM の定格電流を 2 から 1 にコピーする。【C原典】950322。
        foreach (CtInfo ct in ctList)
        {
            ElectricalParameters ep0 = mains[ct.CauseDataNumber - 1].Data.ElectricalParameterSlots[0];
            if (ep0.A1 == ZeroCurrent)
            {
                ep0.A1 = ep0.A2;
                ep0.A2 = CtSecondaryCurrent;
            }
        }

        // (2) 旧から新への移行および CT 要素の挿入。
        var newList = new List<MainCircuitResult>(mains.Count + ctList.Count);
        int j = 0;    // 処理対象 CTINF の位置
        bool f = false;   // 自動生成 CT の間に含まれる要素フラグ

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitResult cur = mains[i];

            // データ追番・親データ追番・グループ親データ追番の付け替え。
            cur.SequenceNumber = BranchArraySorter.FormatFixedWidth(newList.Count + 1, 3);
            int n = EquipmentParameterFormatter.Stoi(cur.Data.ParentSequenceNumber, 3);
            if (n != 0)
            {
                cur.Data.ParentSequenceNumber = mains[n - 1].SequenceNumber;
            }

            n = EquipmentParameterFormatter.Stoi(cur.Data.GroupParentSequenceNumber, 3);
            if (n != 0)
            {
                cur.Data.GroupParentSequenceNumber = mains[n - 1].SequenceNumber;
            }

            // 自動生成 CT の間に含まれる要素は回路要素を '2' にする。
            if (f)
            {
                cur.Data.CircuitElement = '2';
            }

            newList.Add(cur);

            // 現在位置に CT を挿入する必要がある場合。
            if (j < ctList.Count && ctList[j].InsertBeforeDataNumber == i + 1)
            {
                MainCircuitResult ctElem = mains[ctList[j].InsertBeforeDataNumber - 1];
                MainCircuitResult wham = mains[ctList[j].CauseDataNumber - 1];

                var ct = new MainCircuitResult();   // Main_Area_Clear 相当(既定初期値)。
                MainCircuitData d = ct.Data;
                ElectricalParameters cep0 = ctElem.Data.ElectricalParameterSlots[0];

                ct.SequenceNumber = BranchArraySorter.FormatFixedWidth(newList.Count + 1, 3);
                d.SystemNumber = ctElem.Data.SystemNumber;
                d.SystemKind = ctElem.Data.SystemKind;
                d.CircuitClass = ctElem.Data.CircuitClass;
                d.CircuitNumberSuffix = wham.Data.CircuitNumberSuffix;
                d.HierarchyNumber = wham.Data.HierarchyNumber;
                d.ParallelNumber = wham.Data.ParallelNumber;
                d.AutoGenerationKind = '1';
                d.ReservedWord = CtWord;
                d.LineTypeCode = ctElem.Data.LineTypeCode;
                d.LineTypeNumber = ctElem.Data.LineTypeNumber;
                d.LineTypeGroupNumber = ctElem.Data.LineTypeGroupNumber;
                d.AttachedParameter.DimensionGroupNumber = wham.Data.AttachedParameter.DimensionGroupNumber;
                d.AttachedParameter.CommentGroupNumber = wham.Data.AttachedParameter.CommentGroupNumber;
                d.ElectricalParameterSlots[0].A1 = cep0.A1;
                d.ElectricalParameterSlots[0].A2 = cep0.A2;
                d.AttachedParameter.SpFutureMountKind = ctElem.Data.AttachedParameter.SpFutureMountKind;
                d.ElectricalParameterSlots[0].Bn = cep0.Bn;
                d.ElectricalParameterSlots[0].Qty = '1';
                d.IncomingNumber = ctElem.Data.IncomingNumber;

                char whamNara = wham.Data.SortKind;
                d.SortKind = whamNara is '1' or '3' ? (char)(whamNara + 1) : whamNara;

                if (j % 2 == 0)
                {
                    d.CircuitElement = '2';
                    f = true;
                }
                else
                {
                    d.CircuitElement = '1';
                    f = false;
                }

                d.DataType[1] = "KT     ";
                newList.Add(ct);
                j++;
            }
        }

        // (3) 直前が自動生成 CT だった場合の階層番号・並び替え機器区分の調整。
        for (int i = 0; i < newList.Count; i++)
        {
            bool endFlg = false;
            MainCircuitData e = newList[i].Data;

            // 挿入 CT('2')で階層が重なる後続要素の階層番号を繰り上げる。
            if (e.ReservedWord.PadRight(8)[..8] == CtWord && e.CircuitElement == '2')
            {
                int k = EquipmentParameterFormatter.Stoi(e.HierarchyNumber, 3);

                // atoi(kaisono) は kaisono→heino→chokuno→yoyakkbn の物理隣接を数字が続く限り読む
                // (fydf806.h)。この越境読取のため通常この条件はほぼ常に真になる忠実仕様。
                if (i > 0 && newList[i - 1].Data.CircuitElement == '1'
                    && 1 + AtoiAcrossHierarchy(newList[i - 1].Data) != k)
                {
                    for (int m = i; m < newList.Count; m++)
                    {
                        int kn = EquipmentParameterFormatter.Stoi(newList[m].Data.HierarchyNumber, 3);
                        if (k == kn && !endFlg)
                        {
                            newList[m].Data.HierarchyNumber = BranchArraySorter.FormatFixedWidth(kn + 1, 3);
                        }
                        else if (k < kn)
                        {
                            newList[m].Data.HierarchyNumber = BranchArraySorter.FormatFixedWidth(kn + 1, 3);
                            endFlg = true;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            // CT と同階層・同並列の後続要素の並び替え機器区分を 1 戻す。
            if (e.ReservedWord.PadRight(8)[..8] == CtWord)
            {
                int k = EquipmentParameterFormatter.Stoi(e.HierarchyNumber, 3);
                int t = EquipmentParameterFormatter.Stoi(e.ParallelNumber, 3);
                char kiryoso = e.CircuitElement;

                for (int m = i + 1; m < newList.Count; m++)
                {
                    MainCircuitData nd = newList[m].Data;
                    if (kiryoso != nd.CircuitElement)
                    {
                        break;
                    }

                    int kn = EquipmentParameterFormatter.Stoi(nd.HierarchyNumber, 3);
                    int tn = EquipmentParameterFormatter.Stoi(nd.ParallelNumber, 3);
                    if (k == kn && t == tn && !endFlg)
                    {
                        if (nd.SortKind is '4' or '2')
                        {
                            nd.SortKind = (char)(nd.SortKind - 1);
                        }
                    }
                    else if (k < kn || t < tn)
                    {
                        endFlg = true;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        return newList;
    }

    /// <summary>
    /// 階層番号(kaisono)から始まる物理隣接フィールドを数字が続く限り atoi する。
    /// 【C原典】<c>atoi(newmaina[i-1].dt.kaisono)</c>。kaisono[3]→heino[3]→chokuno[3]→yoyakkbn の
    /// 順に連なる数字を 1 つの整数として読む(fydf806.h の並び)。桁溢れ回避のため long で保持する。
    /// </summary>
    private static long AtoiAcrossHierarchy(MainCircuitData d)
    {
        string s = Fix3(d.HierarchyNumber) + Fix3(d.ParallelNumber) + Fix3(d.SeriesNumber) + d.AutoGenerationKind;

        int i = 0;
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t'))
        {
            i++;
        }

        int start = i;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            i++;
        }

        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            i++;
        }

        string num = s[start..i];
        return long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : 0L;
    }

    /// <summary>3 バイト固定長フィールドの物理表現(0 詰め左寄せ 3 桁)を得る。</summary>
    private static string Fix3(string s) => s.Length >= 3 ? s[..3] : s.PadLeft(3, '0');
}
