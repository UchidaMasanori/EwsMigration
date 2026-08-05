using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ＮＴ(中性線端子)回路の自動生成情報を組み立てる。
/// 【C原典】toku/sekkei/src/Fyss14.c Pre_NT_Make(4257) / SortNTINF(4229) と struct NTINF(115)。
///
/// Fyss14_Make_UpperParm の f/r ループが、上流パラメータ生成後の主回路に対して
/// ＮＴを挿入すべき箇所を判定する(Pre_NT_Make)→挿入する(Mainfile_NT_Make)。
/// 本クラスは判定部(Pre_NT_Make)と挿入部(Mainfile_NT_Make)を移植する。
/// </summary>
public static class NtCircuitGenerator
{
    /// <summary>データ追番フィールド幅(datano/oyatno/goyano/kaisono[3])。</summary>
    private const int SequenceWidth = 3;

    /// <summary>自動生成 NT の予約語。【C原典】"NT      "(本移植ではトリム済みで保持)。</summary>
    private const string NtWord = "NT";

    /// <summary>
    /// ＮＴを自動生成すべき箇所を判定して一覧を返す。【C原典】Pre_NT_Make(Fyss14.c:4257)。
    ///
    /// 判定対象は「予約語が MCB または RMCB」かつ「ep[2] 極数の 3 桁目(epap[2])が '1'」かつ
    /// 「グループ親データ追番(goyano)が "000" でない」要素。ただし 1 相 2 線 210V(X,Y 相のみ)は除外する。
    /// 同一グループ親を持つ既出要素があればその要素の対象 MCB を差し替えてスキップし、
    /// グループ親の下流(<see cref="DownstreamSelector.SelectDownstream"/>)に同一階層で既に NT があれば
    /// これもスキップする。最終的に挿入位置(datano_NT)昇順・階層(kaisou)降順で整列して返す。
    /// </summary>
    /// <param name="records">主回路レコード列(FYRT800 配列相当)。データ追番は index+1 とみなす。</param>
    /// <returns>ＮＴ挿入情報の一覧(挿入不要なら空)。【C原典】*i_NT / *p_NT。</returns>
    public static IReadOnlyList<NtInsertion> PrepareNtInsertions(IReadOnlyList<MainCircuitResult> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var plan = new List<NtInsertion>();
        int count = records.Count;

        for (int i = 0; i < count; i++)
        {
            MainCircuitData d = records[i].Data;

            // 【C原典】予約語が MCB でも RMCB でもなければ対象外(1996.01.08 RMCB 追加)。
            if (d.ReservedWord != "MCB" && d.ReservedWord != "RMCB")
            {
                continue;
            }

            // 【C原典】ep[2].epap[2]!='1'(電気パラメータ[2]の極数 3 桁目が 1)は対象外。
            string epap = d.ElectricalParameterSlots[2].P;
            if (epap.Length < 3 || epap[2] != '1')
            {
                continue;
            }

            // 【C原典】グループ親データ追番(goyano)が "000" は対象外。
            if (d.GroupParentSequenceNumber == "000")
            {
                continue;
            }

            // 【C原典】96.04.15: 1 相 2 線 210V は X,Y 相しか取らないので NT 自動発生しない。
            if (d.CircuitPhaseCount == '1'
                && d.CircuitWireType == '2'
                && d.CircuitVoltage[0] == "210"
                && d.CircuitVoltage[1] == "000"
                && d.CircuitVoltage[2] == "000")
            {
                continue;
            }

            // 【C原典】既出要素の中に同一グループ親があれば、その要素の対象 MCB を差し替えてスキップ。
            bool found = false;
            for (int j = 0; j < plan.Count; j++)
            {
                MainCircuitData other = records[plan[j].McbSequenceNumber - 1].Data;
                if (d.GroupParentSequenceNumber == other.GroupParentSequenceNumber)
                {
                    found = true;
                    plan[j] = plan[j] with { McbSequenceNumber = i + 1 };
                }
            }

            if (found)
            {
                continue;
            }

            // 【C原典】グループ親に連なる下流要素の追番リストを得る(Fyss35_Select_Karyu_Sub)。
            int sijino = EquipmentParameterFormatter.Stoi(d.GroupParentSequenceNumber, SequenceWidth);
            IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(records, sijino);
            if (downstream is null || downstream.Count == 0)
            {
                continue;
            }

            // 【C原典】下流のうち同一階層(kaisono)である最後の要素の追番 k を得る。
            //   その過程で同一階層に NT が既に居れば exist_NT を立てる。k の初期値は i(0 始まり)。
            bool existNt = false;
            int k = i;
            foreach (int sel in downstream)
            {
                MainCircuitData cand = records[sel - 1].Data;
                if (cand.HierarchyNumber == d.HierarchyNumber && k < sel)
                {
                    if (cand.ReservedWord == "NT")
                    {
                        existNt = true;
                    }

                    k = sel;
                }
            }

            if (existNt)
            {
                continue;
            }

            // 【C原典】NTINF 設定: datano_MCB=i+1 / datano_END=k / datano_NT=下流末尾 / kaisou。
            plan.Add(new NtInsertion(
                McbSequenceNumber: i + 1,
                EndSequenceNumber: k,
                InsertAfterSequenceNumber: downstream[^1],
                Hierarchy: EquipmentParameterFormatter.Stoi(d.HierarchyNumber, SequenceWidth)));
        }

        // 【C原典】qsort(SortNTINF): datano_NT 昇順、同値なら kaisou 降順(p2->kaisou - p1->kaisou)。
        return plan
            .OrderBy(x => x.InsertAfterSequenceNumber)
            .ThenByDescending(x => x.Hierarchy)
            .ToList();
    }

