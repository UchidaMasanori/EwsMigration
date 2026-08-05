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
