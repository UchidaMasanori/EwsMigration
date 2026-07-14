using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// “d‹Cƒpƒ‰ƒ[ƒ^(’èŠiƒL[)ƒ`ƒFƒbƒN(<see cref="ElectricalParameterChecker"/>)‚ÌŒŸØB
/// yCŒ´“Tztoku/sekkei/src/Fyss1d.c Parm_Check_Main / Get_1_Group / Check_1_GroupB
/// –{ƒtƒF[ƒY(E.1)‚ÍŒ^”ñˆË‘¶ƒp[ƒT‚Ì\‘¢ŒŸØ(Œ…E‹L†EŒJ•Ô‚µ)‚ğ‘ÎÛ‚Æ‚·‚éB
/// </summary>
public sealed class ElectricalParameterCheckerTests
{
    private static (short Rc, string Err) Check(string yoyaku, string parm)
    {
        var checker = new ElectricalParameterChecker();
        short rc = checker.CheckParameters(yoyaku, parm, out string err);
        return (rc, err);
    }

    // „Ÿ„Ÿ ³íŒn „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Theory]
    [InlineData("MCB", "3P225AF150AT")]   // ‹É”/AF/AT(®”‚Ì‚İ)
    [InlineData("MCB", "3P")]             // ’Pˆê‹L†
    [InlineData("ELB", "30MA")]           // MA(num=3 ‚ÌŒJ•Ô‚µ‹L†‚ğ1ŒÂ)
    [InlineData("MMCB", "12.34AT")]       // AT(len=5,d_len=2)¬”‚ ‚è
    [InlineData("MCB", "200V")]           // V(”CˆÓ“WŠJ flag=1)
    public void \‘¢“I‚É³‚µ‚¢“d‹Cƒpƒ‰ƒ[ƒ^‚Í³íI—¹‚·‚é(string yoyaku, string parm)
    {
        (short rc, string err) = Check(yoyaku, parm);
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
    }

    // „Ÿ„Ÿ Get_1_Group —R—ˆ‚ÌƒGƒ‰[ „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Fact]
    public void ƒsƒŠƒIƒh2ŒÂˆÈã‚ÍFY880E()
    {
        // yCŒ´“TzGet_1_Group: piriod_skip > 1
        (short rc, string err) = Check("MCB", "2..5AF");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-880E", err);
    }

    [Fact]
    public void ‹æØ‚è2ŒÂˆÈã‚ÍFY881E()
    {
        // yCŒ´“TzGet_1_Group: delimit_skip > 1
        (short rc, string err) = Check("MCB", "200¥¥210V");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-881E", err);
    }

    [Fact]
    public void ‘ÎÛŠO•¶š‚ÍFY695E()
    {
        // yCŒ´“TzGet_1_Group: keta_a+keta_p+keta_b+keta_d == 0(¬•¶š“™)
        (short rc, string err) = Check("MCB", "3p");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-695E", err);
    }

    [Fact]
    public void ”š•”‚È‚µ‚ÍFY696E()
    {
        // yCŒ´“TzGet_1_Group: *keta1 == 0(‹L†æ“ª)
        (short rc, string err) = Check("MCB", "AF");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-696E", err);
    }

    // „Ÿ„Ÿ Check_1_Group —R—ˆ‚ÌƒGƒ‰[ „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Fact]
    public void ƒe[ƒuƒ‹‚É‚È‚¢‹L†‚ÍFY699E()
    {
        // yCŒ´“TzCheck_1_Group: p_tbl->symbol[0]=='\0'(–¢’è‹`‹L† ZZ)
        (short rc, string err) = Check("MCB", "3ZZ");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-699E", err);
    }

    [Fact]
    public void ®”•”Œ…”over‚ÍFY882E()
    {
        // yCŒ´“TzCheck_1_Group: keta > len - d_len(AF len=4 ‚É5Œ…)
        (short rc, string err) = Check("MCB", "22500AF");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-882E", err);
    }

    [Fact]
    public void ¬”Œ…’è‹`‚È‚µ‹L†‚ÉƒsƒŠƒIƒh‚ ‚è‚ÍFY883E()
    {
        // yCŒ´“TzCheck_1_Group: keta==1 && d_len==0(AF ‚Í d_len=0)
        (short rc, string err) = Check("MCB", "22.5AF");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-883E", err);
    }

    [Fact]
    public void ¬”•”Œ…”over‚ÍFY884E()
    {
        // yCŒ´“TzCheck_1_Group: keta > d_len(AT d_len=2 ‚É3Œ…¬”)
        (short rc, string err) = Check("MMCB", "12.345AT");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-884E", err);
    }

    [Fact]
    public void ŒJ•Ô‚µ”over‚ÍFY885E()
    {
        // yCŒ´“TzCheck_1_Group: p_tbl->num <= inum(V num=1 ‚É2’l)
        (short rc, string err) = Check("MCB", "200¥210V");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-885E", err);
    }

    // „Ÿ„Ÿ •ªŠò(TR / –¢û˜^—\–ñŒê) „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Fact]
    public void TR‚Íê—pƒp[ƒTTR_check_main‚Ö•ªŠò‚·‚é()
    {
        // yCŒ´“TzParm_Check_Main: strcmp(s_yoyaku,"TR")==0 ¨ TR_check_mainB
        // "3P" ‚ÍˆêŸ‘Š”‚Ì‚İ‚Å•K{‚Ì V/VAC(flag2)‚ª–³‚¢‚½‚ß ior1==0 ¨ FY-889EB
        (short rc, string err) = Check("TR", "3P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-889E", err);
    }

    [Fact]
    public void –¢û˜^—\–ñŒê‚Í\‘¢ŒŸØ‚ğƒXƒLƒbƒv‚·‚é()
    {
        // –{ƒtƒF[ƒY–¢û˜^‚Ì’èŠiƒL[•\‚ÍƒXƒLƒbƒv(Œã‘±ƒtƒF[ƒY‚Å’Ç‰Á)B
        // PT ‚Í“Áê“WŠJ(tkak_tbl flag ”ñ0)‚Ì‚½‚ßˆø‚«‘±‚«–¢û˜^B
        (short rc, string err) = Check("PT", "3P");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
    }

    // „Ÿ„Ÿ ’Ç‰Áƒoƒbƒ`(c‚è‚ÌŒ^)‚Ì\‘¢ŒŸØ „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Theory]
    [InlineData("TB", "225A")]        // ft_tb: A(3,0,1,0)
    [InlineData("TB", "5.50SQ")]      // ft_tb: SQ(5,2,1,0) ¬”2Œ…
    [InlineData("GL", "5P")]          // ft_gl: P(3,1,1,0)
    [InlineData("INV", "5.50KW")]     // ft_inv: KW(5,2,1,0)
    [InlineData("MV", "200VAC")]      // ft_mv: VAC(3,0,1,1)
    [InlineData("HSB", "225AF")]      // ft_hsb: AF(3,0,1,0)
    [InlineData("2ERY", "100AF")]     // ft_2ery: AF(5,2,1,0) æ“ª”š—\–ñŒê
    [InlineData("TSU", "10.50A")]     // ft_tsu: A(4,2,1,0)
    [InlineData("LGT", "225A")]       // ft_lgt: A(4,0,1,0)
    public void ’Ç‰Á‚µ‚½’èŠiƒL[•\‚Ì³íŒn‚Í³íI—¹‚·‚é(string yoyaku, string parm)
    {
        (short rc, string err) = Check(yoyaku, parm);
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
    }

    [Fact]
    public void ’Ç‰Á•\‚Ì–¢’è‹`‹L†‚ÍFY699E()
    {
        // ft_inv ‚Í KW/V/VAC/VC/VCAC/VCDC ‚Ì‚İBAT ‚Í–¢’è‹` ¨ Check_1_Group FY-699EB
        (short rc, string err) = Check("INV", "5.5AT");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-699E", err);
    }

    [Fact]
    public void ‹ó•\‚Ì—\–ñŒê‚Í”CˆÓƒpƒ‰ƒ[ƒ^‚ÅFY699E()
    {
        // yCŒ´“Tzft_vvvf ‚Í‹ó•\({"",0,0,0,0} ‚Ì‚İ)B—\–ñŒê©‘Ì‚Í fyak_tbl ‚É‘¶İ‚·‚é‚ª
        // ŒŸØ‹L†‚ª‚È‚¢‚½‚ßA”ñ‹óƒpƒ‰ƒ[ƒ^‚Í Check_1_Group ‚Ì‹L†•sˆê’v‚Å FY-699EB
        (short rc, string err) = Check("VVVF", "5.5KW");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-699E", err);
    }

    [Fact]
    public void ’Ç‰Á•\‚Ì¬”Œ…’è‹`‚È‚µ‹L†ƒsƒŠƒIƒh‚ ‚è‚ÍFY883E()
    {
        // ft_tb A(3,0,1,0) ‚Í d_len=0BƒsƒŠƒIƒh•t—^ ¨ Check_1_Group FY-883EB
        (short rc, string err) = Check("TB", "22.5A");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-883E", err);
    }

    // „Ÿ„Ÿ CT/VT•t‚«('/')•\‚Ì\‘¢ŒŸØ „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Theory]
    [InlineData("AM", "50/5A")]        // ft_am: "/"(4,0,1,1) + A(4,0,1,0)
    [InlineData("VT", "110/110VAC")]   // ft_vt: "/"(3,0,1,0) + VAC(3,0,1,0)
    [InlineData("CT", "1000/5A")]      // ft_ct: "/"(4,0,1,0) + A(3,0,1,0)
    [InlineData("RTR", "200/22VA")]    // ft_rtr: "/"(3,0,1,0)—£U{100,200} + VA(2,0,1,0)
    [InlineData("BLTR", "75/22VA")]    // ft_bltr: "/"(3,0,1,0) + VA(2,0,1,0)
    [InlineData("PLTR", "75/40VAC")]   // ft_pltr: "/"(3,0,1,0)sv1..440 + VAC(2,0,1,0)v1.0..50.0
    [InlineData("THSW", "3C/2C")]      // ft_thsw: "C/"(3,0,1,0) + C(3,0,1,0)
    [InlineData("WH", "1P100/5A50HZ")] // ft_wh: P + "/"(3,0,1,1) + A + HZ
    public void CT_VT•t‚«•\‚Ì³íŒn‚Í³íI—¹‚·‚é(string yoyaku, string parm)
    {
        (short rc, string err) = Check(yoyaku, parm);
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
    }

    [Fact]
    public void CT‚ÌˆêŸ“d—¬Œ…”’´‰ß‚ÍFY882E()
    {
        // t_ct "/"(4,0,1,0): ˆêŸ’l‚Í4Œ…‚Ü‚ÅB5Œ… ¨ Check_1_Group FY-882EB
        (short rc, string err) = Check("CT", "10000/5A");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-882E", err);
    }

    [Fact]
    public void CT_VT•t‚«•\‚Ì–¢’è‹`‹L†‚ÍFY699E()
    {
        // t_ct ‚Í "/"/A/VA ‚Ì‚İBV ‚Í–¢’è‹` ¨ Check_1_Group FY-699EB
        (short rc, string err) = Check("CT", "1000/5V");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-699E", err);
    }

    // „Ÿ„Ÿ E.2: key_check ’lŠi”[E”ÍˆÍŒŸØ „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    private static (short Rc, RatingValues Values, string Err) CheckValues(string yoyaku, string parm)
    {
        var checker = new ElectricalParameterChecker();
        short rc = checker.CheckParameters(yoyaku, parm, out RatingValues values, out string err);
        return (rc, values, err);
    }

    [Fact]
    public void MCB‚ÌŠe‹L†’l‚ªkey_tbl‚ÖŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_MCB: p/af/at ƒtƒB[ƒ‹ƒh‚Ö memcpy
        (short rc, RatingValues values, string err) = CheckValues("MCB", "3P225AF150AT");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("3", values.Get("p"));
        Assert.Equal("225", values.Get("af"));
        Assert.Equal("150", values.Get("at"));
    }

    [Fact]
    public void MCB‚ÌVAC‚ÍŒğ—¬‹æ•ªfv‚ªA‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_MCB: VAC/V ¨ key_tbl.mcb.fv='A'
        (short rc, RatingValues values, string err) = CheckValues("MCB", "200V");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("200", values.Get("v"));
        Assert.Equal("A", values.Get("fv"));
    }

    [Fact]
    public void MC‚ÌVDC‚Í’¼—¬‹æ•ªfv‚ªD‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_MC: VDC ¨ key_tbl.mc.fv='D'
        (short rc, RatingValues values, string err) = CheckValues("MC", "200VDC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("200", values.Get("v"));
        Assert.Equal("D", values.Get("fv"));
    }

    [Fact]
    public void “¯ˆê‹L†‚Ìd•¡“o˜^‚Íd•¡ƒGƒ‰[‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_MCB: key_tbl.mcb.p[0]!='\0' ¨ FY-890E
        (short rc, _, string err) = CheckValues("MCB", "3P4P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-890E", err);
    }

    [Fact]
    public void MCB‚ÌP”ÍˆÍŠO‚ÍFY891E()
    {
        // yCŒ´“Tzkey_check_MCB: i_val<1||i_val>4 ¨ FY-891E
        (short rc, _, string err) = CheckValues("MCB", "5P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-891E", err);
    }

    [Fact]
    public void MCB‚ÌAT”ÍˆÍŠO‚ÍFY800E()
    {
        // yCŒ´“Tzkey_check_MCB: AT i_val<0||i_val>1200 ¨ FY-800E
        (short rc, _, string err) = CheckValues("MCB", "1500AT");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-800E", err);
    }

    [Fact]
    public void THR‚ÌE—£U’lˆÈŠO‚ÍFY893E()
    {
        // yCŒ´“Tzkey_check_THR: E ‚Í 0/2/3 ‚Ì‚İ‹–—e ¨ 1 ‚Í FY-893E
        (short rc, _, string err) = CheckValues("THR", "1E");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-893E", err);
    }

    [Fact]
    public void THR‚ÌAT¬”’l‚ªŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_THR: AT f_val 0.01..500.00
        (short rc, RatingValues values, string err) = CheckValues("THR", "12.34AT");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("12.34", values.Get("at"));
    }

