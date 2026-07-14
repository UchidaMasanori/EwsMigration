using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 電気パラメータ(定格キー)チェック(<see cref="ElectricalParameterChecker"/>)の検証。
/// 【C原典】toku/sekkei/src/Fyss1d.c Parm_Check_Main / Get_1_Group / Check_1_Group。
/// 本フェーズ(E.1)は型非依存パーサの構造検証(桁・記号・繰返し)を対象とする。
/// </summary>
public sealed class ElectricalParameterCheckerTests
{
    private static (short Rc, string Err) Check(string yoyaku, string parm)
    {
        var checker = new ElectricalParameterChecker();
        short rc = checker.CheckParameters(yoyaku, parm, out string err);
        return (rc, err);
    }

    // ── 正常系 ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("MCB", "3P225AF150AT")]   // 極数/AF/AT(整数のみ)
    [InlineData("MCB", "3P")]             // 単一記号
    [InlineData("ELB", "30MA")]           // MA(num=3 の繰返し記号を1個)
    [InlineData("MMCB", "12.34AT")]       // AT(len=5,d_len=2)小数あり
    [InlineData("MCB", "200V")]           // V(任意展開 flag=1)
    public void 構造的に正しい電気パラメータは正常終了する(string yoyaku, string parm)
    {
        (short rc, string err) = Check(yoyaku, parm);
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
    }

    // ── Get_1_Group 由来のエラー ─────────────────────────────────────

    [Fact]
    public void ピリオド2個以上はFY880E()
    {
        // 【C原典】Get_1_Group: piriod_skip > 1
        (short rc, string err) = Check("MCB", "2..5AF");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-880E", err);
    }

    [Fact]
    public void 区切り2個以上はFY881E()
    {
        // 【C原典】Get_1_Group: delimit_skip > 1
        (short rc, string err) = Check("MCB", "200･･210V");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-881E", err);
    }

    [Fact]
    public void 対象外文字はFY695E()
    {
        // 【C原典】Get_1_Group: keta_a+keta_p+keta_b+keta_d == 0(小文字等)
        (short rc, string err) = Check("MCB", "3p");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-695E", err);
    }

    [Fact]
    public void 数字部なしはFY696E()
    {
        // 【C原典】Get_1_Group: *keta1 == 0(記号先頭)
        (short rc, string err) = Check("MCB", "AF");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-696E", err);
    }

    // ── Check_1_Group 由来のエラー ───────────────────────────────────

    [Fact]
    public void テーブルにない記号はFY699E()
    {
        // 【C原典】Check_1_Group: p_tbl->symbol[0]=='\0'(未定義記号 ZZ)
        (short rc, string err) = Check("MCB", "3ZZ");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-699E", err);
    }

    [Fact]
    public void 整数部桁数overはFY882E()
    {
        // 【C原典】Check_1_Group: keta > len - d_len(AF len=4 に5桁)
        (short rc, string err) = Check("MCB", "22500AF");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-882E", err);
    }

    [Fact]
    public void 小数桁定義なし記号にピリオドありはFY883E()
    {
        // 【C原典】Check_1_Group: keta==1 && d_len==0(AF は d_len=0)
        (short rc, string err) = Check("MCB", "22.5AF");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-883E", err);
    }

    [Fact]
    public void 小数部桁数overはFY884E()
    {
        // 【C原典】Check_1_Group: keta > d_len(AT d_len=2 に3桁小数)
        (short rc, string err) = Check("MMCB", "12.345AT");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-884E", err);
    }

    [Fact]
    public void 繰返し数overはFY885E()
    {
        // 【C原典】Check_1_Group: p_tbl->num <= inum(V num=1 に2値)
        (short rc, string err) = Check("MCB", "200･210V");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-885E", err);
    }

    // ── 分岐(TR / 未収録予約語) ─────────────────────────────────────

    [Fact]
    public void TRは専用パーサTR_check_mainへ分岐する()
    {
        // 【C原典】Parm_Check_Main: strcmp(s_yoyaku,"TR")==0 → TR_check_main。
        // "3P" は一次相数のみで必須の V/VAC(flag2)が無いため ior1==0 → FY-889E。
        (short rc, string err) = Check("TR", "3P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-889E", err);
    }

    [Fact]
    public void 未収録予約語は構造検証をスキップする()
    {
        // 本フェーズ未収録の定格キー表はスキップ(後続フェーズで追加)。
        // PT は特殊展開(tkak_tbl flag 非0)のため引き続き未収録。
        (short rc, string err) = Check("PT", "3P");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
    }

    // ── 追加バッチ(残りの型)の構造検証 ─────────────────────────────

