namespace Ews.Analysis;

using System.Globalization;
using System.Text.RegularExpressions;
using Ews.Domain.Analysis;
using Ews.Domain.Common;

/// <summary>
/// 電気パラメータ整形エンジン(型式展開)。
///
/// 【C原典】toku/sekkei/src/Fyss1f.c
///   - 入口  : <c>eparm_set(union fyrt811* u, struct eparmg* ep, CHAR* yoyaku)</c>(Fyss1f.c:2208)
///             … 検証済みの定格値(<see cref="RatingValues"/> / union fyrt811 / key_tbl)を予約語別に
///               固定長数値文字列へ整形して <see cref="ElectricalParameters"/>(struct eparmg)へ格納する。
///   - 補助  : <c>set_9(CHAR* from, INT from_length, CHAR* to, INT to_length, CHAR* format, DOUBLE multiple)</c>
///             (Fyss1f.c:3230) … <c>from</c> 先頭 <c>from_length</c> 文字を atof×multiple し、C の
///             sprintf 書式(例 "%09.3f")で整形して <c>to_length</c> 桁ぶんへ格納(strncpy、末尾 NUL 埋めなし)。
///   - 補助  : <c>chk_9(CHAR* from, INT from_length)</c>(Fyss1f.c:3253) … from 先頭 from_length 文字の atof。
///   - 補助  : <c>Stof(CHAR* str, SHORT size)</c>(Fysk09.c:10) … str 先頭 size 文字の atof。
///
/// <see cref="ElectricalParameterChecker"/>(key_check)が populate した <see cref="RatingValues"/> を入力に取り、
/// eparm_set は各予約語ぶんの set_9 呼び出しを忠実に再現する(key_check の逆写像)。
/// 未設定フィールドは C では union の 0 埋めにより atof=0 となるため、本移植でも
/// <see cref="RatingValues.Get"/> が null のフィールドは "" 扱い(atof=0)とする。
///
/// 本フェーズで eparm_set の全予約語(約99分岐、Fyss1f.c:2219~3218)を収録する。
/// 遮断器/漏電遮断器系、引込 PS/P/UP、電磁接触器系、端子台・計器系、TB/CON/TR(多スロット変圧器)、
/// 表示灯 WL/GL/RL/OL/FL/BL、地絡・漏電リレー ZCT/LGR/ELR、タイマ TM/TS、単位スイッチ TSU/SSWU/... 等。
/// C 原典で空分岐(STM/SIR/C/R/D/NICA/RE/VVVF/TV)や未該当予約語は '0' 埋めのまま返す。
/// </summary>
public sealed class EquipmentParameterFormatter
{
    /// <summary>
    /// 定格値を予約語別に整形して電気パラメータを生成する。【C原典】eparm_set(Fyss1f.c:2208)。
    /// 未収録(未移植)の予約語では既定値('0' 埋め)の <see cref="ElectricalParameters"/> をそのまま返す
    /// (C では該当 if/else 分岐が無く ep が Main_Area_Clear の '0' のままになるのと同じ)。
    /// </summary>
    /// <param name="values">検証済み定格値(【C原典】union fyrt811 u)。</param>
    /// <param name="reservedWord">予約語(【C原典】yoyaku、8桁固定・右空白)。</param>
    /// <returns>整形済み電気パラメータ(【C原典】struct eparmg ep)。</returns>
    public ElectricalParameters EparmSet(RatingValues values, string reservedWord)
    {
        ArgumentNullException.ThrowIfNull(values);
        ElectricalParameters ep = new();
        string yoyaku = reservedWord ?? string.Empty;

        // memcmp 部分一致の予約語群(先頭/接尾一致)を先に処理する。
        // XERY = 2ERY/3ERY/4ERY(【C原典】memcmp("ERY",&yoyaku[1],7))
        if (yoyaku.Length == 4 && yoyaku.EndsWith("ERY", StringComparison.Ordinal))
        {
            SetXery(values, ep);
            return ep;
        }
        // FLTx(【C原典】memcmp(yoyaku,"FLT",3))
        if (yoyaku.StartsWith("FLT", StringComparison.Ordinal))
        {
            SetFltx(values, ep);
            return ep;
        }

        switch (yoyaku)
        {
            case "MCB":
                SetBreaker(values, ep, afLength: 4, atLength: 4, atStofSpecial: true, hasMa: false, hasKw: false, eZeroToNine: true);
                break;
            case "ELB":
                SetBreaker(values, ep, afLength: 4, atLength: 4, atStofSpecial: true, hasMa: true, hasKw: false, eZeroToNine: true);
                break;
            case "MMCB":
                SetBreaker(values, ep, afLength: 3, atLength: 6, atStofSpecial: false, hasMa: false, hasKw: true, eZeroToNine: false);
                break;
            case "ELMB":
                SetBreaker(values, ep, afLength: 3, atLength: 6, atStofSpecial: false, hasMa: true, hasKw: true, eZeroToNine: false);
                break;
            case "SB":
                SetBreaker(values, ep, afLength: 2, atLength: 2, atStofSpecial: false, hasMa: false, hasKw: false, eZeroToNine: true);
                break;
            case "RMCB":
                SetBreaker(values, ep, afLength: 2, atLength: 2, atStofSpecial: false, hasMa: false, hasKw: false, eZeroToNine: false, hasVc: true);
                break;
            case "RELB":
                SetBreaker(values, ep, afLength: 2, atLength: 2, atStofSpecial: false, hasMa: true, hasKw: false, eZeroToNine: false, hasVc: true);
                break;
            case "RMMCB":
                SetBreaker(values, ep, afLength: 2, atLength: 5, atStofSpecial: false, hasMa: false, hasKw: true, eZeroToNine: false, hasVc: true);
                break;
            case "RELMB":
                SetBreaker(values, ep, afLength: 2, atLength: 5, atStofSpecial: false, hasMa: true, hasKw: true, eZeroToNine: false, hasVc: true);
                break;
            case "PS":
                SetIncoming(values, ep, withCableSize: false);
                break;
            case "P":
                SetIncoming(values, ep, withCableSize: true);
                break;
            case "UP":
                // 【C原典】eparm_set UP(Fyss1f.c:2253): 定格電圧2 のみ。
                ep.V2Kbn = FvKbn(values.Get("fv"));
                ep.V2[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);
                break;
            case "MC":
                SetContactor(values, ep, hasE: false, hasAt: false);
                break;
            case "MG":
                SetContactor(values, ep, hasE: true, hasAt: true);
                break;
            case "THR":
                SetThr(values, ep);
                break;
            case "SC":
                SetSc(values, ep);
                break;
            case "NT":
                SetNt(values, ep);
                break;
            case "WH":
                SetWh(values, ep);
                break;
            case "VM":
                SetVoltMeter(values, ep, hasVa: false);
                break;
            case "VT":
                SetVoltMeter(values, ep, hasVa: true);
                break;
            case "AM":
                SetAmpMeter(values, ep);
                break;
            case "CT":
                SetCt(values, ep);
                break;
            case "VS":
            case "AS":
                // 【C原典】eparm_set VS/AS(Fyss1f.c:2552/2556): 相数２/線式２ のみ。
                ep.Ph2[0] = Set9(values.Get("p"), 1, 1, "%1.0f", 1.0);
                ep.Wr2[0] = Set9(values.Get("w"), 1, 1, "%1.0f", 1.0);
                break;
            case "TB":
                SetTb(values, ep);
                break;
            case "CON":
                SetCon(values, ep);
                break;
            case "TR":
                SetTr(values, ep);
                break;
            case "ZCT":  // Fyss1f.c:2617
                ep.A2 = Set9(values.Get("a"), 3, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                ep.Ksize = Set9(values.Get("p"), 3, 5, "%05.1f", 1.0);  // 径サイズ
                break;
            case "LGR":  // Fyss1f.c:2628
                ep.K = Set9(values.Get("k"), 2, 3, "%03.0f", 1.0);
                for (int i = 0; i < 4; i++)  // 感度電流 4スロット(from_length=4)
                {
                    ep.Ma[i] = Set9(values.Get($"ma[{i}]"), 4, 4, "%04.0f", 1.0);
                }
                ApplyVc(ep, values);
                break;
            case "ELR":  // Fyss1f.c:2638
                for (int i = 0; i < 3; i++)
                {
                    ep.Ma[i] = Set9(values.Get($"ma[{i}]"), 3, 4, "%04.0f", 1.0);
                }
                ApplyVc(ep, values);
                break;
            case "HPSB":  // Fyss1f.c:2646
            case "HSB":   // Fyss1f.c:2655
                ep.P = Set9(values.Get("p"), 1, 3, "%03.0f", 1.0);
                ep.Af = Set9(values.Get("af"), 3, 9, "%09.3f", 1.0);
                ep.At = Set9(values.Get("at"), 3, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                ep.Am = Set9(values.Get("am"), 3, 3, "%03.0f", 1.0);  // メーター定格
                break;
            case "RRY":  // Fyss1f.c:2664
                ep.P = Set9(values.Get("p"), 1, 3, "%03.0f", 1.0);
                ep.A2 = Set9(values.Get("a"), 2, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                break;
            case "RTR":  // Fyss1f.c:2674
                ep.V1[0] = Set9(values.Get("sv"), 3, 8, "%08.1f", 1.0);
                ApplyV2(ep, values);
                ep.Va = Set9(values.Get("va"), 2, 10, "%010.2f", 1.0);
                break;
            case "MCDT":  // Fyss1f.c:2681
                ep.P = Set9(values.Get("p"), 1, 3, "%03.0f", 1.0);
                ep.A2 = Set9(values.Get("a"), 3, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                break;
            case "F":  // Fyss1f.c:2691
                ep.A2 = Set9(values.Get("a"), 3, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                break;
            case "LA":  // Fyss1f.c:2698
                ep.Ph2[0] = Set9(values.Get("p"), 1, 1, "%1.0f", 1.0);
                ep.Wr2[0] = Set9(values.Get("w"), 1, 1, "%1.0f", 1.0);
                ApplyV2(ep, values);
                break;
            case "DCPW":  // Fyss1f.c:2705
                ep.A2 = Set9(values.Get("a"), 6, 9, "%09.3f", 1.0);
                ep.W1 = Set9(values.Get("w"), 5, 10, "%010.2f", 1.0);
                ep.V1[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);   // 定格電圧1(区分は設定しない)
                ep.V2Kbn = FvKbn(values.Get("fvdc"));
                ep.V2[0] = Set9(values.Get("vdc"), 3, 8, "%08.1f", 1.0);
                break;
            case "CR":  // Fyss1f.c:2717
                ep.A2 = Set9(values.Get("a"), 5, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                ep.Ac = Set9(values.Get("ac"), 2, 2, "%02.0f", 1.0);
                ep.Bc = Set9(values.Get("bc"), 2, 2, "%02.0f", 1.0);
                ep.Cc = Set9(values.Get("cc"), 2, 2, "%02.0f", 1.0);
                break;
            case "TM":  // Fyss1f.c:2729
                SetTimer(values, ep);
                break;
            case "TS":  // Fyss1f.c:2754
                ep.A2 = Set9(values.Get("a"), 5, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                ep.Ac = Set9(values.Get("ac"), 2, 2, "%02.0f", 1.0);
                ep.Bc = Set9(values.Get("bc"), 2, 2, "%02.0f", 1.0);
                ep.Cc = Set9(values.Get("cc"), 2, 2, "%02.0f", 1.0);
                break;
            case "G":
            case "G1":
            case "G2":
            case "G3":
            case "G4":
            case "GI":
            case "GP":
            case "GPN":  // Fyss1f.c:2765 (表示灯群: 制御電圧のみ)
                ApplyVc(ep, values);
                break;
            case "WL":
            case "GL":
            case "RL":
            case "OL":
            case "FL":
            case "BL":  // Fyss1f.c:2782 (表示灯: 電圧/負荷容量/径サイズ)
                ApplyV2(ep, values);
                ep.W1 = Set9(values.Get("w"), 4, 10, "%010.2f", 1.0);
                ep.Ksize = Set9(values.Get("p"), 4, 5, "%05.1f", 1.0);
                break;
            case "COS":  // Fyss1f.c:2793
            case "PBS":  // Fyss1f.c:2800
                ep.A2 = Set9(values.Get("a"), 5, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                ep.Ksize = Set9(values.Get("p"), 4, 5, "%05.1f", 1.0);
                break;
            case "SSW":  // Fyss1f.c:2809
            case "TSW":  // Fyss1f.c:2816
                ep.P = Set9(values.Get("p"), 1, 3, "%03.0f", 1.0);
                ep.A2 = Set9(values.Get("a"), 5, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                break;
            case "BZ":  // Fyss1f.c:2823 (ブザー: W は×1)
                ApplyVc(ep, values);
                SetWva(values, ep, "fwva", "wva", 4, wMultiple: 1.0, vMultiple: 1.0);
                break;
            case "BEL":  // Fyss1f.c:2832 (ベル: W は×1000)
                ApplyVc(ep, values);
                SetWva(values, ep, "fwva", "wva", 4, wMultiple: 1000.0, vMultiple: 1.0);
                ep.Ksize = Set9(values.Get("p"), 3, 5, "%05.1f", 1.0);
                break;
            case "CP":  // Fyss1f.c:2842
                ep.P = Set9(values.Get("p"), 1, 3, "%03.0f", 1.0);
                ep.Af = Set9(values.Get("af"), 2, 9, "%09.3f", 1.0);
                ep.At = Set9(values.Get("at"), 2, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                break;
            case "RSW":  // Fyss1f.c:2850
                ep.K = Set9(values.Get("k"), 3, 3, "%03.0f", 1.0);
                ApplyVc(ep, values);
                break;
            case "EE":  // Fyss1f.c:2856
                ep.A2 = Set9(values.Get("a"), 2, 9, "%09.3f", 1.0);
                ApplyVc(ep, values);
                break;
            case "HM":  // Fyss1f.c:2862
                ApplyVc(ep, values);
                ep.Hz = Set9(values.Get("Hz"), 2, 2, "%02.0f", 1.0);
                break;
            case "CKS":  // Fyss1f.c:2876 (epae は直接代入)
                ep.P = Set9(values.Get("p"), 1, 3, "%03.0f", 1.0);
                ep.E = EDirect(values.Get("e"));
                ep.A2 = Set9(values.Get("a"), 3, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                break;
            case "CSDT":  // Fyss1f.c:2884
                ep.P = Set9(values.Get("p"), 1, 3, "%03.0f", 1.0);
                ep.A2 = Set9(values.Get("a"), 3, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                break;
            case "CU":  // Fyss1f.c:2892
                ApplyVc(ep, values);
                break;
            case "TU":  // Fyss1f.c:2897
                ep.K = Set9(values.Get("k"), 1, 3, "%03.0f", 1.0);
                ApplyVc(ep, values);
                break;
            case "NHMB":  // Fyss1f.c:2903
                ep.P = Set9(values.Get("p"), 1, 3, "%03.0f", 1.0);
                ep.At = Set9(values.Get("at"), 5, 9, "%09.3f", 1.0);
                ep.W1 = Set9(values.Get("kw"), 4, 10, "%010.2f", 1000.0);
                ApplyV2(ep, values);
                break;
            case "APN":  // Fyss1f.c:2911
                ApplyVc(ep, values);
                break;
            case "SL23":
            case "SL32":
            case "SL42":
            case "SL43":  // Fyss1f.c:2916 (制御電圧のみ)
                ApplyVc(ep, values);
                break;
            case "LGT":  // Fyss1f.c:2925
                ep.P = Set9(values.Get("p"), 1, 3, "%03.0f", 1.0);
                ep.A2 = Set9(values.Get("a"), 4, 9, "%09.3f", 1.0);
                ep.T = Set9(values.Get("t"), 4, 5, "%05.1f", 1.0);   // 板厚
                ep.W2 = Set9(values.Get("w"), 3, 3, "%03.0f", 1.0);  // 幅
                break;
            case "BLTR":  // Fyss1f.c:2931
                ep.V1[0] = Set9(values.Get("sv"), 3, 8, "%08.1f", 1.0);
                ApplyV2(ep, values, vLen: 2);
                ep.Va = Set9(values.Get("va"), 2, 10, "%010.2f", 1.0);
                break;
            case "PLTR":  // Fyss1f.c:2938
                ep.V1[0] = Set9(values.Get("sv"), 3, 8, "%08.1f", 1.0);
                ApplyV2(ep, values, vLen: 4);
                ep.Va = Set9(values.Get("va"), 1, 10, "%010.2f", 1.0);
                break;
            case "LSW":  // Fyss1f.c:2951
            case "DSW":  // Fyss1f.c:2957
                ep.A2 = Set9(values.Get("a"), 6, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                break;
            case "SV":  // Fyss1f.c:2963
                ApplyV2(ep, values);
                ep.Va = Set9(values.Get("va"), 2, 10, "%010.2f", 1.0);
                break;
            case "MV":  // Fyss1f.c:2969 (fwva で W×1000 / V×1 分岐)
                ApplyV2(ep, values);
                SetWva(values, ep, "fwva", "va", 3, wMultiple: 1000.0, vMultiple: 1.0);
                break;
            case "KPRY":  // Fyss1f.c:2978 (接点数 from_length=1)
                ep.A2 = Set9(values.Get("a"), 5, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                ep.Ac = Set9(values.Get("ac"), 1, 2, "%02.0f", 1.0);
                ep.Bc = Set9(values.Get("bc"), 1, 2, "%02.0f", 1.0);
                ep.Cc = Set9(values.Get("cc"), 1, 2, "%02.0f", 1.0);
                break;
            case "THSW":  // Fyss1f.c:2990 (温度設定)
                ep.A2 = Set9(values.Get("a"), 5, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                ep.C1 = Set9(values.Get("cs"), 3, 3, "%03.0f", 1.0);
                ep.C2 = Set9(values.Get("c"), 3, 3, "%03.0f", 1.0);
                ep.Cset = Set9(values.Get("cset"), 3, 3, "%03.0f", 1.0);
                break;
            case "L":  // Fyss1f.c:2999
                ep.Ph2[0] = Set9(values.Get("p"), 1, 1, "%1.0f", 1.0);
                ep.Wr2[0] = Set9(values.Get("w"), 1, 1, "%1.0f", 1.0);
                ep.A2 = Set9(values.Get("a"), 2, 9, "%09.3f", 1.0);
                break;
            case "IDF":
            case "HDF":
            case "MDF":  // Fyss1f.c:3004/3007/3010 (極数のみ, from_length=3)
                ep.P = Set9(values.Get("p"), 3, 3, "%03.0f", 1.0);
                break;
            case "TV":  // Fyss1f.c:3013 (C原典は何もしない)
                break;
            case "WDP":  // Fyss1f.c:3015
                ep.T = Set9(values.Get("t"), 2, 5, "%05.1f", 1.0);
                break;
            case "MCFR":  // Fyss1f.c:3018
                ep.A2 = Set9(values.Get("a"), 6, 9, "%09.3f", 1.0);
                ep.W1 = Set9(values.Get("kw"), 6, 10, "%010.2f", 1000.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                ep.Ac = Set9(values.Get("ac"), 1, 2, "%02.0f", 1.0);
                ep.Bc = Set9(values.Get("bc"), 1, 2, "%02.0f", 1.0);
                break;
            case "MGFR":  // Fyss1f.c:3030 (epae 直接代入 + at)
                ep.E = EDirect(values.Get("e"));
                ep.A2 = Set9(values.Get("a"), 6, 9, "%09.3f", 1.0);
                ep.W1 = Set9(values.Get("kw"), 6, 10, "%010.2f", 1000.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                ep.Ac = Set9(values.Get("ac"), 1, 2, "%02.0f", 1.0);
                ep.Bc = Set9(values.Get("bc"), 1, 2, "%02.0f", 1.0);
                ep.At = Set9(values.Get("at"), 6, 9, "%09.3f", 1.0);
                break;
            case "MCSD":  // Fyss1f.c:3044
                ep.A2 = Set9(values.Get("a"), 6, 9, "%09.3f", 1.0);
                ep.W1 = Set9(values.Get("kw"), 6, 10, "%010.2f", 1000.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                break;
            case "MGSD":  // Fyss1f.c:3054 (epae 直接代入 + at)
                ep.E = EDirect(values.Get("e"));
                ep.A2 = Set9(values.Get("a"), 6, 9, "%09.3f", 1.0);
                ep.W1 = Set9(values.Get("kw"), 6, 10, "%010.2f", 1000.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                ep.At = Set9(values.Get("at"), 6, 9, "%09.3f", 1.0);
                break;
            case "MGLD":  // Fyss1f.c:3066
            case "MGCS":  // Fyss1f.c:3075
            case "INV":   // Fyss1f.c:3084
                ep.W1 = Set9(values.Get("kw"), 6, 10, "%010.2f", 1000.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                break;
            case "DCSIR":  // Fyss1f.c:3105 (epav2kbn を fv → fvdc で上書き)
                ep.A2 = Set9(values.Get("a"), 6, 9, "%09.3f", 1.0);
                ep.W1 = Set9(values.Get("w"), 5, 10, "%010.2f", 1.0);
                ep.V2Kbn = FvKbn(values.Get("fv"));
                ep.V1[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);
                ep.V2Kbn = FvKbn(values.Get("fvdc"));
                ep.V2[0] = Set9(values.Get("vdc"), 4, 8, "%08.1f", 1.0);
                break;
            case "DCNI":  // Fyss1f.c:3115
                ep.A2 = Set9(values.Get("a"), 6, 9, "%09.3f", 1.0);
                ep.W1 = Set9(values.Get("w"), 5, 10, "%010.2f", 1.0);
                ep.V2Kbn = FvKbn(values.Get("fv"));
                ep.V1[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);
                ep.V2Kbn = FvKbn(values.Get("fvdc"));
                ep.V2[0] = Set9(values.Get("vdc"), 4, 8, "%08.1f", 1.0);
                ep.Mah = Set9(values.Get("mah"), 5, 5, "%05.0f", 1.0);
                break;
            case "MCFRSD":  // Fyss1f.c:3126
                ep.A2 = Set9(values.Get("a"), 6, 9, "%09.3f", 1.0);
                ep.W1 = Set9(values.Get("kw"), 6, 10, "%010.2f", 1000.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                break;
            case "MGFRSD":  // Fyss1f.c:3136 (epae 直接代入 + at)
                ep.E = EDirect(values.Get("e"));
                ep.A2 = Set9(values.Get("a"), 6, 9, "%09.3f", 1.0);
                ep.W1 = Set9(values.Get("kw"), 6, 10, "%010.2f", 1000.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                ep.At = Set9(values.Get("at"), 6, 9, "%09.3f", 1.0);
                break;
            case "STM":
            case "SIR":
            case "C":
            case "R":
            case "D":
            case "NICA":
            case "RE":
            case "VVVF":  // Fyss1f.c:3148-3162 (C原典は空分岐 → '0' 埋めのまま)
                break;
            case "TSU":    // Fyss1f.c:3164
            case "SSWU":   // Fyss1f.c:3173
            case "PBSU":   // Fyss1f.c:3182
            case "COSU":   // Fyss1f.c:3191
            case "2COSU":  // Fyss1f.c:3200
            case "OLU":    // Fyss1f.c:3209 (単位スイッチ群)
                ep.A2 = Set9(values.Get("a"), 5, 9, "%09.3f", 1.0);
                ApplyV2(ep, values);
                ApplyVc(ep, values);
                ep.K = Set9(values.Get("k"), 2, 3, "%03.0f", 1.0);
                break;
            default:
                // 未収録予約語: ep は '0' 埋めのまま(C の Main_Area_Clear 相当)。
                break;
        }

        return ep;
    }

    /// <summary>
    /// 遮断器系(MCB/ELB/MMCB/ELMB/SB)の共通整形。【C原典】eparm_set(Fyss1f.c:2260~2478)の該当分岐。
    /// 各機種の差異は引数で表現する:
    /// <list type="bullet">
    /// <item><paramref name="afLength"/>/<paramref name="atLength"/> … set_9 の from_length(fyrt811 の AF/AT 桁)。</item>
    /// <item><paramref name="atStofSpecial"/> … MCB/ELB のみ: Stof(at,4)==0 かつ at 非空なら epaat="99999.999"。</item>
    /// <item><paramref name="hasMa"/> … ELB/ELMB: 感度電流 ma[0..2] を epama[0..2] へ("%04.0f")。</item>
    /// <item><paramref name="hasKw"/> … MMCB/ELMB/RMMCB/RELMB: kw×1000 を epaw1 へ("%010.2f")。</item>
    /// <item><paramref name="eZeroToNine"/> … MCB/ELB/SB: e=='0' のとき epae='9'(MMCB/ELMB/R系は無し)。</item>
    /// <item><paramref name="hasVc"/> … RMCB/RELB/RMMCB/RELMB: 制御電圧 vc/fvc を epavc/epavckbn へ。</item>
    /// </list>
    /// </summary>
    private static void SetBreaker(
        RatingValues values,
        ElectricalParameters ep,
        int afLength,
        int atLength,
        bool atStofSpecial,
        bool hasMa,
        bool hasKw,
        bool eZeroToNine,
        bool hasVc = false)
    {
        // 極数（Ｐ）：set_9(&u->xxx.p, 1, ep->epap, 3, "%03.0f", 1.0)
        ep.P = Set9(values.Get("p"), 1, 3, "%03.0f", 1.0);

        // エレメント数（Ｅ）
        string? e = values.Get("e");
        if (eZeroToNine && e == "0")
        {
            // 【C原典】if( u->xxx.e == '0' ) ep->epae = '9';
            ep.E = "9";
        }
        else
        {
            // 【C原典】ep->epae = u->xxx.e ? u->xxx.e : '0';
            ep.E = !string.IsNullOrEmpty(e) ? e[..1] : "0";
        }

        // フレーム電流（ＡＦ）：set_9(u->xxx.af, afLength, ep->epaaf, 9, "%09.3f", 1.0)
        ep.Af = Set9(values.Get("af"), afLength, 9, "%09.3f", 1.0);

        // トリップ電流（ＡＴ）
        string? at = values.Get("at");
        if (atStofSpecial)
        {
            // 【C原典】dwork = Stof(u->xxx.at,4);
            //         if( dwork == 0.0 && u->xxx.at[0] != '\0' ) set_9("99999.999",…) else set_9(u->xxx.at,…)
            double dwork = Stof(at, 4);
            ep.At = dwork == 0.0 && !string.IsNullOrEmpty(at)
                ? Set9("99999.999", 9, 9, "%09.3f", 1.0)
                : Set9(at, atLength, 9, "%09.3f", 1.0);
        }
        else
        {
            ep.At = Set9(at, atLength, 9, "%09.3f", 1.0);
        }

        // 負荷容量（Ｗ）：MMCB/ELMB のみ set_9(u->xxx.kw, 6, ep->epaw1, 10, "%010.2f", 1000.0)
        if (hasKw)
        {
            ep.W1 = Set9(values.Get("kw"), 6, 10, "%010.2f", 1000.0);
        }

        // 感度電流（ＭＡ）：ELB/ELMB のみ set_9(u->xxx.ma[i], 3, ep->epama[i], 4, "%04.0f", 1.0)
        if (hasMa)
        {
            for (int i = 0; i < 3; i++)
            {
                ep.Ma[i] = Set9(values.Get($"ma[{i}]"), 3, 4, "%04.0f", 1.0);
            }
        }

        // 定格電圧2 ＡＣ／ＤＣ区分：ep->epav2kbn = u->xxx.fv ? ((fv=='A')?'A':'D') : ' '
        string? fv = values.Get("fv");
        ep.V2Kbn = string.IsNullOrEmpty(fv) ? ' ' : (fv[0] == 'A' ? 'A' : 'D');

        // 定格電圧2：set_9(u->xxx.v, 3, ep->epav2[0], 8, "%08.1f", 1.0)
        ep.V2[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);

        // 制御電圧2：RMCB/RELB/RMMCB/RELMB のみ
        // ep->epavckbn = u->xxx.fvc ? ((fvc=='A')?'A':'D') : ' ';
        // set_9(u->xxx.vc, 3, ep->epavc, 3, "%03.0f", 1.0);
        if (hasVc)
        {
            string? fvc = values.Get("fvc");
            ep.VcKbn = string.IsNullOrEmpty(fvc) ? ' ' : (fvc[0] == 'A' ? 'A' : 'D');
            ep.Vc = Set9(values.Get("vc"), 3, 3, "%03.0f", 1.0);
        }
    }

    /// <summary>
    /// 定格電圧の ＡＣ／ＤＣ区分。【C原典】<c>u-&gt;xxx.fv ? ((fv=='A')?'A':'D') : ' '</c>。
    /// fv 未設定は ' '、先頭 'A' は 'A'、それ以外は 'D'。
    /// </summary>
    private static char FvKbn(string? fv)
        => string.IsNullOrEmpty(fv) ? ' ' : (fv[0] == 'A' ? 'A' : 'D');

    /// <summary>
    /// 引込(PS/P)。【C原典】eparm_set PS(Fyss1f.c:2219)/P(Fyss1f.c:2233)。
    /// 相数１/線式１(epaph2[0]/epawr2[0] を "%1.0f")、定格電圧2 を3スロット(epav2[0..2])へ。
    /// P のみ電線サイズ(epasq/epaesq "%06.2f")と芯数(epac)/回線数(epaksu)を追加。
    /// </summary>
    private static void SetIncoming(RatingValues values, ElectricalParameters ep, bool withCableSize)
    {
        // set_9(&u->xxx.p, 1, &ep->epaph2[0], 1, "%1.0f", 1.0)
        ep.Ph2[0] = Set9(values.Get("p"), 1, 1, "%1.0f", 1.0);
        // set_9(&u->xxx.w, 1, &ep->epawr2[0], 1, "%1.0f", 1.0)
        ep.Wr2[0] = Set9(values.Get("w"), 1, 1, "%1.0f", 1.0);
        // ep->epav2kbn = u->xxx.fv[0] ? … : ' '
        ep.V2Kbn = FvKbn(values.Get("fv"));
        // set_9(u->xxx.v[i], 3, ep->epav2[i], 8, "%08.1f", 1.0)  (3スロット)
        for (int i = 0; i < 3; i++)
        {
            ep.V2[i] = Set9(values.Get($"v[{i}]"), 3, 8, "%08.1f", 1.0);
        }

        if (withCableSize)
        {
            // set_9(u->p.sq, 3, ep->epasq, 6, "%06.2f", 1.0)
            ep.Sq = Set9(values.Get("sq"), 3, 6, "%06.2f", 1.0);
            // set_9(u->p.esq, 3, ep->epaesq, 6, "%06.2f", 1.0)
            ep.Esq = Set9(values.Get("esq"), 3, 6, "%06.2f", 1.0);
            // ep->epac = u->p.c ? u->p.c : '0';  (芯数、char 直接コピー)
            string? c = values.Get("c");
            ep.C = !string.IsNullOrEmpty(c) ? c[0] : '0';
            // ep->epaksu = u->p.k ? u->p.k : '0';  (本数、char 直接コピー)
            string? k = values.Get("k");
            ep.Ksu = !string.IsNullOrEmpty(k) ? k[0] : '0';
        }
    }

    /// <summary>
    /// 電磁接触器系(MC/MG)。【C原典】eparm_set MC(Fyss1f.c:2459)/MG(Fyss1f.c:2483)。
    /// 極数/定格電流2(epaa2)/負荷容量(kW×1000→epaw1)/定格電圧2/制御電圧2/ａｂ接点数(epaac/epabc "%02.0f")。
    /// MG のみエレメント数(epae)とトリップ電流(at,6桁→epaat)を追加。
    /// </summary>
    private static void SetContactor(RatingValues values, ElectricalParameters ep, bool hasE, bool hasAt)
    {
        // set_9(&u->xxx.p, 1, ep->epap, 3, "%03.0f", 1.0)
        ep.P = Set9(values.Get("p"), 1, 3, "%03.0f", 1.0);

        if (hasE)
        {
            // ep->epae = u->mg.e ? u->mg.e : '0';
            string? e = values.Get("e");
            ep.E = !string.IsNullOrEmpty(e) ? e[..1] : "0";
        }

        // set_9(u->xxx.a, 3, ep->epaa2, 9, "%09.3f", 1.0)
        ep.A2 = Set9(values.Get("a"), 3, 9, "%09.3f", 1.0);

        if (hasAt)
        {
            // set_9(u->mg.at, 6, ep->epaat, 9, "%09.3f", 1.0)
            ep.At = Set9(values.Get("at"), 6, 9, "%09.3f", 1.0);
        }

        // set_9(u->xxx.kw, 6, ep->epaw1, 10, "%010.2f", 1000.0)
        ep.W1 = Set9(values.Get("kw"), 6, 10, "%010.2f", 1000.0);
        // 定格電圧2
        ep.V2Kbn = FvKbn(values.Get("fv"));
        ep.V2[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);
        // 制御電圧2
        ep.VcKbn = FvKbn(values.Get("fvc"));
        ep.Vc = Set9(values.Get("vc"), 3, 3, "%03.0f", 1.0);
        // set_9(&u->xxx.ac, 1, ep->epaac, 2, "%02.0f", 1.0)
        ep.Ac = Set9(values.Get("ac"), 1, 2, "%02.0f", 1.0);
        // set_9(&u->xxx.bc, 1, ep->epabc, 2, "%02.0f", 1.0)
        ep.Bc = Set9(values.Get("bc"), 1, 2, "%02.0f", 1.0);
    }

    /// <summary>
    /// サーマルリレー(THR)。【C原典】eparm_set THR(Fyss1f.c:2474)。
    /// エレメント数/トリップ電流(at,6桁)/負荷容量(kW×1000)/定格電圧2。
    /// </summary>
    private static void SetThr(RatingValues values, ElectricalParameters ep)
    {
        // ep->epae = u->thr.e ? u->thr.e : '0';
        string? e = values.Get("e");
        ep.E = !string.IsNullOrEmpty(e) ? e[..1] : "0";
        ep.At = Set9(values.Get("at"), 6, 9, "%09.3f", 1.0);
        ep.W1 = Set9(values.Get("kw"), 6, 10, "%010.2f", 1000.0);
        ep.V2Kbn = FvKbn(values.Get("fv"));
        ep.V2[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);
    }

    /// <summary>
    /// 進相コンデンサ(SC)。【C原典】eparm_set SC(Fyss1f.c:2499)。
    /// 相数１(epaph2[0])/定格容量(kvar,6桁→epakvar "%06.2f")/静電容量(uf,6桁→epauf "%08.1f")/
    /// 定格電圧2/周波数(Hz,2桁→epahz "%02.0f")。
    /// </summary>
    private static void SetSc(RatingValues values, ElectricalParameters ep)
    {
        ep.Ph2[0] = Set9(values.Get("p"), 1, 1, "%1.0f", 1.0);
        ep.Kvar = Set9(values.Get("kvar"), 6, 6, "%06.2f", 1.0);
        ep.Uf = Set9(values.Get("uf"), 6, 8, "%08.1f", 1.0);
        ep.V2Kbn = FvKbn(values.Get("fv"));
        ep.V2[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);
        ep.Hz = Set9(values.Get("Hz"), 2, 2, "%02.0f", 1.0);
    }

    /// <summary>
    /// 中性線用端子台(NT)。【C原典】eparm_set NT(Fyss1f.c:2509)。
    /// 極数(p,3桁)/定格電流2(a,2桁→epaa2)/定格電圧2。
    /// </summary>
    private static void SetNt(RatingValues values, ElectricalParameters ep)
    {
        // set_9(u->nt.p, 3, ep->epap, 3, "%03.0f", 1.0)  (p は3桁配列)
        ep.P = Set9(values.Get("p"), 3, 3, "%03.0f", 1.0);
        ep.A2 = Set9(values.Get("a"), 2, 9, "%09.3f", 1.0);
        ep.V2Kbn = FvKbn(values.Get("fv"));
        ep.V2[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);
    }

    /// <summary>
    /// 電力量計(WH)。【C原典】eparm_set WH(Fyss1f.c:2516)。
    /// 相数/線式(epaph2[0]/epawr2[0])/定格電流1(sa→epaa1)/定格電流2(a→epaa2)/
    /// 定格電圧1(sv→epav1[0])/定格電圧2/周波数(Hz)。
    /// </summary>
    private static void SetWh(RatingValues values, ElectricalParameters ep)
    {
        ep.Ph2[0] = Set9(values.Get("p"), 1, 1, "%1.0f", 1.0);
        ep.Wr2[0] = Set9(values.Get("w"), 1, 1, "%1.0f", 1.0);
        ep.A1 = Set9(values.Get("sa"), 3, 9, "%09.3f", 1.0);
        ep.A2 = Set9(values.Get("a"), 3, 9, "%09.3f", 1.0);
        ep.V1[0] = Set9(values.Get("sv"), 3, 8, "%08.1f", 1.0);
        ep.V2Kbn = FvKbn(values.Get("fv"));
        ep.V2[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);
        ep.Hz = Set9(values.Get("Hz"), 2, 2, "%02.0f", 1.0);
    }

    /// <summary>
    /// 電圧計/計器用変圧器(VM/VT)。【C原典】eparm_set VM(Fyss1f.c:2528)/VT(Fyss1f.c:2538)。
    /// 定格電圧1(sv→epav1[0])/定格電圧2。VT のみ定格容量(va,3桁→epava "%010.2f")を追加。
    /// </summary>
    private static void SetVoltMeter(RatingValues values, ElectricalParameters ep, bool hasVa)
    {
        ep.V1[0] = Set9(values.Get("sv"), 3, 8, "%08.1f", 1.0);
        ep.V2Kbn = FvKbn(values.Get("fv"));
        ep.V2[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);
        if (hasVa)
        {
            ep.Va = Set9(values.Get("va"), 3, 10, "%010.2f", 1.0);
        }
    }

    /// <summary>
    /// 電流計(AM)。【C原典】eparm_set AM(Fyss1f.c:2534)。
    /// 定格電流1(sa→epaa1)/定格電流2(a→epaa2)のみ。
    /// </summary>
    private static void SetAmpMeter(RatingValues values, ElectricalParameters ep)
    {
        ep.A1 = Set9(values.Get("sa"), 3, 9, "%09.3f", 1.0);
        ep.A2 = Set9(values.Get("a"), 3, 9, "%09.3f", 1.0);
    }

    /// <summary>
    /// 計器用変流器(CT)。【C原典】eparm_set CT(Fyss1f.c:2546)。
    /// 定格電流1(sa,4桁→epaa1)/定格電流2(a,3桁→epaa2)/定格容量(va,2桁→epava "%010.2f")。
    /// </summary>
    private static void SetCt(RatingValues values, ElectricalParameters ep)
    {
        ep.A1 = Set9(values.Get("sa"), 4, 9, "%09.3f", 1.0);
        ep.A2 = Set9(values.Get("a"), 3, 9, "%09.3f", 1.0);
        ep.Va = Set9(values.Get("va"), 2, 10, "%010.2f", 1.0);
    }

    /// <summary>
    /// 端子台(TB)。【C原典】eparm_set TB(Fyss1f.c:2560)。
    /// 極数(p,3桁→epap)/定格電流2(a,3桁→epaa2)/定格電圧2/電線サイズ(sq,6桁→epasq "%06.2f")。
    /// </summary>
    private static void SetTb(RatingValues values, ElectricalParameters ep)
    {
        ep.P = Set9(values.Get("p"), 3, 3, "%03.0f", 1.0);
        ep.A2 = Set9(values.Get("a"), 3, 9, "%09.3f", 1.0);
        ep.V2Kbn = FvKbn(values.Get("fv"));
        ep.V2[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);
        ep.Sq = Set9(values.Get("sq"), 6, 6, "%06.2f", 1.0);
    }

    /// <summary>
    /// コネクタ(CON)。【C原典】eparm_set CON(Fyss1f.c:2568)。
    /// 極数(p,1桁→epap)/定格電流2(a,2桁→epaa2)/定格電圧2。
    /// </summary>
    private static void SetCon(RatingValues values, ElectricalParameters ep)
    {
        ep.P = Set9(values.Get("p"), 1, 3, "%03.0f", 1.0);
        ep.A2 = Set9(values.Get("a"), 2, 9, "%09.3f", 1.0);
        ep.V2Kbn = FvKbn(values.Get("fv"));
        ep.V2[0] = Set9(values.Get("v"), 3, 8, "%08.1f", 1.0);
    }

    /// <summary>
    /// 変圧器(TR)。【C原典】eparm_set TR(Fyss1f.c:2575)。多スロットの最複雑分岐。
    /// <list type="bullet">
    /// <item>1次: 相数(p1→epaph1)/線式(w1→epawr1)/定格電圧1 3スロット(v1[0..2]→epav1[0..2])。
    ///   各 v1[i] の4文字目(index 3)が 'T' ならタップ使用インデックス epav1idx=i+1。</item>
    /// <item>2次相数(PH2)を 0 でないものから順詰め: chk_9(p2)≠0 なら {p2→epaph2[0], p3→epaph2[1]}、
    ///   さもなくば {p3→epaph2[0]}。線式(WR2)も同様に w2/w3 を順詰め。</item>
    /// <item>AC/DC区分: fv2 が 'A'/'D' なら fv2、さもなくば fv3 で判定。</item>
    /// <item>2次電圧(V2): v2[i](i=0..2)のうち chk_9≠0 のものを epav2[i] へ、4文字目 'T' で epav2idx=i+1。
    ///   加えて v3[0]→epav2[1]、v3[1]→epav2[2](各 chk_9≠0 のときのみ上書き)。</item>
    /// <item>定格容量(va,6桁→epava "%010.2f")。</item>
    /// </list>
    /// </summary>
    private static void SetTr(RatingValues values, ElectricalParameters ep)
    {
        // 1次 相数/線式
        ep.Ph1 = Set9(values.Get("p1"), 1, 1, "%1.0f", 1.0);
        ep.Wr1 = Set9(values.Get("w1"), 1, 1, "%1.0f", 1.0);

        // 1次 定格電圧1 3スロット + タップインデックス(4文字目 'T')
        for (int i = 0; i < 3; i++)
        {
            string? v1 = values.Get($"v1[{i}]");
            ep.V1[i] = Set9(v1, 3, 8, "%08.1f", 1.0);
            if (v1 is not null && v1.Length > 3 && v1[3] == 'T')
            {
                ep.V1Idx = (i + 1).ToString(CultureInfo.InvariantCulture);
            }
        }

        // 2次 相数(PH2): 0 でないものから順詰め
        if (Chk9(values.Get("p2"), 1) != 0.0)
        {
            ep.Ph2[0] = Set9(values.Get("p2"), 1, 1, "%1.0f", 1.0);
            ep.Ph2[1] = Set9(values.Get("p3"), 1, 1, "%1.0f", 1.0);
        }
        else
        {
            ep.Ph2[0] = Set9(values.Get("p3"), 1, 1, "%1.0f", 1.0);
        }

        // 2次 線式(WR2): 0 でないものから順詰め
        if (Chk9(values.Get("w2"), 1) != 0.0)
        {
            ep.Wr2[0] = Set9(values.Get("w2"), 1, 1, "%1.0f", 1.0);
            ep.Wr2[1] = Set9(values.Get("w3"), 1, 1, "%1.0f", 1.0);
        }
        else
        {
            ep.Wr2[0] = Set9(values.Get("w3"), 1, 1, "%1.0f", 1.0);
        }

        // AC/DC区分: fv2 が 'A'/'D' なら fv2、さもなくば fv3
        string? fv2 = values.Get("fv2");
        char fv2c = string.IsNullOrEmpty(fv2) ? '\0' : fv2[0];
        ep.V2Kbn = fv2c is 'A' or 'D' ? FvKbn(fv2) : FvKbn(values.Get("fv3"));

        // 2次 電圧(V2): chk_9≠0 のものを詰める + タップインデックス
        for (int i = 0; i < 3; i++)
        {
            string? v2 = values.Get($"v2[{i}]");
            if (Chk9(v2, 3) != 0.0)
            {
                ep.V2[i] = Set9(v2, 3, 8, "%08.1f", 1.0);
                if (v2 is not null && v2.Length > 3 && v2[3] == 'T')
                {
                    ep.V2Idx = (i + 1).ToString(CultureInfo.InvariantCulture);
                }
            }
        }
        // v3[0]→epav2[1]、v3[1]→epav2[2](各 chk_9≠0 のときのみ)
        string? v3_0 = values.Get("v3[0]");
        if (Chk9(v3_0, 3) != 0.0)
        {
            ep.V2[1] = Set9(v3_0, 3, 8, "%08.1f", 1.0);
        }
        string? v3_1 = values.Get("v3[1]");
        if (Chk9(v3_1, 3) != 0.0)
        {
            ep.V2[2] = Set9(v3_1, 3, 8, "%08.1f", 1.0);
        }

        // 定格容量
        ep.Va = Set9(values.Get("va"), 6, 10, "%010.2f", 1.0);
    }

    /// <summary>定格電圧2 区分/電圧を共通セットする(【C原典】epav2kbn=fv?…; set_9(v,vLen,epav2[0],8,"%08.1f"))。</summary>
    private static void ApplyV2(ElectricalParameters ep, RatingValues values, string vField = "v", int vLen = 3, string fvField = "fv")
    {
        ep.V2Kbn = FvKbn(values.Get(fvField));
        ep.V2[0] = Set9(values.Get(vField), vLen, 8, "%08.1f", 1.0);
    }

    /// <summary>制御電圧2 区分/電圧を共通セットする(【C原典】epavckbn=fvc?…; set_9(vc,3,epavc,3,"%03.0f"))。</summary>
    private static void ApplyVc(ElectricalParameters ep, RatingValues values)
    {
        ep.VcKbn = FvKbn(values.Get("fvc"));
        ep.Vc = Set9(values.Get("vc"), 3, 3, "%03.0f", 1.0);
    }

    /// <summary>
    /// エレメント数(Ｅ)を直接代入する(【C原典】<c>ep-&gt;epae = u-&gt;xxx.e;</c>)。
    /// MCB 系の <c>e?e:'0'</c> と異なり NUL 保護しない。未設定時は C の直接バイトコピー('\0')を再現する。
    /// </summary>
    private static string EDirect(string? e) => !string.IsNullOrEmpty(e) ? e[..1] : "\0";

    /// <summary>
    /// W/V 区分付き容量をセットする(【C原典】BZ/BEL/MV: fwva=='W'→epaw1, =='V'→epava)。
    /// </summary>
    private static void SetWva(RatingValues values, ElectricalParameters ep, string fwvaField, string valueField, int len, double wMultiple, double vMultiple)
    {
        string? fwva = values.Get(fwvaField);
        char f = string.IsNullOrEmpty(fwva) ? '\0' : fwva[0];
        if (f == 'W')
        {
            ep.W1 = Set9(values.Get(valueField), len, 10, "%010.2f", wMultiple);
        }
        else if (f == 'V')
        {
            ep.Va = Set9(values.Get(valueField), len, 10, "%010.2f", vMultiple);
        }
    }

    /// <summary>
    /// 【C原典】TM(タイマ、Fyss1f.c:2729)。定格電流/電圧2/制御電圧2 に加え、
    /// nset/nss/ns の時間単位('1':秒×1, '2':分×60, '3':時×3600)で SSET/SS/S を整形する。
    /// </summary>
    private static void SetTimer(RatingValues values, ElectricalParameters ep)
    {
        ep.A2 = Set9(values.Get("a"), 5, 9, "%09.3f", 1.0);
        ApplyV2(ep, values);
        ApplyVc(ep, values);
        ep.Sset = SetTime(values, "nset", "set", ep.Sset);
        ep.Ss = SetTime(values, "nss", "ss", ep.Ss);
        ep.S = SetTime(values, "ns", "s", ep.S);
        ep.Ac = Set9(values.Get("ac"), 2, 2, "%02.0f", 1.0);
        ep.Bc = Set9(values.Get("bc"), 2, 2, "%02.0f", 1.0);
        ep.Cc = Set9(values.Get("cc"), 2, 2, "%02.0f", 1.0);
    }

    /// <summary>
    /// タイマ時間値を単位フラグに応じて整形する。'1':×1 '2':×60 '3':×3600、
    /// それ以外は C では set_9 を呼ばず現状値('0' 埋め)のまま。
    /// </summary>
    private static string SetTime(RatingValues values, string nField, string valField, string current)
    {
        string? n = values.Get(nField);
        char c = string.IsNullOrEmpty(n) ? '\0' : n[0];
        double mul = c switch { '1' => 1.0, '2' => 60.0, '3' => 3600.0, _ => double.NaN };
        return double.IsNaN(mul) ? current : Set9(values.Get(valField), 9, 13, "%013.3f", mul);
    }

    /// <summary>【C原典】XERY(2ERY/3ERY/4ERY、Fyss1f.c:2868)。AF/AT/負荷容量(×1000)/制御電圧2。</summary>
    private static void SetXery(RatingValues values, ElectricalParameters ep)
    {
        ep.Af = Set9(values.Get("af"), 6, 9, "%09.3f", 1.0);
        ep.At = Set9(values.Get("at"), 6, 9, "%09.3f", 1.0);
        ep.W1 = Set9(values.Get("kw"), 6, 10, "%010.2f", 1000.0);
        ApplyVc(ep, values);
    }

    /// <summary>【C原典】FLTx(Fyss1f.c:3093、memcmp 先頭3文字 "FLT")。定格電流/電圧2/制御電圧2/接点数。</summary>
    private static void SetFltx(RatingValues values, ElectricalParameters ep)
    {
        ep.A2 = Set9(values.Get("a"), 5, 9, "%09.3f", 1.0);
        ApplyV2(ep, values);
        ApplyVc(ep, values);
        ep.Ac = Set9(values.Get("ac"), 2, 2, "%02.0f", 1.0);
        ep.Bc = Set9(values.Get("bc"), 2, 2, "%02.0f", 1.0);
        ep.Cc = Set9(values.Get("cc"), 2, 2, "%02.0f", 1.0);
    }

    /// <summary>
    /// 【C原典】<c>set_9</c>(Fyss1f.c:3230)。
    /// <code>
    /// strncpy(work, from, from_length); work[from_length]='\0';
    /// f = atof(work) * multiple;
    /// sprintf(buff, format, f);
    /// strncpy(to, buff, to_length);   // 末尾 NUL 埋めなし(固定長)
    /// </code>
    /// を再現する。<paramref name="from"/> が null(未設定フィールド)の場合は C の union 0 埋めと同じく "" 扱い。
    /// </summary>
    /// <param name="from">元値(【C原典】from、fyrt811 の該当フィールド)。null=未設定。</param>
    /// <param name="fromLength">元値の使用桁数(【C原典】from_length)。</param>
    /// <param name="toLength">格納先の固定桁数(【C原典】to_length)。</param>
    /// <param name="format">C の sprintf 書式(【C原典】format、例 "%09.3f")。</param>
    /// <param name="multiple">乗率(【C原典】multiple)。</param>
    /// <returns>整形済み固定長文字列(【C原典】to、to_length 桁)。</returns>
    public static string Set9(string? from, int fromLength, int toLength, string format, double multiple)
    {
        // strncpy(work, from, from_length); work[from_length]='\0';
        string src = from ?? string.Empty;
        string work = src.Length > fromLength ? src[..fromLength] : src;

        double f = AtofC(work) * multiple;
        string buff = SprintfF(format, f);

        // strncpy(to, buff, to_length): to_length 桁ぶんを転記。buff が長ければ切り詰め。
        if (buff.Length >= toLength)
        {
            return buff[..toLength];
        }

        // buff が短い場合、C の strncpy は残りを '\0' で埋める(固定長フィールド)。
        // 本移植の書式(ゼロ埋め幅=to_length)では通常発生しないが忠実性のため NUL 埋めする。
        return buff.PadRight(toLength, '\0');
    }

    /// <summary>
    /// 【C原典】<c>chk_9(CHAR* from, INT from_length)</c>(Fyss1f.c:3253)。
    /// from 先頭 from_length 文字を atof した値を返す。
    /// </summary>
    public static double Chk9(string? from, int fromLength)
    {
        string src = from ?? string.Empty;
        string work = src.Length > fromLength ? src[..fromLength] : src;
        return AtofC(work);
    }

    /// <summary>
    /// 【C原典】<c>Stof(CHAR* str, SHORT size)</c>(Fysk09.c:10)。
    /// str 先頭 size 文字を atof した値を返す(chk_9 と同義)。null は "" 扱い。
    /// </summary>
    public static double Stof(string? str, int size)
    {
        string src = str ?? string.Empty;
        string work = src.Length > size ? src[..size] : src;
        return AtofC(work);
    }

    /// <summary>
    /// C の <c>sprintf(buff, "%[0]W.Df", value)</c> 相当。ゼロ埋め('0' フラグ)/空白埋めの
    /// 最小幅 W・小数桁 D の固定小数点書式を再現する。
    /// </summary>
    private static string SprintfF(string cFormat, double value)
    {
        Match m = FloatFormat.Match(cFormat);
        if (!m.Success)
        {
            throw new ArgumentException($"未対応の書式です: {cFormat}", nameof(cFormat));
        }

        bool zeroPad = m.Groups[1].Value == "0";
        int width = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        int prec = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);

        string num = value.ToString("F" + prec.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        if (num.Length >= width)
        {
            return num;
        }

        return zeroPad ? num.PadLeft(width, '0') : num.PadLeft(width, ' ');
    }

    /// <summary>"%[0]W.Df" 形式(浮動小数点固定書式)のパターン。</summary>
    private static readonly Regex FloatFormat = new(@"^%(0?)(\d+)\.(\d+)f$", RegexOptions.Compiled);

    /// <summary>
    /// 機器テーブル(<see cref="EquipmentTableEntry"/> / KIKITABLE)の代入文タグ値を予約語別に整形して
    /// 付属パラメータ(<see cref="AttachedParameters"/> / struct fparmg)へ格納する。
    /// 【C原典】mainfile_set の付属パラメータ設定ブロック(Fyss1f.c:1957-2205)。
    ///
    /// 負荷名称[0](fpaln[0])は <paramref name="effectiveLoadName"/> を使う。C 原典では予約語 "P"(盤)のとき
    /// 負荷名称を fp.fpaln[1] へ代用セットした後 DLN を '\0' クリアするため、その場合は空文字を渡す
    /// (fpaln[1] への代用セットは呼出元 MainAreaSet の epabn ブロックで実施する)。
    ///
    /// 【段階移植・未実装】コメント(行種対象)fpacm2/fpacglno(GCM/GCM_Group 依存)、
    /// 寸法グループ fpasglno(SP_Group/GSP 依存)、SP_GFlg による spkvn 上書き、fpaup/tikbn(当ブロック外)。
    /// これらは機器選定(Fyss13-15)/行種グループ状態が未モデル化のため既定値のままとする。
    /// </summary>
    /// <param name="sKiki">機器テーブルエントリ(【C原典】S_Kiki)。</param>
    /// <param name="fp">整形先の付属パラメータ(【C原典】mains->dt.fp)。</param>
    /// <param name="effectiveLoadName">負荷名称[0]に使う DLN(【C原典】"P" では空)。</param>
    public void FparmSet(EquipmentTableEntry sKiki, AttachedParameters fp, string effectiveLoadName)
    {
        ArgumentNullException.ThrowIfNull(sKiki);
        ArgumentNullException.ThrowIfNull(fp);

        // 負荷容量(fpalw1/fpalw2/fpalwkbn) ← DLW。【C原典】Fyss1f.c:1962-2000。
        string dlw = sKiki.LoadCapacity ?? string.Empty;
        if (!string.IsNullOrEmpty(dlw))
        {
            // 【C原典】p= &DLW[0]; if(DLW[0]=='K') p++;
            int p = (dlw[0] == 'K') ? 1 : 0;
            // 【C原典】p2 = strpbrk(p, "0123456789.") … 先頭の数字/ピリオド位置。
            int p2 = IndexOfNumeric(dlw, p);
            if (p2 >= 0)
            {
                // 【C原典】負荷種類(数字前の接頭部を "%.2s")。
                if (p2 != p)
                {
                    fp.LoadKind = TruncBytes(dlw[p..p2], 2);
                }
                // 【C原典】K = strpbrk(p, "K") != NULL(p は p2 位置)。負荷単位×1000 判定。
                bool k = dlw.IndexOf('K', p2) >= 0;
                // 【C原典】strncpy(fpaln[1], p, strlen(p)) … 負荷名称2を代用する。
                fp.LoadName[1] = TruncBytes(dlw[p2..], 20);
                // 【C原典】j = strspn(p, "0123456789.") … 先頭数字/ピリオド桁数。
                int j = SpanNumeric(dlw, p2);
                int remLen = dlw.Length - p2;
                if (j < remLen) // 数字部の後に非数字(単位)が続くとき負荷容量をセット。
                {
                    double f = AtofC(dlw.Substring(p2, j));
                    if (k) f *= 1000.0;
                    fp.LoadCapacity = SprintfF("%07.0f", f); // 【C原典】sprintf("%07.0f", f)。
                }
                // 【C原典】負荷単位区分 'V' 優先、無ければ 'W'。数字部の後方から探索。
                int unitFrom = (j < remLen) ? p2 + j : p2;
                if (dlw.IndexOf('V', unitFrom) >= 0)
                {
                    fp.LoadUnitKind = 'V';
                }
                else if (dlw.IndexOf('W', unitFrom) >= 0)
                {
                    fp.LoadUnitKind = 'W';
                }
            }
        }

        // 負荷電圧(fpalv[0]/fpalv[1]) ← DLV。【C原典】Fyss1f.c:2013-2029。
        if (!string.IsNullOrEmpty(sKiki.LoadVoltage[0]))
        {
            fp.LoadVoltage[0] = TruncBytes(sKiki.LoadVoltage[0], 3);
        }
        if (!string.IsNullOrEmpty(sKiki.LoadVoltage[1]))
        {
            fp.LoadVoltage[1] = TruncBytes(sKiki.LoadVoltage[1], 3);
        }

        // 負荷名称(fpaln[0]) ← DLN("P" 予約語では空)。【C原典】Fyss1f.c:2031-2033。
        fp.LoadName[0] = TruncBytes(effectiveLoadName ?? string.Empty, 20);

        // コメント(予約語対象)(fpacm1) ← DCM。【C原典】Fyss1f.c:2035-2036。
        fp.Comment = TruncBytes(sKiki.Comment, 20);

        // コメント(行種対象)fpacm2/fpacglno ← GCM/GCM_Group: 未モデル化のため未実装。

        // 品名(fpaitpt) ← DIT。【C原典】Fyss1f.c:2078-2081。
        fp.ItemName = TruncBytes(sKiki.ItemName, 25);

        // SP区分(spkvn) ← SP_Flg。【C原典】Fyss1f.c:2084-2087。SP_GFlg 上書きは未モデル化。
        fp.SpFutureMountKind = sKiki.SpecialFlag;

        // 寸法グループ fpasglno ← SP_Group: 未モデル化のため未実装。

        // 寸法(fpah/fpaw/fpad) ← DSP "縦*横*深"。【C原典】Fyss1f.c:2109-2138。
        string dsp = sKiki.SpecialDimension ?? string.Empty;
        if (dsp.Length > 0)
        {
            string[] dims = dsp.Split('*');
            fp.DimensionHeight = SprintfD04((int)AtofC(dims[0]));
            if (dims.Length >= 2)
            {
                fp.DimensionWidth = SprintfD04((int)AtofC(dims[1]));
            }
            if (dims.Length >= 3)
            {
                fp.DimensionDepth = SprintfD04((int)AtofC(dims[2]));
            }
        }

        // 括弧区分(fpag/fpahu/fpak/fpas/fpamh) ← Kakko1/Kakko2。【C原典】Fyss1f.c:2140-2160。
        short k1 = sKiki.Kakko1;
        short k2 = sKiki.Kakko2;
        if (k1 == 12 || k2 == 12) fp.ExternalMountKind = 'G'; // 外部取り付区分 G()
        if (k1 == 13 || k2 == 13) fp.SealKind = 'H';          // 封印区分 H()
        if (k1 == 14 || k2 == 14) fp.PartitionKind = 'K';     // 隔壁区分 K() 改訂<13>
        if (k1 == 15 || k2 == 15) fp.SuppliedKind = 'S';      // 支給品区分 S() 改訂<13>
        if (k1 == 16 || k2 == 16) fp.MeterSealKind = 'M';     // メータ封印区分 MH()

        // 制御電源番号(fpac) ← C_Flg=='1', C_No。【C原典】Fyss1f.c:2161-2164。
        if (sKiki.PowerSourceFlag == '1')
        {
            fp.ControlPowerNumber = ((int)sKiki.PowerSourceNumber).ToString("D2", CultureInfo.InvariantCulture);
        }

        // メーカーコード(fpamk) ← DMK。【C原典】Fyss1f.c:2167-2168。
        fp.MakerCode = TruncBytes(sKiki.Maker, 3);
    }

    /// <summary>C の <c>sprintf(buff, "%04d", n)</c> 相当。最小4桁ゼロ埋め、4桁超は上位4桁(strncpy 4)。</summary>
    private static string SprintfD04(int n)
    {
        string s = n.ToString("D4", CultureInfo.InvariantCulture);
        return s.Length > 4 ? s[..4] : s;
    }

    /// <summary>先頭 <paramref name="start"/> 以降で最初の数字/ピリオド位置。【C原典】strpbrk(p, "0123456789.")。</summary>
    private static int IndexOfNumeric(string s, int start)
    {
        for (int i = start; i < s.Length; i++)
        {
            if ((s[i] >= '0' && s[i] <= '9') || s[i] == '.')
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>先頭 <paramref name="start"/> 以降の連続する数字/ピリオドの桁数。【C原典】strspn(p, "0123456789.")。</summary>
    private static int SpanNumeric(string s, int start)
    {
        int i = start;
        while (i < s.Length && ((s[i] >= '0' && s[i] <= '9') || s[i] == '.'))
        {
            i++;
        }
        return i - start;
    }

    /// <summary>CP932 バイト幅で切り詰める(全角=2バイトの分断を回避)。【C原典】sprintf("%.Ns")+memcpy(strlen)。</summary>
    private static string TruncBytes(string? value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        byte[] b = FixedFieldCodec.ShiftJis.GetBytes(value);
        if (b.Length <= maxBytes)
        {
            return value;
        }

        int i = 0;
        int cut = 0;
        while (i < maxBytes)
        {
            bool lead = (b[i] >= 0x81 && b[i] <= 0x9F) || (b[i] >= 0xE0 && b[i] <= 0xFC);
            if (lead)
            {
                if (i + 2 > maxBytes)
                {
                    break;
                }
                i += 2;
            }
            else
            {
                i += 1;
            }
            cut = i;
        }
        return FixedFieldCodec.ShiftJis.GetString(b, 0, cut);
    }

    /// <summary>
    /// C の <c>atof()</c> 相当。先頭の数値部(符号・整数・小数)を実数化する。
    /// 【C原典】set_9/chk_9/Stof 内の atof。
    /// </summary>
    private static double AtofC(string s)
    {
        int i = 0;
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
        int start = i;
        if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
        if (i < s.Length && s[i] == '.')
        {
            i++;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
        }

        string num = s[start..i];
        return double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double f) ? f : 0.0;
    }
}