    [Fact]
    public void MCDT‚ÌVCAC‚Í•â•“dˆ³‹æ•ªfvc‚ªA‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_MCDT: VC/VCAC ¨ key_tbl.mcdt.fvc='A'
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
    public void MCDT‚ÌP”ÍˆÍŠO‚ÍFY891E()
    {
        // yCŒ´“Tzkey_check_MCDT: P i_val<2||i_val>4 ¨ FY-891E(1P ‚Í”ÍˆÍŠO)
        (short rc, _, string err) = CheckValues("MCDT", "1P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-891E", err);
    }

    [Fact]
    public void CSDT‚ÌŠe‹L†’l‚ªŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_CSDT: p/a/v Ši”[ + fv='A'
        (short rc, RatingValues values, string err) = CheckValues("CSDT", "2P100A200VAC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("2", values.Get("p"));
        Assert.Equal("100", values.Get("a"));
        Assert.Equal("200", values.Get("v"));
        Assert.Equal("A", values.Get("fv"));
    }

    [Fact]
    public void SC‚ÌHZ—£U’lˆÈŠO‚ÍFY824E()
    {
        // yCŒ´“Tzkey_check_SC: HZ ‚Í 50/60 ‚Ì‚İ ¨ 55 ‚Í FY-824E
        (short rc, _, string err) = CheckValues("SC", "55HZ");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-824E", err);
    }

    [Fact]
    public void MMCB‚ÌAT¬”’l‚ªŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_MMCB: AT f_val 0.01..225.0 ¨ mmcb.at ‚ÖŠi”[B
        (short rc, RatingValues values, string err) = CheckValues("MMCB", "12.34AT");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("12.34", values.Get("at"));
    }

    // „Ÿ„Ÿ TR(•Ïˆ³Ší)ê—pƒp[ƒT TR_check_main / key_check_TR „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Fact]
    public void TR‚Ì³íŒn‚ÍŠeƒXƒƒbƒg‚ÖŠi”[‚³‚ê‚é()
    {
        // yCŒ´“TzTR_check_main + key_check_TR: ˆêŸ P1/W1A'/'¨v1[0]A“ñŸ V¨fv2/v2[0]AVA¨vaB
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
    public void TR‚ÌKVA‚Í1000”{‚µ‚Äva‚ÖŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_TR KVA: l_val = f_val * 1000 ¨ va ‚ÖŠi”[B
        (short rc, RatingValues values, string err) = CheckValues("TR", "3P4W420/210V100KVA");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100000", values.Get("va"));
    }

    [Fact]
    public void TR‚ÌP‘Š”‚ª1‚Å‚à3‚Å‚à‚È‚¢ê‡‚ÍFY890E()
    {
        // yCŒ´“Tzkey_check_TR P: i_val!=1 && i_val!=3 ¨ FY-890EB
        (short rc, _, string err) = CheckValues("TR", "2P2W210/105V50VA");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-890E", err);
    }

    [Fact]
    public void TR‚Ìft_tr–¢’è‹`‹L†‚ÍFY699E()
    {
        // yCŒ´“TzTR_check_main: ft_tr ‚É‹L†‚È‚µ(A ‚Í”ñ‘ÎÛ)¨ FY-699EB
        (short rc, _, string err) = CheckValues("TR", "1P2W210/105A");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-699E", err);
    }

    [Fact]
    public void TR‚ÅV‚Ü‚½‚ÍVAC‚ª–³‚¢ê‡‚ÍFY889E()
    {
        // yCŒ´“TzTR_check_main: flag2(V/VAC)‚ğ1‚Â‚àó—‚µ‚È‚¢(ior1==0)¨ FY-889EB
        (short rc, _, string err) = CheckValues("TR", "1P2W210/50VA");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-889E", err);
    }

    // „Ÿ„Ÿ ŒvŠíE’[q‘äŒn key_check(VM/AM/VT/CT/VS/AS/TB/CON) „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Fact]
    public void VM‚ÌVAC‚Æ“ñŸ“dˆ³‚ªŠeƒtƒB[ƒ‹ƒh‚ÖŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_VM: VAC/V ¨ vm.v(fv='A')A"/" ¨ vm.svB
        (short rc, RatingValues values, string err) = CheckValues("VM", "300/150V");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("150", values.Get("v"));
        Assert.Equal("A", values.Get("fv"));
        Assert.Equal("300", values.Get("sv"));
    }

    [Fact]
    public void VM‚ÌVDC‚Í’¼—¬‹æ•ªfv‚ªD‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_VM: VDC ¨ vm.fv='D'A”ÍˆÍ 1..150B
        (short rc, RatingValues values, string err) = CheckValues("VM", "100VDC");
        Assert.Equal(0, rc);
        Assert.Equal("100", values.Get("v"));
        Assert.Equal("D", values.Get("fv"));
    }

    [Fact]
    public void VM‚ÌVDC”ÍˆÍŠO‚ÍFY802E()
    {
        // yCŒ´“Tzkey_check_VM: VDC i_val>150 ¨ FY-802EB
        (short rc, _, string err) = CheckValues("VM", "200VDC");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-802E", err);
    }

    [Fact]
    public void AM‚ÌˆêŸ“d—¬‚Æ“ñŸ“d—¬‚ªŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_AM: A ¨ am.aA"/" ¨ am.saB
        (short rc, RatingValues values, string err) = CheckValues("AM", "100/5A");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("5", values.Get("a"));
        Assert.Equal("100", values.Get("sa"));
    }

    [Fact]
    public void VT‚ÌVA‚ªŠi”[‚³‚êV‚Í110’´‚ÅFY802E()
    {
        // yCŒ´“Tzkey_check_VT: "/" ¨ vt.sv(1..440)AV/VAC ¨ vt.v(1..110)AVA 1..500B
        (short rc, RatingValues values, string err) = CheckValues("VT", "440/110V50VA");
        Assert.Equal(0, rc);
        Assert.Equal("110", values.Get("v"));
        Assert.Equal("440", values.Get("sv"));
        Assert.Equal("50", values.Get("va"));

        (short rc2, _, string err2) = CheckValues("VT", "200V");
        Assert.Equal(-1, rc2);
        Assert.Equal("FY-802E", err2);
    }

    [Fact]
    public void CT‚ÌVA”ÍˆÍŠO‚ÍFY836E()
    {
        // yCŒ´“Tzkey_check_CT: VA i_val>40 ¨ FY-836EB
        (short rc, _, string err) = CheckValues("CT", "100/5A50VA");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-836E", err);
    }

    [Fact]
    public void VS‚ÌP‚Í1‚©3‚Ì‚İ‚ÅW‚Í3‚©4‚Ì‚İ()
    {
        // yCŒ´“Tzkey_check_VS: P¸{1,3}(FY-891E)AW¸{3,4}(FY-830E)B
        (short rc, RatingValues values, string err) = CheckValues("VS", "3P4W");
        Assert.Equal(0, rc);
        Assert.Equal("3", values.Get("p"));
        Assert.Equal("4", values.Get("w"));

        (short rc2, _, string err2) = CheckValues("VS", "2P");
        Assert.Equal(-1, rc2);
        Assert.Equal("FY-891E", err2);
    }

    [Fact]
    public void AS‚ÌW”ÍˆÍŠO‚ÍFY830E()
    {
        // yCŒ´“Tzkey_check_AS: W¸{3,4} ˆÈŠO ¨ FY-830EB
        (short rc, _, string err) = CheckValues("AS", "1P5W");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-830E", err);
    }

    [Fact]
    public void TB‚ÌSQ¬”’l‚ÆVDC‹æ•ª‚ªŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_TB: SQ f_val 0.01..400AVDC ¨ tb.fv='D'B
        (short rc, RatingValues values, string err) = CheckValues("TB", "3P100V5.50SQ");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("3", values.Get("p"));
        Assert.Equal("100", values.Get("v"));
        Assert.Equal("A", values.Get("fv"));
        Assert.Equal("5.50", values.Get("sq"));
    }

    [Fact]
    public void TB‚ÌP‰ºŒÀ‚ÍŠù’è2‚Å‚ ‚èFY891E()
    {
        // yCŒ´“Tzkey_check_TB: ‰ü’ù<6> Šù’è i_min=2B1P ‚Í”ÍˆÍŠO ¨ FY-891EB
        (short rc, _, string err) = CheckValues("TB", "1P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-891E", err);
    }

    [Fact]
    public void CON‚ÌP‚Í2‚©3‚Ì‚İ‚ÅVDC‚Í125’´‚ÅFY802E()
    {
        // yCŒ´“Tzkey_check_CON: P¸{2,3}AVDC 1..125B
        (short rc, RatingValues values, string err) = CheckValues("CON", "2P15A100V");
        Assert.Equal(0, rc);
        Assert.Equal("2", values.Get("p"));
        Assert.Equal("15", values.Get("a"));
        Assert.Equal("100", values.Get("v"));
        Assert.Equal("A", values.Get("fv"));

        (short rc2, _, string err2) = CheckValues("CON", "2P200VDC");
        Assert.Equal(-1, rc2);
        Assert.Equal("FY-802E", err2);
    }

    // „Ÿ„Ÿ ƒuƒŒ[ƒJŒn key_check(ELB/MMCB/ELMB/SB/RMCB/RELB/RMMCB/RELMB) „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Fact]
    public void ELB‚ÌŠe‹L†‚ªŠi”[‚³‚êMAƒXƒƒbƒg‚Ö“ü‚é()
    {
        // yCŒ´“Tzkey_check_ELB: P/E/AF/AT/V + MA ‚Í ma[inum] “YšB15 ‚Í‹–—e—£U’lB
        (short rc, RatingValues values, string err) = CheckValues("ELB", "3P225AF150A200V15MA");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("3", values.Get("p"));
        Assert.Equal("225", values.Get("af"));
        Assert.Equal("150", values.Get("at"));
        Assert.Equal("200", values.Get("v"));
        Assert.Equal("A", values.Get("fv"));
        Assert.Equal("15", values.Get("ma[0]"));
    }

    [Fact]
    public void ELB‚ÌMA—£U’lˆÈŠO‚ÍFY810E()
    {
        // yCŒ´“Tzkey_check_ELB: MA¸{15,30,100,200,500} ˆÈŠO ¨ FY-810EB
        (short rc, _, string err) = CheckValues("ELB", "3P225AF150A50MA");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-810E", err);
    }

    [Fact]
    public void MMCB‚ÌKW”ÍˆÍŠO‚ÍFY812E()
    {
        // yCŒ´“Tzkey_check_MMCB: KW f_val>110.0 ¨ FY-812EB
        (short rc, _, string err) = CheckValues("MMCB", "150.00KW");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-812E", err);
    }

    [Fact]
    public void SB‚ÌP‚Í2‚Ì‚İ‚Å‚ ‚èFY891E()
    {
        // yCŒ´“Tzkey_check_SB: P!=2 ¨ FY-891EB
        (short rc, _, string err) = CheckValues("SB", "3P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-891E", err);
    }

    [Fact]
    public void RMCB‚ÌVC‚Í•â•“dˆ³fvc‚ªA‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_RMCB: VCAC/VC ¨ rmcb.vc(fvc='A')A1..240B
        (short rc, RatingValues values, string err) = CheckValues("RMCB", "2P30AF20A100V200VC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("200", values.Get("vc"));
        Assert.Equal("A", values.Get("fvc"));
    }

    [Fact]
    public void RMMCB‚ÌAT¬””ÍˆÍŠO‚ÍFY800E()
    {
        // yCŒ´“Tzkey_check_RMMCB: AT f_val>40.0 ¨ FY-800EB
        (short rc, _, string err) = CheckValues("RMMCB", "2P30AF50.00A");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-800E", err);
    }

    [Fact]
    public void RELMB‚ÌKW‚ªŠi”[‚³‚êMA³íŒn‚Í³íI—¹‚·‚é()
    {
        // yCŒ´“Tzkey_check_RELMB: KW 0.01..999AMA=ma[inum]B
        (short rc, RatingValues values, string err) = CheckValues("RELMB", "2P30AF20.00A5.50KW200V15MA");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("5.50", values.Get("kw"));
    }

    // „Ÿ„Ÿ •Ï—¬ŠíEƒŠƒŒ[Œn key_check(ZCT/LGR/ELR/HPSB/HSB/RRY/RTR) „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Fact]
    public void ZCT‚ÌŠe‹L†‚ªŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_ZCT: P 1..100AA 1..800AV/VAC 1..600(fv='A')B
        (short rc, RatingValues values, string err) = CheckValues("ZCT", "50P400A200V");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("50", values.Get("p"));
        Assert.Equal("400", values.Get("a"));
        Assert.Equal("200", values.Get("v"));
        Assert.Equal("A", values.Get("fv"));
    }

    [Fact]
    public void LGR‚ÌMA—£U’l‚ÆK‚ªŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_LGR: MA¸{50,100,200,400,500,800,1000}AK 1..10B
        (short rc, RatingValues values, string err) = CheckValues("LGR", "100MA5K200VC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100", values.Get("ma[0]"));
        Assert.Equal("5", values.Get("k"));
        Assert.Equal("200", values.Get("vc"));
        Assert.Equal("A", values.Get("fvc"));
    }

    [Fact]
    public void ELR‚ÌMA—£U’lˆÈŠO‚ÍFY810E()
    {
        // yCŒ´“Tzkey_check_ELR: MA¸{30,100,200,500} ˆÈŠO ¨ FY-810EB
        (short rc, _, string err) = CheckValues("ELR", "60MA");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-810E", err);
    }

    [Fact]
    public void HPSB‚ÌAM”ÍˆÍŠO‚ÍFY844E()
    {
        // yCŒ´“Tzkey_check_HPSB: AM 5..200 ‚Ì”ÍˆÍŠO ¨ FY-844EB
        (short rc, _, string err) = CheckValues("HPSB", "3P100AF50AT300AM");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-844E", err);
    }

    [Fact]
    public void HSB‚ÌVDC‚Í’¼—¬‹æ•ªfv‚ªD‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_HSB: VDC ¨ hsb.fv='D'A”ÍˆÍ 1..999B
        (short rc, RatingValues values, string err) = CheckValues("HSB", "3P100AF50AT500VDC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("500", values.Get("v"));
        Assert.Equal("D", values.Get("fv"));
    }

    [Fact]
    public void RRY‚ÌP‚Í1‚©2‚Ì‚İ‚Å‚ ‚èFY891E()
    {
        // yCŒ´“Tzkey_check_RRY: P¸{1,2} ˆÈŠO ¨ FY-891EB
        (short rc, _, string err) = CheckValues("RRY", "3P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-891E", err);
    }

    [Fact]
    public void RTR‚ÌV‚Í—£U’lˆÈŠO‚ÅFY802E()
    {
        // yCŒ´“Tzkey_check_RTR: V¸{24,100,200} ˆÈŠO ¨ FY-802EB
        (short rc, _, string err) = CheckValues("RTR", "150V");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-802E", err);
    }

    // „Ÿ„Ÿ ƒqƒ…[ƒYE“dŒ¹EƒŠƒŒ[Eƒ^ƒCƒ}Œn key_check(F/LA/DCPW/CR/TM/TS) „Ÿ„Ÿ„Ÿ„Ÿ

    [Fact]
    public void F‚ÌVDC‚Í’¼—¬‹æ•ªfv‚ªD‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_F: VDC ¨ f.fv='D'AV ‚Æ“¯ˆêƒtƒB[ƒ‹ƒh vB
        (short rc, RatingValues values, string err) = CheckValues("F", "30A100VDC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("30", values.Get("a"));
        Assert.Equal("100", values.Get("v"));
        Assert.Equal("D", values.Get("fv"));
    }

    [Fact]
    public void LA‚ÌP‚Í1‚©3‚Ì‚İ‚Å‚ ‚èFY891E()
    {
        // yCŒ´“Tzkey_check_LA: P¸{1,3} ˆÈŠO ¨ FY-891EB
        (short rc, _, string err) = CheckValues("LA", "2P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-891E", err);
    }

    [Fact]
    public void DCPW‚ÌVDC‚Í“Æ—§ƒtƒB[ƒ‹ƒh‚ÉŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_DCPW: V¨v(1..240)AVDC¨vdc(1..30, fvdc='D')B
        (short rc, RatingValues values, string err) = CheckValues("DCPW", "10.00A50.0W100V24VDC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100", values.Get("v"));
        Assert.Equal("24", values.Get("vdc"));
        Assert.Equal("D", values.Get("fvdc"));
    }

    [Fact]
    public void CR‚ÌAC•â•Ú“_‚Í”ÍˆÍŠO‚ÅFY818E()
    {
        // yCŒ´“Tzkey_check_CR: AC 1..9 ‚Ì”ÍˆÍŠO(0)¨ FY-818EB
        (short rc, _, string err) = CheckValues("CR", "0.50A0AC");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-818E", err);
    }

    [Fact]
    public void TM‚ÌSSET‚Íİ’èí•Ênset‚ª1‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_TM: SSET ¨ tm.set Ši”[ & nset='1'B
        (short rc, RatingValues values, string err) = CheckValues("TM", "10.00A1.500SSET");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("1.500", values.Get("set"));
        Assert.Equal("1", values.Get("nset"));
    }

    [Fact]
    public void TM‚ÌAC”ÍˆÍŠO‚ÍFY818E()
    {
        // yCŒ´“Tzkey_check_TM: AC 1..9 ‚Ì”ÍˆÍŠO(0)¨ FY-818EB
        (short rc, _, string err) = CheckValues("TM", "10.00A0AC");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-818E", err);
    }

    [Fact]
    public void TS‚ÌVDC‚Í’¼—¬‹æ•ªfv‚ªD‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_TS: VDC ¨ ts.fv='D'A”ÍˆÍ 1..125B
        (short rc, RatingValues values, string err) = CheckValues("TS", "10.00A100VDC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100", values.Get("v"));
        Assert.Equal("D", values.Get("fv"));
    }

    // „Ÿ„Ÿ •\¦“”EƒXƒCƒbƒ`EƒuƒU[Œn key_check(GX/XL/COS/PBS/SSW/TSW/BZ/BEL/CP/RSW/EE/HM/XERY/CKS) „Ÿ„Ÿ

    [Fact]
    public void GX‚Í—\–ñŒêG‚Å‹¤—L‚³‚êVC‚ªŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_GX(—\–ñŒê G/G1/c/GPN ‹¤—L): VC 1..260Afvc='A'B
        (short rc, RatingValues values, string err) = CheckValues("G", "100VC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100", values.Get("vc"));
        Assert.Equal("A", values.Get("fvc"));
    }

    [Fact]
    public void XL‚Í—\–ñŒêGL‚Å‹¤—L‚³‚êVDC‚ªfvD‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_XL(—\–ñŒê GL/RL/OL/BL/WL ‹¤—L): VDC ¨ fv='D'A”ÍˆÍ 1.0..125.0B
        (short rc, RatingValues values, string err) = CheckValues("GL", "100VDC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100", values.Get("v"));
        Assert.Equal("D", values.Get("fv"));
    }

    [Fact]
    public void BZ‚ÌW‚Íwva‚ÖŠi”[‚³‚êfwva‚ªW‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_BZ: W/VA ¨ “¯ˆê wvaAfwva='W'/'V'B
        (short rc, RatingValues values, string err) = CheckValues("BZ", "1.00W");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("1.00", values.Get("wva"));
        Assert.Equal("W", values.Get("fwva"));
    }

    [Fact]
    public void CP‚ÌAF‚Í30ˆÈŠO‚ÅFY895E()
    {
        // yCŒ´“Tzkey_check_CP: AF ‚Í 30 ŒÅ’è ¨ 40 ‚Í FY-895EB
        (short rc, _, string err) = CheckValues("CP", "40AF");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-895E", err);
    }

    [Fact]
    public void CKS‚ÌE‚Í0‚©2‚©3ˆÈŠO‚ÅFY893E()
    {
        // yCŒ´“Tzkey_check_CKS: E¸{0,2,3} ˆÈŠO ¨ FY-893EB
        (short rc, _, string err) = CheckValues("CKS", "2P1E");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-893E", err);
    }

    [Fact]
    public void HM‚ÌHZ‚Í50‚©60ˆÈŠO‚ÅFY824E()
    {
        // yCŒ´“Tzkey_check_HM: HZ¸{50,60} ˆÈŠO ¨ FY-824EB
        (short rc, _, string err) = CheckValues("HM", "55HZ");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-824E", err);
    }

    [Fact]
    public void XERY‚Í—\–ñŒê2ERY‚Å‹¤—L‚³‚êVC”ÍˆÍŠO‚ÍFY814E()
    {
        // yCŒ´“Tzkey_check_XERY(—\–ñŒê 2ERY/3ERY/4ERY ‹¤—L): VC 1..500 ‚Ì”ÍˆÍŠO ¨ FY-814EB
        (short rc, _, string err) = CheckValues("2ERY", "600VC");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-814E", err);
    }

    [Fact]
    public void RSW‚ÌK‚Í”ÍˆÍŠO‚ÅFY842E()
    {
        // yCŒ´“Tzkey_check_RSW: K 1..256 ‚Ì”ÍˆÍŠO ¨ FY-842EB
        (short rc, _, string err) = CheckValues("RSW", "300K");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-842E", err);
    }

    // „Ÿ„Ÿ ƒ†ƒjƒbƒgEÆ–¾E•Ïˆ³ŠíEƒXƒCƒbƒ`Œn key_check(Wave5) „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Fact]
    public void NHMB‚ÌAT‚ÆA‚Í“¯ˆêƒtƒB[ƒ‹ƒh‚ÉŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_NHMB: AT/A ¨ “¯ˆê at(0.01..99.99)BP 1..3B
        (short rc, RatingValues values, string err) = CheckValues("NHMB", "3P50.00A200V");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("50.00", values.Get("at"));
        Assert.Equal("200", values.Get("v"));
    }

    [Fact]
    public void SLX‚Í—\–ñŒêSL23‚Å‹¤—L‚³‚êVC‚ªŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_SLX(—\–ñŒê SL23/SL32/SL42/SL43 ‹¤—L): VC 1..240Afvc='A'B
        (short rc, RatingValues values, string err) = CheckValues("SL23", "100VC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100", values.Get("vc"));
        Assert.Equal("A", values.Get("fvc"));
    }

    [Fact]
    public void MV‚ÌW‚Íva‚ÖŠi”[‚³‚êfwva‚ªW‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_MV: VA/W ¨ “¯ˆê vaAfwva='V'/'W'B
        (short rc, RatingValues values, string err) = CheckValues("MV", "100W");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100", values.Get("va"));
        Assert.Equal("W", values.Get("fwva"));
    }

    [Fact]
    public void THSW‚ÌC‹L†‚ÍcsƒtƒB[ƒ‹ƒh‚ÖŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_THSW: C/¨csAC¨cACSET¨cset(‚¢‚¸‚ê‚à 1..999)B
        (short rc, RatingValues values, string err) = CheckValues("THSW", "3C/2C");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("3", values.Get("cs"));
        Assert.Equal("2", values.Get("c"));
    }

    [Fact]
    public void L‚Ì‹L†P‚Í1ˆÈŠO‚ÅFY891E()
    {
        // yCŒ´“Tzkey_check_L: P ‚Í 1 ŒÅ’è ¨ 2 ‚Í FY-891EB
        (short rc, _, string err) = CheckValues("L", "2P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-891E", err);
    }

    [Fact]
    public void PLTR‚Ì“ñŸ“dˆ³sv‚Í1‚©‚ç440‚Ü‚Å‹–—e‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_PLTR: '/' ¨ sv 1..440(BLTR ‚Ì 1..240 ‚Æ‘Šˆá)B
        (short rc, RatingValues values, string err) = CheckValues("PLTR", "440/40VAC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("440", values.Get("sv"));
    }

    [Fact]
    public void KPRY‚ÌVCDC‚Í’¼—¬‹æ•ªfvc‚ªD‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_KPRY: VCDC ¨ fvc='D'A”ÍˆÍ 1..60B
        (short rc, RatingValues values, string err) = CheckValues("KPRY", "60VCDC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("60", values.Get("vc"));
        Assert.Equal("D", values.Get("fvc"));
    }

    [Fact]
    public void IDF‚ÌP‚Í”ÍˆÍŠO‚ÅFY891E()
    {
        // yCŒ´“Tzkey_check_IDF: P 1..999 ‚Ì”ÍˆÍŠO(0)¨ FY-891EB
        (short rc, _, string err) = CheckValues("IDF", "0P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-891E", err);
    }

    // „Ÿ„Ÿ ƒtƒB[ƒ_EƒCƒ“ƒo[ƒ^E’¼—¬“dŒ¹Œn key_check(Wave6) „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Fact]
    public void MCFR‚ÌA‚ÆKW‚ªŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_MCFR: A 0.01..800AKW 0.01..140AV 1..550B
        (short rc, RatingValues values, string err) = CheckValues("MCFR", "100.00A5.50KW200V");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100.00", values.Get("a"));
        Assert.Equal("5.50", values.Get("kw"));
        Assert.Equal("200", values.Get("v"));
    }

    [Fact]
    public void MGFR‚ÌE‚Í0‚©2‚©3ˆÈŠO‚ÅFY893E()
    {
        // yCŒ´“Tzkey_check_MGFR: E¸{0,2,3} ˆÈŠO ¨ FY-893EB
        (short rc, _, string err) = CheckValues("MGFR", "1E");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-893E", err);
    }

    [Fact]
    public void FLTX‚Í—\–ñŒêFLT1‚Å‹¤—L‚³‚êVCDC‚ÍfvcD‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_FLTX(—\–ñŒê FLT1/c/FLTI ‹¤—L): VCDC ¨ fvc='D'A”ÍˆÍ 1..125B
        (short rc, RatingValues values, string err) = CheckValues("FLT1", "100VCDC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100", values.Get("vc"));
        Assert.Equal("D", values.Get("fvc"));
    }

