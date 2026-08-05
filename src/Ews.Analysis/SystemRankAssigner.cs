using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 主回路系統内の各要素へ座標(ランク)番号を付与する。
/// 【C原典】Main_Rank_Set(toku/sekkei/src/Fyss14.c:3733)。
///
/// 系統(ksyubetu=='1')の各要素に対し、入線番号 / 生成回路番号 / 直列追番 /
/// 並列追番 / 上流並列追番 / 親データ追番 / グループ親データ追番 / グループ並列追番
/// を割り付ける。系統外(ksyubetu!='1')は既定値(SEP は上流並列追番のみ 001)へ整える。
/// 冒頭で生成回路番号採番器(<see cref="CircuitNumberGenerator"/>)を生成し直すことで、
/// C 原典の静的グローバル <c>Kbangoua</c>/<c>PM_flg</c> の memset クリア相当とする。
/// </summary>
public static class SystemRankAssigner
{
    /// <summary>
    /// 主回路データ列へ座標番号を付与する(in-place)。
    /// 【C原典】Main_Rank_Set(Fyss14.c:3733)。
    /// </summary>
    public static void Assign(IReadOnlyList<MainCircuitResult> mains)
    {
        var generator = new CircuitNumberGenerator();   // C: memset(&Kbangoua, ...)
        var rank = new short[16];                        // ランク設定用ワーク
        short nsenNo = 0;                                // 入線番号
        short gyoglno = 0;                               // 行種グループ番号
        char sou = '0';                                  // 回路相(直前 P 行の相を保持)

        for (int i = 0; i < mains.Count; i++)
        {
            var d = mains[i].Data;

            // 行種グループ番号のセット
            if (d.SortKind == '3' || d.SortKind == '4') gyoglno++;
            d.LineTypeGroupNumber = Fmt3(gyoglno);

            // 積算ワークエリア初期化
            var wk = mains[i].Work;
            wk.EquipmentSelectionKind = ' ';
            wk.StartCircuitKind = ' ';
            wk.LeadingEquipmentFlag = ' ';
            wk.SelectionInstructionFlag = ' ';
            wk.ScProcessedFlag = ' ';
            wk.SetCurrent = 0.0;
            wk.RatedCapacity = 0.0;
            for (int j = 0; j < 6; j++)
            {
                var a = wk.AccumulationSlots[j];
                a.A = 0.0; a.B = 0.0; a.C = 0.0; a.D = 0.0; a.E = 0.0; a.M = 0.0; a.S = 0.0;
            }

            if (d.SystemKind != '1')
            {
                if (d.ReservedWord == "SEP")
                {
                    d.HierarchyNumber = "000";
                    d.UpperParallelNumber = "001";
                    d.ParallelNumber = "000";
                    d.SeriesNumber = "000";
                    d.ParentSequenceNumber = "000";
                    d.GroupParentSequenceNumber = "000";
                    d.SystemNumber = "000";
                    d.SystemKind = '0';
                }
                else
                {
                    d.IncomingNumber = "000";
                    d.SeriesNumber = "000";
                    d.ParallelNumber = "000";
                    d.UpperParallelNumber = "000";
                    d.ParentSequenceNumber = "000";
                    d.GroupParentSequenceNumber = "000";
                }
                continue;
            }

            // ---- ksyubetu == '1' (P 系統) ----

            // 入線番号(P 系統の連番)
            string gyocd3 = (d.LineTypeCode ?? string.Empty).PadRight(3)[..3];
            if (gyocd3 == "P  ")
            {
                sou = d.CircuitPhaseCount;
                nsenNo++;
            }
            d.IncomingNumber = Fmt3(nsenNo);

            // 生成回路番号
            short kairono = generator.Find(d, sou);
            d.CircuitNumber = Fmt3(kairono);

            if (d.ReservedWord == "P")
            {
                for (int j = 0; j < 16; j++) rank[j] = 0;
                d.HierarchyNumber = "000";
                d.UpperParallelNumber = "000";
                d.ParallelNumber = "000";
                d.SeriesNumber = "000";
                d.ParentSequenceNumber = "000";
                d.GroupParentSequenceNumber = "000";
            }
            else if (d.ReservedWord == "SEP")
            {
                d.HierarchyNumber = "000";
                d.UpperParallelNumber = "001";
                d.ParallelNumber = "000";
                d.SeriesNumber = "000";
                d.ParentSequenceNumber = "000";
                d.GroupParentSequenceNumber = "000";
                d.IncomingNumber = Fmt3(nsenNo);
                d.SystemKind = '1';
            }
            else
            {
                var prev = mains[i - 1].Data;
                bool inst = d.SortKind == '2' || d.SortKind == '4';   // 機器選定指示フラグ

                // 直列追番
                string chokuno;
                if (inst)
                {
                    chokuno = "001";
                }
                else if (prev.ReservedWord == "P")
                {
                    chokuno = "001";
                }
                else if (d.HierarchyNumber == prev.HierarchyNumber)   // 941205 同一階層
                {
                    chokuno = Fmt3(P3(prev.SeriesNumber) + 1);
                }
                else
                {
                    chokuno = "001";
                }
                d.SeriesNumber = chokuno;

                // 並列追番
                string heino;
                if (inst)
                {
                    int no = P3(d.HierarchyNumber);
                    rank[no - 1]++;
                    heino = Fmt3(rank[no - 1]);
                }
                else if (prev.ReservedWord == "P")
                {
                    heino = "001";
                }
                else if (d.HierarchyNumber == prev.HierarchyNumber)   // 941205 同一階層
                {
                    heino = prev.ParallelNumber;
                }
                else
                {
                    int no = P3(d.HierarchyNumber);
                    rank[no - 1]++;
                    heino = Fmt3(rank[no - 1]);
                }
                d.ParallelNumber = heino;

                // 上流並列追番
                string joheino = "000";
                if (inst)
                {
                    int no = P3(d.HierarchyNumber) - 1;   // ひとつ前のランク
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var m = mains[j].Data;
                        int kai = P3(m.HierarchyNumber);
                        if (no == kai) { joheino = m.ParallelNumber; break; }
                        if (m.ReservedWord == "P") { joheino = "000"; break; }
                    }
                }
                else
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var m = mains[j].Data;
                        if (m.ReservedWord == "P") { joheino = "000"; break; }
                        if (m.SortKind == '2' || m.SortKind == '4')
                        {
                            joheino = d.ReservedWord == "RRY"     // 改訂<15>
                                ? m.ParallelNumber
                                : m.UpperParallelNumber;
                            break;
                        }
                    }
                }
                d.UpperParallelNumber = joheino;

