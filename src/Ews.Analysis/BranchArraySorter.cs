using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 分岐配列指定無し時の並べ替え。【C原典】<c>Fyss3C_Bunki_Sort</c>(toku/sekkei/src/Fyss3C.c)。
///
/// 物件の分岐配列指定有無区分が '2'(自動配列)の時、末端回路ブレーカ群を
/// 予約語種別・電圧・フレーム/トリップ電流・タイプ等の複合ソートキーで並べ替え、
/// 上流並列追番(joheino)・並列追番(heino)・グループ並列追番(glheino)を再設定する。
///
/// 大規模関数のため段階移植する。本ファイルは基盤となるリーフヘルパー
/// (<see cref="GetReservedWordKind"/>=GetYNUM / <see cref="SetDecimalPoint"/>=SetPoint /
/// <see cref="FormatFixedWidth"/>=itoanz)を保持する。固定長 atoi(antoi)は
/// <see cref="EquipmentParameterFormatter.Stoi"/> を再利用する。
/// </summary>
public static class BranchArraySorter
{
    /// <summary>
    /// 予約語識別子。【C原典】<c>enum YNUM</c>(Fyss3C.c)。宣言順を厳守する
    /// (機器構成の電気パラメータ取り出し <c>KouseiGetElement</c> が switch で使用)。
    /// </summary>
    public enum ReservedWordKind
    {
        P, MCB, ELB, MMCB, ELMB,
        SB, RMCB, RELB, RMMCB, RELMB,
        MC, THR, MG, SC, NT,
        WH, VM, AM, VT, CT,
        VS, AS, TB, CON, TR,
        ZCT, LGR, ELR, HPSB, HSB,
        RRY, RTR, MCDT, F, LA,
        DCPW, CR, TM, TS, G,
        G1, G2, G3, G4, GI,
        GP, GPN, WL, GL, RL,
        OL, BL, COS, PBS, SSW,
        TSW, BZ, BEL, CP, RSW,
        EE, HM, ERY2, ERY3, ERY4,
        CKS, CSDT, CU, TU, NHMB,
        APN, SL23, SL32, SL42, SL43,
        LGT, BLTR, PLTR, FL, LSW,
        DSW, SV, MV, KPRY, THSW,
        L, IDF, HDF, MDF, TV,
        WDP, MCFR, MGFR, MCSD, MGSD,
        MGLD, MGCS, INV, FLT, DCSIR,
        DCNI, MCFRSD, MGFRSD, STM, SIR,
        C, R, D, NICA, RE,
        VVVF, None,
    }