    [Fact]
    public void DCSIR‚ÌVDC‚Í“Æ—§ƒtƒB[ƒ‹ƒhvdc‚ÉŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_DCSIR: V¨v(1..440)AVDC¨vdc(1..50, fvdc='D')B
        (short rc, RatingValues values, string err) = CheckValues("DCSIR", "50.00A100.0W200V50VDC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("200", values.Get("v"));
        Assert.Equal("50", values.Get("vdc"));
        Assert.Equal("D", values.Get("fvdc"));
    }

    [Fact]
    public void DCNI‚ÌMAH‚ªŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_DCNI: MAH 1..99999B
        (short rc, RatingValues values, string err) = CheckValues("DCNI", "50.00A12345MAH");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("12345", values.Get("mah"));
    }

    [Fact]
    public void MGFRSD‚ÌAT‚ÆA‚Í“¯ˆêƒtƒB[ƒ‹ƒh‚Åd•¡‚ÍFY899E()
    {
        // yCŒ´“Tzkey_check_MGFRSD: AT/A ‚Í“¯ˆê aBd•¡“o˜^‚Í FY-899EB
        (short rc, _, string err) = CheckValues("MGFRSD", "100.00AT200.00A");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-899E", err);
    }

    [Fact]
    public void MCFRSD‚ÌKW‚ªŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_MCFRSD: KW 0.01..140AV 1..550B
        (short rc, RatingValues values, string err) = CheckValues("MCFRSD", "50.00A10.00KW200V");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("10.00", values.Get("kw"));
    }

