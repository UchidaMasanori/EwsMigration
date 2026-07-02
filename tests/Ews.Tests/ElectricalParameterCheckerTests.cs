using Ews.Analysis;
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
        // 本フェーズ未収録の定格キー表はスキップ(後続フェーズで追加)
        (short rc, string err) = Check("VVVF", "3P");
        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, err);
    }
}