    /// <summary>
    /// NTINF を元に主回路データブロックへ NT を挿入しデータ追番を再採番する。
    /// 【C原典】<c>Mainfile_NT_Make</c>(Fyss14.c:4392)。
    ///
    /// 旧主回路を新リストへ複写しつつ、挿入位置(datano_NT)の直後へ NT 要素を挿入し、
    /// データ追番・親データ追番(oyatno)・グループ親データ追番(goyano)を新採番へ付け替える。
    /// NT 要素の各フィールドは発生原因の MCB(datano_MCB)から複写する。
    /// 【C原典】と同じく旧リストの要素は再利用され、重さならないように追番が書き換わる。
    /// </summary>
    /// <param name="mains">旧主回路エリア。要素は再利用され採番等が書き換わる。【C原典】*Pmaina(件数 *Pmainc)。</param>
    /// <param name="plan"><see cref="PrepareNtInsertions"/> が返す NT 挿入情報(datano_NT 昇順)。【C原典】p_NT(件数 i_NT)。</param>
    /// <returns>NT 挿入後の新主回路エリア。【C原典】*Pmaina(件数 *Pmainc=mainc+i_NT)。</returns>
    public static IReadOnlyList<MainCircuitResult> InsertNtRecords(
        IReadOnlyList<MainCircuitResult> mains, IReadOnlyList<NtInsertion> plan)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(plan);

        var newList = new List<MainCircuitResult>(mains.Count + plan.Count);
        int j = 0;   // 処理対象 NTINF の位置。

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitResult cur = mains[i];

            // 【C原典】データ追番・親データ追番・グループ親データ追番を新採番へ付け替える。
            //   maina[i].datano を in-place で書き換えるのと同値に、cur(=mains[i])の追番を更新する。
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

            newList.Add(cur);

            // 【C原典】現データ(旧 datano=i+1)の直後に NT を挿入する必要がある間繰り返す。
            while (j < plan.Count && plan[j].InsertAfterSequenceNumber == i + 1)
            {
                // 【C原典】datano_MCB は旧配列の元 index+1。追番以外のフィールドは不変なのでそのまま参照する。
                MainCircuitData mcb = mains[plan[j].McbSequenceNumber - 1].Data;

                var nt = new MainCircuitResult();   // Main_Area_Clear 相当(既定初期値)。
                MainCircuitData d = nt.Data;

                nt.SequenceNumber = BranchArraySorter.FormatFixedWidth(newList.Count + 1, SequenceWidth);
                d.AutoGenerationKind = '1';
                d.ReservedWord = NtWord;
                d.LineTypeCode = mcb.LineTypeCode;
                d.ElectricalParameterSlots[0].Bn = mcb.ElectricalParameterSlots[0].Bn;
                d.CircuitElement = mcb.CircuitElement;
                d.ElectricalParameterSlots[0].Qty = '1';
                d.SystemNumber = mcb.SystemNumber;
                d.SystemKind = mcb.SystemKind;
                d.CircuitClass = mcb.CircuitClass;
                d.CircuitNumberSuffix = mcb.CircuitNumberSuffix;
                d.IncomingNumber = mcb.IncomingNumber;
                d.HierarchyNumber = mcb.HierarchyNumber;
                d.SortKind = '4';

                newList.Add(nt);
                j++;
            }
        }

        return newList;
    }
}

/// <summary>
/// ＮＴ自動生成の 1 挿入分の情報。【C原典】struct NTINF(Fyss14.c:115)。
/// </summary>
/// <param name="McbSequenceNumber">発生原因となる MCB のデータ追番(1 始まり)。【C原典】datano_MCB。</param>
/// <param name="EndSequenceNumber">上記 MCB と同一階層の最大並列追番を持つ要素の追番。【C原典】datano_END。</param>
/// <param name="InsertAfterSequenceNumber">ＮＴを挿入するべき要素の直前のデータ追番。【C原典】datano_NT。</param>
/// <param name="Hierarchy">ＮＴを挿入するべき要素の階層。【C原典】kaisou。</param>
public sealed record NtInsertion(
    int McbSequenceNumber,
    int EndSequenceNumber,
    int InsertAfterSequenceNumber,
    int Hierarchy);