    [Theory]
    [InlineData("TB", "225A")]        // ft_tb: A(3,0,1,0)
    [InlineData("TB", "5.50SQ")]      // ft_tb: SQ(5,2,1,0) 小数2桁
    [InlineData("GL", "5P")]          // ft_gl: P(3,1,1,0)
    [InlineData("INV", "5.50KW")]     // ft_inv: KW(5,2,1,0)
    [InlineData("MV", "200VAC")]      // ft_mv: VAC(3,0,1,1)
    [InlineData("HSB", "225AF")]      // ft_hsb: AF(3,0,1,0)
    [InlineData("2ERY", "100AF")]     // ft_2ery: AF(5,2,1,0) 先頭数字予約語
    [InlineData("TSU", "10.50A")]     // ft_tsu: A(4,2,1,0)
    [InlineData("LGT", "225A")]       // ft_lgt: A(4,0,1,0)
    public void 追加した定格キー表の正常系は正常終了する(string yoyaku, string parm)
    {
        (short rc, string err) = Check(yoyaku, parm);
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
    }

    [Fact]
    public void 追加表の未定義記号はFY699E()
    {
        // ft_inv は KW/V/VAC/VC/VCAC/VCDC のみ。AT は未定義 → Check_1_Group FY-699E。
        (short rc, string err) = Check("INV", "5.5AT");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-699E", err);
    }

    [Fact]
    public void 空表の予約語は任意パラメータでFY699E()
    {
        // 【C原典】ft_vvvf は空表({"",0,0,0,0} のみ)。予約語自体は fyak_tbl に存在するが
        // 検証記号がないため、非空パラメータは Check_1_Group の記号不一致で FY-699E。
        (short rc, string err) = Check("VVVF", "5.5KW");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-699E", err);
    }

    [Fact]
    public void 追加表の小数桁定義なし記号ピリオドありはFY883E()
    {
        // ft_tb A(3,0,1,0) は d_len=0。ピリオド付与 → Check_1_Group FY-883E。
        (short rc, string err) = Check("TB", "22.5A");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-883E", err);
    }

    // ── CT/VT付き('/')表の構造検証 ──────────────────────

    [Theory]
    [InlineData("AM", "50/5A")]        // ft_am: "/"(4,0,1,1) + A(4,0,1,0)
    [InlineData("VT", "110/110VAC")]   // ft_vt: "/"(3,0,1,0) + VAC(3,0,1,0)
    [InlineData("CT", "1000/5A")]      // ft_ct: "/"(4,0,1,0) + A(3,0,1,0)
    [InlineData("RTR", "75/22VA")]     // ft_rtr: "/"(3,0,1,0) + VA(2,0,1,0)
    [InlineData("BLTR", "75/22VA")]    // ft_bltr: "/"(3,0,1,0) + VA(2,0,1,0)
    [InlineData("PLTR", "75/55VAC")]   // ft_pltr: "/"(3,0,1,0) + VAC(2,0,1,0)
    [InlineData("THSW", "3C/2C")]      // ft_thsw: "C/"(3,0,1,0) + C(3,0,1,0)
    [InlineData("WH", "1P100/5A50HZ")] // ft_wh: P + "/"(3,0,1,1) + A + HZ
    public void CT_VT付き表の正常系は正常終了する(string yoyaku, string parm)
    {
        (short rc, string err) = Check(yoyaku, parm);
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
    }

    [Fact]
    public void CTの一次電流桁数超過はFY882E()
    {
        // t_ct "/"(4,0,1,0): 一次値は4桁まで。5桁 → Check_1_Group FY-882E。
        (short rc, string err) = Check("CT", "10000/5A");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-882E", err);
    }

    [Fact]
    public void CT_VT付き表の未定義記号はFY699E()
    {
        // t_ct は "/"/A/VA のみ。V は未定義 → Check_1_Group FY-699E。
        (short rc, string err) = Check("CT", "1000/5V");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-699E", err);
    }

    // ── E.2: key_check 値格納・範囲検証 ─────────────────────────────

    private static (short Rc, RatingValues Values, string Err) CheckValues(string yoyaku, string parm)
    {
        var checker = new ElectricalParameterChecker();
        short rc = checker.CheckParameters(yoyaku, parm, out RatingValues values, out string err);
        return (rc, values, err);
    }

    [Fact]
    public void MCBの各記号値がkey_tblへ格納される()
    {
        // 【C原典】key_check_MCB: p/af/at フィールドへ memcpy
        (short rc, RatingValues values, string err) = CheckValues("MCB", "3P225AF150AT");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("3", values.Get("p"));
        Assert.Equal("225", values.Get("af"));
        Assert.Equal("150", values.Get("at"));
    }

    [Fact]
    public void MCBのVACは交流区分fvがAになる()
    {
        // 【C原典】key_check_MCB: VAC/V → key_tbl.mcb.fv='A'
        (short rc, RatingValues values, string err) = CheckValues("MCB", "200V");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("200", values.Get("v"));
        Assert.Equal("A", values.Get("fv"));
    }

