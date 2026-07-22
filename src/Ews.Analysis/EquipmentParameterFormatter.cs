namespace Ews.Analysis;

using System.Globalization;
using System.Text.RegularExpressions;
using Ews.Domain.Analysis;

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
/// 本フェーズ(Wave 1~5)は遮断器系 MCB/ELB/MMCB/ELMB/SB、漏電遮断器系 RMCB/RELB/RMMCB/RELMB、
/// 引込 PS/P/UP、電磁接触器系 MC/THR/MG/SC、端子台・計器系 NT/WH/VM/AM/VT/CT/VS/AS を収録する。
/// TB/CON/TR(多スロット)や ZCT/LGR/… 等は後続 Wave で追加する。
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