    /// <summary>予約語(8 バイト右詰め)と識別子の対応。【C原典】<c>GetYNUM</c> の <c>y_data[]</c>。</summary>
    private static readonly (string Word, ReservedWordKind Kind)[] ReservedWordTable =
    [
        ("P       ", ReservedWordKind.P), ("MCB     ", ReservedWordKind.MCB), ("ELB     ", ReservedWordKind.ELB),
        ("MMCB    ", ReservedWordKind.MMCB), ("ELMB    ", ReservedWordKind.ELMB), ("SB      ", ReservedWordKind.SB),
        ("RMCB    ", ReservedWordKind.RMCB), ("RELB    ", ReservedWordKind.RELB), ("RMMCB   ", ReservedWordKind.RMMCB),
        ("RELMB   ", ReservedWordKind.RELMB), ("MC      ", ReservedWordKind.MC), ("THR     ", ReservedWordKind.THR),
        ("MG      ", ReservedWordKind.MG), ("SC      ", ReservedWordKind.SC), ("NT      ", ReservedWordKind.NT),
        ("WH      ", ReservedWordKind.WH), ("VM      ", ReservedWordKind.VM), ("AM      ", ReservedWordKind.AM),
        ("VT      ", ReservedWordKind.VT), ("CT      ", ReservedWordKind.CT), ("VS      ", ReservedWordKind.VS),
        ("AS      ", ReservedWordKind.AS), ("TB      ", ReservedWordKind.TB), ("CON     ", ReservedWordKind.CON),
        ("TR      ", ReservedWordKind.TR), ("ZCT     ", ReservedWordKind.ZCT), ("LGR     ", ReservedWordKind.LGR),
        ("ELR     ", ReservedWordKind.ELR), ("HPSB    ", ReservedWordKind.HPSB), ("HSB     ", ReservedWordKind.HSB),
        ("RRY     ", ReservedWordKind.RRY), ("RTR     ", ReservedWordKind.RTR), ("MCDT    ", ReservedWordKind.MCDT),
        ("F       ", ReservedWordKind.F), ("LA      ", ReservedWordKind.LA), ("DCPW    ", ReservedWordKind.DCPW),
        ("CR      ", ReservedWordKind.CR), ("TM      ", ReservedWordKind.TM), ("TS      ", ReservedWordKind.TS),
        ("G       ", ReservedWordKind.G), ("G1      ", ReservedWordKind.G1), ("G2      ", ReservedWordKind.G2),
        ("G3      ", ReservedWordKind.G3), ("G4      ", ReservedWordKind.G4), ("GI      ", ReservedWordKind.GI),
        ("GP      ", ReservedWordKind.GP), ("GPN     ", ReservedWordKind.GPN), ("WL      ", ReservedWordKind.WL),
        ("GL      ", ReservedWordKind.GL), ("RL      ", ReservedWordKind.RL), ("OL      ", ReservedWordKind.OL),
        ("BL      ", ReservedWordKind.BL), ("COS     ", ReservedWordKind.COS), ("PBS     ", ReservedWordKind.PBS),
        ("SSW     ", ReservedWordKind.SSW), ("TSW     ", ReservedWordKind.TSW), ("BZ      ", ReservedWordKind.BZ),
        ("BEL     ", ReservedWordKind.BEL), ("CP      ", ReservedWordKind.CP), ("RSW     ", ReservedWordKind.RSW),
        ("EE      ", ReservedWordKind.EE), ("HM      ", ReservedWordKind.HM),
        ("2ERY    ", ReservedWordKind.ERY2), ("3ERY    ", ReservedWordKind.ERY3), ("4ERY    ", ReservedWordKind.ERY4),
        ("CKS     ", ReservedWordKind.CKS), ("CSDT    ", ReservedWordKind.CSDT), ("CU      ", ReservedWordKind.CU),
        ("TU      ", ReservedWordKind.TU), ("NHMB    ", ReservedWordKind.NHMB), ("APN     ", ReservedWordKind.APN),
        ("SL23    ", ReservedWordKind.SL23), ("SL32    ", ReservedWordKind.SL32), ("SL42    ", ReservedWordKind.SL42),
        ("SL43    ", ReservedWordKind.SL43), ("LGT     ", ReservedWordKind.LGT), ("BLTR    ", ReservedWordKind.BLTR),
        ("PLTR    ", ReservedWordKind.PLTR), ("FL      ", ReservedWordKind.FL), ("LSW     ", ReservedWordKind.LSW),
        ("DSW     ", ReservedWordKind.DSW), ("SV      ", ReservedWordKind.SV), ("MV      ", ReservedWordKind.MV),
        ("KPRY    ", ReservedWordKind.KPRY), ("THSW    ", ReservedWordKind.THSW), ("L       ", ReservedWordKind.L),
        ("IDF     ", ReservedWordKind.IDF), ("HDF     ", ReservedWordKind.HDF), ("MDF     ", ReservedWordKind.MDF),
        ("TV      ", ReservedWordKind.TV), ("WDP     ", ReservedWordKind.WDP), ("MCFR    ", ReservedWordKind.MCFR),
        ("MGFR    ", ReservedWordKind.MGFR), ("MCSD    ", ReservedWordKind.MCSD), ("MGSD    ", ReservedWordKind.MGSD),
        ("MGLD    ", ReservedWordKind.MGLD), ("MGCS    ", ReservedWordKind.MGCS), ("INV     ", ReservedWordKind.INV),
        ("FLT     ", ReservedWordKind.FLT), ("DCSIR   ", ReservedWordKind.DCSIR), ("DCNI    ", ReservedWordKind.DCNI),
        ("MCFRSD  ", ReservedWordKind.MCFRSD), ("MGFRSD  ", ReservedWordKind.MGFRSD), ("STM     ", ReservedWordKind.STM),
        ("SIR     ", ReservedWordKind.SIR), ("C       ", ReservedWordKind.C), ("R       ", ReservedWordKind.R),
        ("D       ", ReservedWordKind.D), ("NICA    ", ReservedWordKind.NICA), ("RE      ", ReservedWordKind.RE),
        ("VVVF    ", ReservedWordKind.VVVF),
    ];