    [Fact]
    public void MCのVDCは直流区分fvがDになる()
    {
        // 【C原典】key_check_MC: VDC → key_tbl.mc.fv='D'
        (short rc, RatingValues values, string err) = CheckValues("MC", "200VDC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("200", values.Get("v"));
        Assert.Equal("D", values.Get("fv"));
    }

    [Fact]
    public void 同一記号の重複登録は重複エラーになる()
    {
        // 【C原典】key_check_MCB: key_tbl.mcb.p[0]!='\0' → FY-890E
        (short rc, _, string err) = CheckValues("MCB", "3P4P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-890E", err);
    }

    [Fact]
    public void MCBのP範囲外はFY891E()
    {
        // 【C原典】key_check_MCB: i_val<1||i_val>4 → FY-891E
        (short rc, _, string err) = CheckValues("MCB", "5P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-891E", err);
    }

    [Fact]
    public void MCBのAT範囲外はFY800E()
    {
        // 【C原典】key_check_MCB: AT i_val<0||i_val>1200 → FY-800E
        (short rc, _, string err) = CheckValues("MCB", "1500AT");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-800E", err);
    }

    [Fact]
    public void THRのE離散値以外はFY893E()
    {
        // 【C原典】key_check_THR: E は 0/2/3 のみ許容 → 1 は FY-893E
        (short rc, _, string err) = CheckValues("THR", "1E");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-893E", err);
    }

    [Fact]
    public void THRのAT小数値が格納される()
    {
        // 【C原典】key_check_THR: AT f_val 0.01..500.00
        (short rc, RatingValues values, string err) = CheckValues("THR", "12.34AT");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("12.34", values.Get("at"));
    }

    [Fact]
    public void MCDTのVCACは補助電圧区分fvcがAになる()
    {
        // 【C原典】key_check_MCDT: VC/VCAC → key_tbl.mcdt.fvc='A'
        (short rc, RatingValues values, string err) = CheckValues("MCDT", "3P100A200V110VC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("3", values.Get("p"));
        Assert.Equal("100", values.Get("a"));
        Assert.Equal("200", values.Get("v"));
        Assert.Equal("A", values.Get("fv"));
        Assert.Equal("110", values.Get("vc"));
        Assert.Equal("A", values.Get("fvc"));
    }

    [Fact]
    public void MCDTのP範囲外はFY891E()
    {
        // 【C原典】key_check_MCDT: P i_val<2||i_val>4 → FY-891E(1P は範囲外)
        (short rc, _, string err) = CheckValues("MCDT", "1P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-891E", err);
    }

    [Fact]
    public void CSDTの各記号値が格納される()
    {
        // 【C原典】key_check_CSDT: p/a/v 格納 + fv='A'
        (short rc, RatingValues values, string err) = CheckValues("CSDT", "2P100A200VAC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("2", values.Get("p"));
        Assert.Equal("100", values.Get("a"));
        Assert.Equal("200", values.Get("v"));
        Assert.Equal("A", values.Get("fv"));
    }

    [Fact]
    public void SCのHZ離散値以外はFY824E()
    {
        // 【C原典】key_check_SC: HZ は 50/60 のみ → 55 は FY-824E
        (short rc, _, string err) = CheckValues("SC", "55HZ");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-824E", err);
    }

    [Fact]
    public void 未収録型は構造検証のみで値は格納されない()
    {
        // MMCB は定格キー表収録済みだが key_check 未収録 → 構造検証のみ
        (short rc, RatingValues values, string err) = CheckValues("MMCB", "12.34AT");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Null(values.Get("at"));
    }

    // ── TR(変圧器)専用パーサ TR_check_main / key_check_TR ─────────────

    [Fact]
    public void TRの正常系は各スロットへ格納される()
    {
        // 【C原典】TR_check_main + key_check_TR: 一次 P1/W1、'/'→v1[0]、二次 V→fv2/v2[0]、VA→va。
        (short rc, RatingValues values, string err) = CheckValues("TR", "1P2W210/105V50VA");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("1", values.Get("p1"));
        Assert.Equal("2", values.Get("w1"));
        Assert.Equal("210", values.Get("v1[0]"));
        Assert.Equal("A", values.Get("fv2"));
        Assert.Equal("105", values.Get("v2[0]"));
        Assert.Equal("50", values.Get("va"));
    }

    [Fact]
    public void TRのKVAは1000倍してvaへ格納される()
    {
        // 【C原典】key_check_TR KVA: l_val = f_val * 1000 → va へ格納。
        (short rc, RatingValues values, string err) = CheckValues("TR", "3P4W420/210V100KVA");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100000", values.Get("va"));
    }

    [Fact]
    public void TRのP相数が1でも3でもない場合はFY890E()
    {
        // 【C原典】key_check_TR P: i_val!=1 && i_val!=3 → FY-890E。
        (short rc, _, string err) = CheckValues("TR", "2P2W210/105V50VA");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-890E", err);
    }

    [Fact]
    public void TRのft_tr未定義記号はFY699E()
    {
        // 【C原典】TR_check_main: ft_tr に記号なし(A は非対象)→ FY-699E。
        (short rc, _, string err) = CheckValues("TR", "1P2W210/105A");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-699E", err);
    }

    [Fact]
    public void TRでVまたはVACが無い場合はFY889E()
    {
        // 【C原典】TR_check_main: flag2(V/VAC)を1つも受理しない(ior1==0)→ FY-889E。
        (short rc, _, string err) = CheckValues("TR", "1P2W210/50VA");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-889E", err);
    }
}
