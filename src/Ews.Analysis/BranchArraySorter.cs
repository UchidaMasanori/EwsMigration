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
}
