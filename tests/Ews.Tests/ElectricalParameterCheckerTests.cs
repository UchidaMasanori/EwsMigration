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
    public void TRは専用パーサへ分岐し本フェーズはスキップ()
    {
        // 【C原典】Parm_Check_Main: strcmp(s_yoyaku,"TR")==0 → TR_check_main(E.2で移植)
        (short rc, string err) = Check("TR", "3P");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
    }

    [Fact]
    public void 未収録予約語は構造検証をスキップする()
    {
        // 本フェーズ未収録の定格キー表はスキップ(後続フェーズで追加)。
        // CT は CT/VT付き('/')表のため引き続き未収録。
        (short rc, string err) = Check("CT", "3P");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
    }

    // ── 追加バッチ(残りの型)の構造検証 ─────────────────────────────

    [Theory]
    [InlineData("TB", "225A")]        // t_tb: A(3,0,1,0)
    [InlineData("TB", "5.50SQ")]      // t_tb: SQ(5,2,1,0) 小数2桁
    [InlineData("GL", "5P")]          // t_gl: P(3,1,1,0)
    [InlineData("VVVF", "5.5KW")]     // t_vvvf: KW(4,2,1,0)
    [InlineData("MV", "200VAC")]      // t_mv: VAC(3,0,1,1)
    [InlineData("HSB", "225AF")]      // t_hsb: AF(3,0,1,0)
    [InlineData("C", "470UF")]        // t_c: UF(4,0,1,0)
    [InlineData("R", "1000O")]        // t_r: O(4,0,1,0)
    public void 追加した定格キー表の正常系は正常終了する(string yoyaku, string parm)
    {
        (short rc, string err) = Check(yoyaku, parm);
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
    }

    [Fact]
    public void 追加表の未定義記号はFY699E()
    {
        // t_vvvf は KW/VAC のみ。AT は未定義 → Check_1_Group FY-699E。
        (short rc, string err) = Check("VVVF", "5.5AT");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-699E", err);
    }

    [Fact]
    public void 追加表の小数桁定義なし記号ピリオドありはFY883E()
    {
        // t_stm S(3,0,2,0) は d_len=0。ピリオド付与 → Check_1_Group FY-883E。
        (short rc, string err) = Check("STM", "12.3S");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-883E", err);
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
}