    /// <summary>
    /// 指定予約語に対応した予約語識別子を返す。関係ない語句は <see cref="ReservedWordKind.None"/>。
    /// 【C原典】<c>GetYNUM(char* yoyaku)</c>。先頭 8 バイトを完全一致比較する(memcmp 8)。
    /// </summary>
    public static ReservedWordKind GetReservedWordKind(string reservedWord)
    {
        string key = (reservedWord ?? string.Empty).PadRight(8)[..8];
        foreach ((string word, ReservedWordKind kind) in ReservedWordTable)
        {
            if (word == key)
            {
                return kind;
            }
        }

        return ReservedWordKind.None;
    }

    /// <summary>
    /// 数値文字列 <paramref name="value"/> の指定位置に小数点を打ち込む。
    /// 【C原典】<c>SetPoint(char* p,int n)</c>。呼元は <c>KouseiGetElement</c> のみ。
    ///
    /// 忠実再現: <c>n&lt;=0</c> は無変更(C原典は未初期化ローカルへ strcat して return する no-op)。
    /// <c>n&gt;=長さ</c> は先頭に "0." を付す。それ以外は末尾 <paramref name="n"/> 桁の直前に '.' を挿入する。
    /// </summary>
    public static string SetDecimalPoint(string value, int n)
    {
        string p = value ?? string.Empty;

        if (n <= 0)
        {
            return p;
        }

        if (n >= p.Length)
        {
            return "0." + p;
        }

        int cut = p.Length - n;
        return string.Concat(p.AsSpan(0, cut), ".", p.AsSpan(cut));
    }