    // „Ÿ„Ÿ ƒ†ƒjƒbƒg‰»ƒXƒCƒbƒ`Œn key_check(Wave7: TSU/SSWU/PBSU/COSU/2COSU/OLU) „Ÿ„Ÿ

    [Fact]
    public void TSU‚ÌVDC‚Í’¼—¬‹æ•ªfv‚ªD‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_TSU: VDC ¨ fv='D'AV/VDC ‚Í“¯ˆê v(1..999)B
        (short rc, RatingValues values, string err) = CheckValues("TSU", "10.50A200VDC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("200", values.Get("v"));
        Assert.Equal("D", values.Get("fv"));
    }

    [Fact]
    public void OLU‚ÌK‚Í”ÍˆÍŠO‚ÅFY842E()
    {
        // yCŒ´“Tzkey_check_OLU: K 1..99 ‚Ì”ÍˆÍŠO(0)¨ FY-842EB
        (short rc, _, string err) = CheckValues("OLU", "0K");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-842E", err);
    }

    [Fact]
    public void COSU‚ÌVCAC‚ÍŒğ—¬‹æ•ªfvc‚ªA‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_COSU: VCAC ¨ fvc='A'AVC/VCAC/VCDC ‚Í“¯ˆê vcB
        (short rc, RatingValues values, string err) = CheckValues("COSU", "100VCAC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100", values.Get("vc"));
        Assert.Equal("A", values.Get("fvc"));
    }

    // „Ÿ„Ÿ “Áêˆ— key_check(NT Šï”ŠÛ‚ß / WH •›‹L† n_kigo) „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    [Fact]
    public void NT‚ÌŠï”P‚ÍŠÛ‚ßã‚°‚ÄŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_NT(940822): P ‚ªŠï”‚Ì‚Æ‚« +1 ‚µ‚ÄŠi”[("59"¨"60")B
        (short rc, RatingValues values, string err) = CheckValues("NT", "59P");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("60", values.Get("p"));
    }

    [Fact]
    public void NT‚ÌP‚Í”ÍˆÍŠO‚ÅFY891E()
    {
        // yCŒ´“Tzkey_check_NT: P 4..60 ‚Ì”ÍˆÍŠO(3)¨ FY-891EB
        (short rc, _, string err) = CheckValues("NT", "3P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-891E", err);
    }

    [Fact]
    public void NT‚ÌVDC‚Í’¼—¬‹æ•ªfv‚ªD‚É‚È‚é()
    {
        // yCŒ´“Tzkey_check_NT: VDC ¨ fv='D'AV/VAC/VDC ‚Í“¯ˆê v(1..260)B
        (short rc, RatingValues values, string err) = CheckValues("NT", "10A200VDC");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("200", values.Get("v"));
        Assert.Equal("D", values.Get("fv"));
    }

    [Fact]
    public void WH‚Ì•›‹L†V‚ÅƒXƒ‰ƒbƒVƒ…‚Ísv‚ÖV‚Åv‚ÖŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_WH: '/' ‚Ì’¼Œã•›‹L†(n_kigo)‚ª 'V' ‚È‚ç“ñŸ“dˆ³ svB
        (short rc, RatingValues values, string err) = CheckValues("WH", "3P100/200V");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100", values.Get("sv"));
        Assert.Equal("200", values.Get("v"));
    }

    [Fact]
    public void WH‚Ì•›‹L†A‚ÅƒXƒ‰ƒbƒVƒ…‚Ísa‚ÖA‚Åa‚ÖŠi”[‚³‚ê‚é()
    {
        // yCŒ´“Tzkey_check_WH: '/' ‚Ì’¼Œã•›‹L†(n_kigo)‚ª 'A' ‚È‚ç“ñŸ“d—¬ saB
        (short rc, RatingValues values, string err) = CheckValues("WH", "1P100/5A");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
        Assert.Equal("100", values.Get("sa"));
        Assert.Equal("5", values.Get("a"));
    }

    [Fact]
    public void WH‚ÌP‚Í1‚©3ˆÈŠO‚ÅFY891E()
    {
        // yCŒ´“Tzkey_check_WH: P¸{1,3} ˆÈŠO ¨ FY-891EB
        (short rc, _, string err) = CheckValues("WH", "2P");
        Assert.Equal(-1, rc);
        Assert.Equal("FY-891E", err);
    }
}
