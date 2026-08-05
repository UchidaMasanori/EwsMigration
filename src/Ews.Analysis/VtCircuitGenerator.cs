using System.Globalization;
using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ＶＴ(計器用変圧器)回路の自動生成情報を組み立てる。
/// 【C原典】toku/sekkei/src/Fyss14.c Pre_VT_Make(4694) / SortVTINF(4669) と struct VTINF(124)。
///
/// Fyss14_Make_UpperParm の f/r ループが、上流パラメータ生成後の主回路に対して
/// ＶＴを挿入すべき箇所を判定する(Pre_VT_Make)→挿入する(Mainfile_VT_Make)。
/// 本クラスは判定部(Pre_VT_Make)を移植し、挿入すべき箇所の一覧(<see cref="VtInsertion"/>)と
/// 発生有無ステータスを返す。実際の主回路への挿入(Mainfile_VT_Make)は後続増分で移植する。
/// </summary>
public static class VtCircuitGenerator
{
    /// <summary>データ追番等の固定長フィールド幅(datano/oyatno/goyano/kaisono[3])。</summary>
    private const int SequenceWidth = 3;

    /// <summary>自動生成 VT の予約語。【C原典】"VT      "(本移植ではトリム済みで保持)。</summary>
    private const string VtWord = "VT";

    /// <summary>
    /// ＶＴを自動生成すべき箇所を判定して一覧とステータスを返す。【C原典】Pre_VT_Make(Fyss14.c:4694)。
    ///
    /// 判定対象は「予約語が WH または VM」かつ「回路要素(kiryoso)が '3'」かつ「回路電圧 kpav[0] が "220" 超」の要素。
    /// 同一行種(gyocd)・同一行種グループ番号(gyoglno)内に既に VT があれば発生させず、その VT の直後に
    /// 続く回路要素 '3' を '4' へ格下げする(950515)。それ以外は回路要素 '3' の連続の先頭
    /// (予約語 F で区切る/950905)を求め、その直前を挿入位置(datano_VT)とする。同一挿入位置に
    /// 既存の VT 情報があれば重複挿入しない。最終的に datano_VT 昇順で整列して返す。
    /// </summary>
    /// <param name="records">主回路レコード列(FYRT800 配列相当)。データ追番は index+1 とみなす。回路要素は書き換わる。【C原典】Pmaina。</param>
    /// <returns>ＶＴ挿入情報の一覧とステータス。【C原典】*p_VT / *i_VT と戻り値(0:無 1:有 2:既存VTのみ)。</returns>
    public static VtPreparation PrepareVtInsertions(IReadOnlyList<MainCircuitResult> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var plan = new List<VtInsertion>();
        int count = records.Count;
        int existingVtOnlyCount = 0;   // 【C原典】s_vt: 既存 VT により発生を抑止した件数。

        for (int i = 0; i < count; i++)
        {
            MainCircuitData d = records[i].Data;

            // 【C原典】予約語が WH でも VM でもなければ対象外。
            if (d.ReservedWord != "WH" && d.ReservedWord != "VM")
            {
                continue;
            }

            // 【C原典】回路要素(kiryoso)が '3' でなければ対象外。
            if (d.CircuitElement != '3')
            {
                continue;
            }

            // 【C原典】回路電圧 kpav[0] が "220" 以下は対象外(220V 超のみ VT を発生)。
            if (string.CompareOrdinal(d.CircuitVoltage[0], "220") <= 0)
            {
                continue;
            }

            // 【C原典】950512: 同一行種・同一行種グループ番号内に VT が存在すれば発生させない。
            int vtIndex = -1;
            for (int j = 0; j < count; j++)
            {
                MainCircuitData cand = records[j].Data;
                if (d.LineTypeCode == cand.LineTypeCode
                    && d.LineTypeGroupNumber == cand.LineTypeGroupNumber
                    && cand.ReservedWord == "VT")
                {
                    vtIndex = j;
                    existingVtOnlyCount++;
                    break;
                }
            }

            if (vtIndex >= 0)
            {
                // 【C原典】950515: 既存 VT の直後から続く回路要素 '3' を '4' へ格下げ('4' は読み飛ばし、'3'/'4' 以外で終了)。
                for (int k = vtIndex + 1; k < count; k++)
                {
                    MainCircuitData e = records[k].Data;
                    if (e.CircuitElement == '3')
                    {
                        e.CircuitElement = '4';
                    }
                    else if (e.CircuitElement != '4')
                    {
                        break;
                    }
                }

                continue;
            }

            // 【C原典】回路要素 '3' の連続の先頭を後方に探す(kiryoso!='3' か 予約語 F で区切る/950905)。
            int start = i;
            for (; start >= 0; start--)
            {
                MainCircuitData e = records[start].Data;
                if (e.CircuitElement != '3')
                {
                    break;
                }

                if (e.ReservedWord == "F")
                {
                    break;
                }
            }

            start++;

            // 【C原典】同一挿入位置に既配置の VT 情報があれば重複挿入しない。
            int insertBefore = start + 1;
            bool existVt = false;
            foreach (VtInsertion v in plan)
            {
                if (v.InsertBeforeSequenceNumber == insertBefore)
                {
                    existVt = true;
                    break;
                }
            }

            if (existVt)
            {
                continue;
            }

            // 【C原典】VTINF 設定: datano_WHVM=i+1 / datano_VT=start+1。
            plan.Add(new VtInsertion(
                WhVmSequenceNumber: i + 1,
                InsertBeforeSequenceNumber: insertBefore));
        }

        // 【C原典】qsort(SortVTINF): datano_VT 昇順。
        List<VtInsertion> sorted = plan
            .OrderBy(x => x.InsertBeforeSequenceNumber)
            .ToList();

        // 【C原典】i_vt==0 && s_vt>0 なら 2、そうでなければ i_vt?1:0。
        int status = sorted.Count == 0
            ? (existingVtOnlyCount > 0 ? 2 : 0)
            : 1;

        return new VtPreparation(sorted, status);
    }