                // 親データ追番
                string oyatno;
                if (inst)
                {
                    int no = P3(d.HierarchyNumber) - 1;
                    oyatno = "000";
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var m = mains[j];
                        int kai = P3(m.Data.HierarchyNumber);
                        if (no == kai) { oyatno = m.SequenceNumber; break; }
                        if (m.Data.ReservedWord == "P") { oyatno = m.SequenceNumber; break; }  // 96.03.07
                    }
                }
                else
                {
                    oyatno = mains[i - 1].SequenceNumber;
                }
                d.ParentSequenceNumber = oyatno;

                // グループ親データ追番
                {
                    int no = P3(d.HierarchyNumber) - 1;
                    string goyano = "000";
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var m = mains[j];
                        int kai = P3(m.Data.HierarchyNumber);
                        if (no == kai) { goyano = m.SequenceNumber; break; }
                        if (m.Data.ReservedWord == "P") { goyano = "000"; break; }
                    }
                    d.GroupParentSequenceNumber = goyano;
                }

                // グループ並列追番
                string glheino;
                if (inst)
                {
                    int no = P3(d.GroupParentSequenceNumber);
                    short ghno = 1;
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var m = mains[j].Data;
                        if (m.ReservedWord == "P") break;
                        int goya = P3(m.GroupParentSequenceNumber);
                        if (no == goya && (m.SortKind == '2' || m.SortKind == '4')) ghno++;
                    }
                    glheino = Fmt3(ghno);
                }
                else
                {
                    glheino = "000";
                }
                d.GroupParallelNumber = glheino;
            }
        }
    }

    /// <summary>SHORT を 3 桁ゼロ詰めへ整形(4 桁以上は先頭 3 桁 = C の memcpy(...,3) 相当)。</summary>
    private static string Fmt3(int v)
    {
        string s = v.ToString("000");
        return s.Length > 3 ? s[..3] : s;
    }

    /// <summary>3 桁数値文字列を short 相当へ解釈(sscanf %hd 相当)。</summary>
    private static int P3(string? s)
        => int.TryParse((s ?? string.Empty).Trim(), out int v) ? v : 0;
}