    /// <summary>
    /// 整数値を <paramref name="width"/> 桁の 0 詰め文字列にする(超過時は先頭 <paramref name="width"/> 文字)。
    /// 【C原典】<c>itoanz(char* s,int n,int d)</c>(<c>%.nd</c> で 0 詰め後 <c>memcpy(s,buf,n)</c>)。
    /// </summary>
    public static string FormatFixedWidth(int value, int width)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        string s = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        bool negative = s.StartsWith('-');
        string digits = negative ? s[1..] : s;
        string zeroPadded = negative ? "-" + digits.PadLeft(width - 1, '0') : digits.PadLeft(width, '0');
        return zeroPadded[..width];
    }

    // =====================================================================
    // 作業モデルと収集関数群。【C原典】Fyss3C.c の SDATA/TREE/Stat と
    // InitializeWorkArea / SetResults / IsMatch_* / GetFloor* / GetBrothers /
    // GetMinimumKaisono / GetMaximumKaisono。並べ替え本体は後段で移植する。
    // =====================================================================

    /// <summary>処理ステータス。【C原典】<c>enum Stat</c>(Fyss3C.c)。</summary>
    public enum WorkStatus
    {
        NoDone, Doing, Sorting, SortDone, CDoing, CSorting, CSortDone, Done,
    }

    /// <summary>並べ替え作業用の要素データ(数値化)。【C原典】<c>struct TREE</c>(Fyss3C.c)。</summary>
    public sealed class TreeData
    {
        /// <summary>入線番号。【C原典】nyuseno。</summary>
        public int EntryLineNumber { get; set; }

        /// <summary>上流並列追番。【C原典】joheino。</summary>
        public int UpperParallelNumber { get; set; }

        /// <summary>階層番号。【C原典】kaisono。</summary>
        public int HierarchyNumber { get; set; }

        /// <summary>並列追番。【C原典】heino。</summary>
        public int ParallelNumber { get; set; }

        /// <summary>直列追番。【C原典】chokuno。</summary>
        public int SeriesNumber { get; set; }

        /// <summary>行種グループ番号。【C原典】gyoglno。</summary>
        public int LineTypeGroupNumber { get; set; }

        /// <summary>親データ追番。【C原典】oyatno。</summary>
        public int ParentSequenceNumber { get; set; }

        /// <summary>グループ親データ追番。【C原典】goyano。</summary>
        public int GroupParentNumber { get; set; }

        /// <summary>回路要素(数値化 = kiryoso - '0')。【C原典】kiryoso。</summary>
        public int CircuitElement { get; set; }

        /// <summary>グループ並列追番。【C原典】glheino。</summary>
        public int GroupParallelNumber { get; set; }

        /// <summary>浅いコピーを返す。【C原典】memcpy(&sd.new,&sd.now,sizeof(TREE))。</summary>
        public TreeData Clone() => (TreeData)MemberwiseClone();
    }

    /// <summary>並べ替え処理データ。【C原典】<c>struct SDATA</c>(Fyss3C.c)。</summary>
    public sealed class WorkData
    {
        /// <summary>処理前データ。【C原典】TREE now。</summary>
        public TreeData Now { get; set; } = new();

        /// <summary>処理後データ。【C原典】TREE new。</summary>
        public TreeData New { get; set; } = new();

        /// <summary>処理ステータス。【C原典】Stat stat。</summary>
        public WorkStatus Stat { get; set; } = WorkStatus.NoDone;
    }

    /// <summary>
    /// 作業領域の初期設定。主回路データを数値変換して作業テーブルへ移送し、ステータスを nodone にする。
    /// 【C原典】<c>InitializeWorkArea</c>(Fyss3C.c)。
    /// </summary>
    public static WorkData[] InitializeWorkArea(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        var sd = new WorkData[mains.Count];
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            var now = new TreeData
            {
                EntryLineNumber = EquipmentParameterFormatter.Stoi(d.IncomingNumber, 3),
                UpperParallelNumber = EquipmentParameterFormatter.Stoi(d.UpperParallelNumber, 3),
                HierarchyNumber = EquipmentParameterFormatter.Stoi(d.HierarchyNumber, 3),
                ParallelNumber = EquipmentParameterFormatter.Stoi(d.ParallelNumber, 3),
                SeriesNumber = EquipmentParameterFormatter.Stoi(d.SeriesNumber, 3),
                LineTypeGroupNumber = EquipmentParameterFormatter.Stoi(d.LineTypeGroupNumber, 3),
                ParentSequenceNumber = EquipmentParameterFormatter.Stoi(d.ParentSequenceNumber, 3),
                GroupParentNumber = EquipmentParameterFormatter.Stoi(d.GroupParentSequenceNumber, 3),
                CircuitElement = d.CircuitElement - '0',
                GroupParallelNumber = EquipmentParameterFormatter.Stoi(d.GroupParallelNumber, 3),
            };
            sd[i] = new WorkData
            {
                Now = now,
                New = now.Clone(),
                Stat = WorkStatus.NoDone,
            };
        }

        return sd;
    }

    /// <summary>
    /// 処理結果の移送。処理完了(≠nodone)した要素の上流並列追番・並列追番・グループ並列追番を
    /// 主回路データへ書き戻す。【C原典】<c>SetResults</c>(Fyss3C.c)。
    /// </summary>
    public static void SetResults(IReadOnlyList<MainCircuitResult> mains, WorkData[] sd)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(sd);

        for (int i = 0; i < mains.Count; i++)
        {
            if (sd[i].Stat == WorkStatus.NoDone)
            {
                continue;
            }

            MainCircuitData d = mains[i].Data;
            d.UpperParallelNumber = FormatFixedWidth(sd[i].New.UpperParallelNumber, 3);
            d.ParallelNumber = FormatFixedWidth(sd[i].New.ParallelNumber, 3);
            d.GroupParallelNumber = FormatFixedWidth(sd[i].New.GroupParallelNumber, 3);
        }
    }

    /// <summary>
    /// 行種コード(gyocd)が 'B'/'BO'/'O' のいずれかかを調べる。【C原典】<c>IsMatch_gyocd</c>(Fyss3C.c)。
    /// </summary>
    public static bool IsMatchLineTypeCode(MainCircuitResult record)
    {
        ArgumentNullException.ThrowIfNull(record);
        string g = (record.Data.LineTypeCode ?? string.Empty).PadRight(3)[..3];
        return g is "B  " or "BO " or "O  ";
    }

    /// <summary>
    /// 電気パラメータ[0]の盤種類(epabn)が '1'/'4' かを調べる。【C原典】<c>IsMatch_epabn</c>(Fyss3C.c)。
    /// </summary>
    public static bool IsMatchPanelKind(MainCircuitResult record)
    {
        ArgumentNullException.ThrowIfNull(record);
        char bn = record.Data.ElectricalParameterSlots[0].Bn;
        return bn is '1' or '4';
    }

    /// <summary>
    /// 処理対象(doing)かつ指定階層番号の要素インデックス一覧を得る。【C原典】<c>GetFloorElements</c>(Fyss3C.c)。
    /// </summary>
    public static List<int> GetFloorElements(WorkData[] sd, int hierarchyNumber)
    {
        ArgumentNullException.ThrowIfNull(sd);
        var list = new List<int>();
        for (int i = 0; i < sd.Length; i++)
        {
            if (sd[i].Stat == WorkStatus.Doing && sd[i].Now.HierarchyNumber == hierarchyNumber)
            {
                list.Add(i);
            }
        }

        return list;
    }

    /// <summary>
    /// 処理対象(doing)かつ指定階層番号・直列追番==1 の要素インデックス一覧を得る。
    /// 【C原典】<c>GetFloorTopElements</c>(Fyss3C.c)。
    /// </summary>
    public static List<int> GetFloorTopElements(WorkData[] sd, int hierarchyNumber)
    {
        ArgumentNullException.ThrowIfNull(sd);
        var list = new List<int>();
        for (int i = 0; i < sd.Length; i++)
        {
            if (sd[i].Stat == WorkStatus.Doing &&
                sd[i].Now.HierarchyNumber == hierarchyNumber &&
                sd[i].Now.SeriesNumber == 1)
            {
                list.Add(i);
            }
        }

        return list;
    }

    /// <summary>
    /// 指定要素に直列に連なる要素群(上流並列追番・階層番号・並列追番が等しく直列追番≠1)を得る。
    /// base の直後から連続する範囲で、条件を満たさなくなった時点で打ち切る。
    /// 【C原典】<c>GetFloorElementsOfSirial</c>(Fyss3C.c)。
    /// </summary>
    public static List<int> GetFloorElementsOfSeries(WorkData[] sd, int baseIndex)
    {
        ArgumentNullException.ThrowIfNull(sd);
        var list = new List<int>();
        WorkData b = sd[baseIndex];
        for (int i = baseIndex + 1; i < sd.Length; i++)
        {
            if (sd[i].Stat != WorkStatus.Doing ||
                sd[i].Now.UpperParallelNumber != b.Now.UpperParallelNumber ||
                sd[i].Now.HierarchyNumber != b.Now.HierarchyNumber ||
                sd[i].Now.ParallelNumber != b.Now.ParallelNumber ||
                sd[i].Now.SeriesNumber == 1)
            {
                break;
            }

            list.Add(i);
        }

        return list;
    }

    /// <summary>
    /// 処理対象(doing)かつ指定階層番号・回路要素==4(VT)の要素インデックス一覧を得る。
    /// 【C原典】<c>GetFloorElementsOfVT</c>(Fyss3C.c)。
    /// </summary>
    public static List<int> GetFloorElementsOfVt(WorkData[] sd, int hierarchyNumber)
    {
        ArgumentNullException.ThrowIfNull(sd);
        var list = new List<int>();
        for (int i = 0; i < sd.Length; i++)
        {
            if (sd[i].Stat == WorkStatus.Doing &&
                sd[i].Now.HierarchyNumber == hierarchyNumber &&
                sd[i].Now.CircuitElement == 4)
            {
                list.Add(i);
            }
        }

        return list;
    }

    /// <summary>
    /// 指定要素と同じ階層番号・行種グループ番号を持つ直列追番==1 の計器回路(CT)一覧を得る。
    /// 【C原典】<c>GetFloorElementsOfCT</c>(Fyss3C.c)。
    /// </summary>
    public static List<int> GetFloorElementsOfCt(WorkData[] sd, int baseIndex)
    {
        ArgumentNullException.ThrowIfNull(sd);
        var list = new List<int>();
        WorkData b = sd[baseIndex];
        for (int i = 0; i < sd.Length; i++)
        {
            if (sd[i].Stat == WorkStatus.Doing &&
                sd[i].Now.HierarchyNumber == b.Now.HierarchyNumber &&
                sd[i].Now.LineTypeGroupNumber == b.Now.LineTypeGroupNumber &&
                sd[i].Now.SeriesNumber == 1)
            {
                list.Add(i);
            }
        }

        return list;
    }

    /// <summary>
    /// 指定要素と同じ親データ追番を持つ要素インデックス一覧を得る(ステータス無関係)。
    /// 【C原典】<c>GetBrothers</c>(Fyss3C.c)。
    /// </summary>
    public static List<int> GetBrothers(WorkData[] sd, int index)
    {
        ArgumentNullException.ThrowIfNull(sd);
        var list = new List<int>();
        for (int i = 0; i < sd.Length; i++)
        {
            if (sd[i].Now.ParentSequenceNumber == sd[index].Now.ParentSequenceNumber)
            {
                list.Add(i);
            }
        }

        return list;
    }

    /// <summary>
    /// 処理対象(doing)データの最小階層番号を得る(該当なしは 0x7FFF)。【C原典】<c>GetMinimumKaisono</c>(Fyss3C.c)。
    /// </summary>
    public static int GetMinimumHierarchyNumber(WorkData[] sd)
    {
        ArgumentNullException.ThrowIfNull(sd);
        int r = 0x7FFF;
        foreach (WorkData w in sd)
        {
            if (w.Stat != WorkStatus.Doing)
            {
                continue;
            }

            if (r > w.Now.HierarchyNumber)
            {
                r = w.Now.HierarchyNumber;
            }
        }

        return r;
    }

    /// <summary>
    /// 処理対象(doing)データの最大階層番号を得る(該当なしは -1)。【C原典】<c>GetMaximumKaisono</c>(Fyss3C.c)。
    /// </summary>
    public static int GetMaximumHierarchyNumber(WorkData[] sd)
    {
        ArgumentNullException.ThrowIfNull(sd);
        int r = -1;
        foreach (WorkData w in sd)
        {
            if (w.Stat != WorkStatus.Doing)
            {
                continue;
            }

            if (r < w.Now.HierarchyNumber)
            {
                r = w.Now.HierarchyNumber;
            }
        }

        return r;
    }
}