    /// <summary>
    /// VTINF を元に主回路データブロックへ VT を挿入しデータ追番を再採番する。
    /// 【C原典】<c>Mainfile_VT_Make</c>(Fyss14.c:4824)。
    ///
    /// 旧主回路を新リストへ複写しつつ、挿入位置(datano_VT)の直前へ VT 要素を挿入し、
    /// データ追番・親データ追番(oyatno)・グループ親データ追番(goyano)を新採番へ付け替える。
    /// VT 要素の各フィールドは挿入位置の要素(発生元)から複写し、発生元の並び替え機器区分は
    /// 1 戻す(950601)。VT に連なる回路要素 '3' は '4' へ格下げする。挿入後、自動生成 VT の
    /// 直後で同一階層・同一並列の要素の並び替え機器区分を 1 戻す(960404)。
    /// 【C原典】と同じく旧リストの要素は再利用され、重ならないように追番が書き換わる。
    /// </summary>
    /// <param name="mains">旧主回路エリア。要素は再利用され採番等が書き換わる。【C原典】*Pmaina(件数 *Pmainc)。</param>
    /// <param name="plan"><see cref="PrepareVtInsertions"/> が返す VT 挿入情報(datano_VT 昇順)。【C原典】p_VT(件数 i_VT)。</param>
    /// <returns>VT 挿入後の新主回路エリア。【C原典】*Pmaina(件数 *Pmainc=mainc+i_VT)。</returns>
    public static IReadOnlyList<MainCircuitResult> InsertVtRecords(
        IReadOnlyList<MainCircuitResult> mains, IReadOnlyList<VtInsertion> plan)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(plan);

        var newList = new List<MainCircuitResult>(mains.Count + plan.Count);
        int j = 0;        // 処理対象 VTINF の位置。
        bool f = false;   // 自動生成 VT に連なる回路要素('3')フラグ。

        for (int i = 0; i < mains.Count; i++)
        {
            // 【C原典】現在位置(datano_VT の直前)に VT を挿入する必要がある場合。
            if (j < plan.Count && plan[j].InsertBeforeSequenceNumber == i + 1)
            {
                f = true;

                MainCircuitData src = mains[i].Data;   // 【C原典】maina[datano_VT-1](=挿入位置の要素=発生元)。
                var vt = new MainCircuitResult();      // Main_Area_Clear 相当(既定初期値)。
                MainCircuitData d = vt.Data;

                vt.SequenceNumber = BranchArraySorter.FormatFixedWidth(newList.Count + 1, SequenceWidth);
                d.SystemNumber = src.SystemNumber;
                d.SystemKind = src.SystemKind;
                d.HierarchyNumber = src.HierarchyNumber;
                d.ParallelNumber = src.ParallelNumber;
                d.SortKind = src.SortKind;
                src.SortKind = (char)(src.SortKind - 1);   // 【C原典】950601: 発生元の並び替え機器区分を 1 戻す。
                d.AutoGenerationKind = '1';
                d.ReservedWord = VtWord;
                d.LineTypeCode = src.LineTypeCode;
                d.LineTypeNumber = src.LineTypeNumber;     // 【C原典】1994/11/23 亀田。
                d.LineTypeGroupNumber = src.LineTypeGroupNumber;
                d.AttachedParameter.DimensionGroupNumber = src.AttachedParameter.DimensionGroupNumber;
                d.AttachedParameter.CommentGroupNumber = src.AttachedParameter.CommentGroupNumber;
                d.ElectricalParameterSlots[0].Bn = src.ElectricalParameterSlots[0].Bn;
                d.ElectricalParameterSlots[0].Qty = '1';
                d.IncomingNumber = src.IncomingNumber;
                d.CircuitClass = src.CircuitClass;
                d.CircuitNumberSuffix = src.CircuitNumberSuffix;
                d.CircuitElement = '4';

                // 【C原典】950512: 同一行種・同一行種グループに予約語 F があればタイプ "FN"、無ければ "FU"。
                //   (C原典は maina[k] を参照するが意図は生成 VT 自身の行種=発生元と同一。)
                string dataType = "FU     ";
                for (int l = 0; l < mains.Count; l++)
                {
                    MainCircuitData cand = mains[l].Data;
                    if (d.LineTypeCode == cand.LineTypeCode
                        && d.LineTypeGroupNumber == cand.LineTypeGroupNumber
                        && cand.ReservedWord == "F")
                    {
                        dataType = "FN     ";
                        break;
                    }
                }

                d.DataType[0] = dataType;

                newList.Add(vt);
                j++;
            }

            // 【C原典】旧データの複写とデータ追番・親/グループ親データ追番の付け替え。
            MainCircuitResult cur = mains[i];
            cur.SequenceNumber = BranchArraySorter.FormatFixedWidth(newList.Count + 1, SequenceWidth);
            int n = EquipmentParameterFormatter.Stoi(cur.Data.ParentSequenceNumber, SequenceWidth);
            if (n != 0)
            {
                cur.Data.ParentSequenceNumber = mains[n - 1].SequenceNumber;
            }

            n = EquipmentParameterFormatter.Stoi(cur.Data.GroupParentSequenceNumber, SequenceWidth);
            if (n != 0)
            {
                cur.Data.GroupParentSequenceNumber = mains[n - 1].SequenceNumber;
            }

            // 【C原典】回路要素が '3' でなければフラグを落とし、フラグ中('3' が連なる)なら '4' へ格下げ。
            if (cur.Data.CircuitElement != '3')
            {
                f = false;
            }

            if (f)
            {
                cur.Data.CircuitElement = '4';
            }

            newList.Add(cur);
        }

        // 【C原典】960404: 自動生成 VT('4')直後で同一階層・同一並列の要素の並び替え機器区分を 1 戻す。
        for (int i = 0; i < newList.Count; i++)
        {
            MainCircuitData e = newList[i].Data;
            if (e.ReservedWord != VtWord || e.AutoGenerationKind != '1' || e.CircuitElement != '4')
            {
                continue;
            }

            char kiryoso = e.CircuitElement;   // '4'
            int k = EquipmentParameterFormatter.Stoi(e.HierarchyNumber, SequenceWidth);
            int h = EquipmentParameterFormatter.Stoi(e.ParallelNumber, SequenceWidth);

            // 【C原典】直前が回路要素 '1' かつ atoi(kaisono)+1 が階層と一致しない場合のみ調整。
            //   atoi は kaisono→heino→chokuno→yoyakkbn の物理隣接を数字が続く限り越境読取する。
            if (i > 0 && newList[i - 1].Data.CircuitElement == '1'
                && 1 + AtoiAcrossHierarchy(newList[i - 1].Data) != k)
            {
                bool endFlg = false;
                for (int m = i + 1; m < newList.Count; m++)
                {
                    MainCircuitData nd = newList[m].Data;
                    if (kiryoso != nd.CircuitElement)
                    {
                        break;
                    }

                    int kn = EquipmentParameterFormatter.Stoi(nd.HierarchyNumber, SequenceWidth);
                    int hn = EquipmentParameterFormatter.Stoi(nd.ParallelNumber, SequenceWidth);
                    if (k == kn && h == hn && !endFlg)
                    {
                        if (nd.SortKind is '4' or '2')
                        {
                            nd.SortKind = (char)(nd.SortKind - 1);
                        }
                    }
                    else if (k < kn || h < hn)
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

/// <summary>
/// ＶＴ自動生成の 1 挿入分の情報。【C原典】struct VTINF(Fyss14.c:124)。
/// </summary>
/// <param name="WhVmSequenceNumber">発生原因となる WH/VM のデータ追番(1 始まり)。【C原典】datano_WHVM。</param>
/// <param name="InsertBeforeSequenceNumber">ＶＴを挿入するべき要素の直前のデータ追番。【C原典】datano_VT。</param>
public sealed record VtInsertion(
    int WhVmSequenceNumber,
    int InsertBeforeSequenceNumber);

/// <summary>
/// ＶＴ自動生成の判定結果。【C原典】Pre_VT_Make の出力(*p_VT/*i_VT)と戻り値。
/// </summary>
/// <param name="Insertions">ＶＴ挿入情報の一覧(datano_VT 昇順)。【C原典】*p_VT(件数 *i_VT)。</param>
/// <param name="Status">発生有無ステータス。0:発生なし 1:発生あり 2:既存 VT により全件抑止(s_vt&gt;0 かつ i_vt==0)。【C原典】Pre_VT_Make の戻り値。</param>
public sealed record VtPreparation(
    IReadOnlyList<VtInsertion> Insertions,
    int Status);
