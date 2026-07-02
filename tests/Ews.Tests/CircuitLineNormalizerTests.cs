using Ews.Analysis;
using Ews.Domain.Circuits;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// sí•Ê‘Oˆ—(<see cref="CircuitLineNormalizer"/>)‚ÌŒŸØB
/// yCŒ´“Tztoku/qrespo/sekkei/fyskews/src/FyskEwsMain.c ‚Ì Fysk_* •ÏŠ·ŒQB
/// </summary>
public sealed class CircuitLineNormalizerTests
{
    private static CircuitDescriptionLine Line(string lineType, string text, int lineNumber = 0)
        => new()
        {
            LineType = lineType,
            CircuitText = text,
            LineNumber = lineNumber,
        };

    [Fact]
    public void CheckDuplicationComma_˜A‘±ƒRƒ“ƒ}‚ğ1‚Â‚Éô‚Ş()
    {
        // yCŒ´“TzFysk_CheckDuplicationComma
        var lines = new List<CircuitDescriptionLine> { Line("M", "AAA,,,BBB,,CCC") };
        CircuitLineNormalizer.CheckDuplicationComma(lines);
        Assert.Equal("AAA,BBB,CCC", lines[0].CircuitText);
    }

    [Fact]
    public void AddLightCircuitNt_“d“”‰ñ˜H‚ÌåŠ²‚É––”ö_NT_‚ğ•t—^‚·‚é()
    {
        // yCŒ´“TzFysk_LightCirCuitCheck
        var lines = new List<CircuitDescriptionLine>
        {
            Line("P", "AC100/200V 1P3W"),
            Line("M", "MCB 3P 500A"),
        };
        CircuitLineNormalizer.AddLightCircuitNt(lines);
        Assert.Equal("MCB 3P 500A+(NT)", lines[1].CircuitText);
    }

    [Fact]
    public void AddLightCircuitNt_Šù‘¶‚ÌŠÛŠ‡ŒÊ“à‚É_NT_‚ğ‘}“ü‚·‚é()
    {
        // yCŒ´“TzFysk_LightCirCuitCheck (‰ü’ù<8>)
        var lines = new List<CircuitDescriptionLine>
        {
            Line("P", "AC100/200V 1P3W"),
            Line("M", "MCB 3P 600A+(AL)"),
        };
        CircuitLineNormalizer.AddLightCircuitNt(lines);
        Assert.Equal("MCB 3P 600A+(AL+NT)", lines[1].CircuitText);
    }

    [Fact]
    public void AddLightCircuitNt_Šù‚ÉNT‚ª‚ ‚ê‚Î•t—^‚µ‚È‚¢()
    {
        var lines = new List<CircuitDescriptionLine>
        {
            Line("P", "AC100/200V 1P3W"),
            Line("M", "MCB 3P 500A+(NT)"),
        };
        CircuitLineNormalizer.AddLightCircuitNt(lines);
        Assert.Equal("MCB 3P 500A+(NT)", lines[1].CircuitText);
    }

    [Fact]
    public void CompleteMpDepth_––”öŠ‡ŒÊ‚É[‚³15‚ğ•âŠ®‚·‚é()
    {
        // yCŒ´“TzFysk_MPCHeck
        var lines = new List<CircuitDescriptionLine> { Line("MP", "(SP=100*200)") };
        CircuitLineNormalizer.CompleteMpDepth(lines);
        Assert.Equal("(SP=100*200*15)", lines[0].CircuitText);
    }

    [Fact]
    public void TransformAfAt_60AF_‚ğ_100AF_‚Ö•ÏŠ·‚·‚é()
    {
        // yCŒ´“TzFysk_TransAfAt
        var lines = new List<CircuitDescriptionLine> { Line("M", "MCB 3P 60AT 60AF") };
        CircuitLineNormalizer.TransformAfAt(lines);
        Assert.Equal("MCB 3P 60AT 100AF", lines[0].CircuitText);
    }

    [Fact]
    public void RemoveTwoEt_Cs‚Ì_2ET_‚ğíœ‚·‚é()
    {
        // yCŒ´“TzFysk_2ET_Check
        var lines = new List<CircuitDescriptionLine> { Line("C", "ABC+(2ET)DEF") };
        CircuitLineNormalizer.RemoveTwoEt(lines);
        Assert.Equal("ABCDEF", lines[0].CircuitText);
    }

    [Fact]
    public void MergeConsecutiveTm_˜A‘±TM‚ğƒRƒ“ƒ}Œ‹‡‚·‚é()
    {
        // yCŒ´“TzFysk_TM_Consecutive_Check
        var lines = new List<CircuitDescriptionLine>
        {
            Line("TM", "T1"),
            Line("TM", "T2"),
            Line("TM", "T3"),
            Line("M", "MM"),
        };
        CircuitLineNormalizer.MergeConsecutiveTm(lines);
        Assert.Equal(2, lines.Count);
        Assert.Equal("T1,T2,T3", lines[0].CircuitText);
        Assert.Equal("MM", lines[1].CircuitText);
    }

    [Fact]
    public void MergeConsecutiveSm_TM’¼Œã‚ÌSM‚ğ’¼‘Os‚ÖŒ‹‡‚·‚é()
    {
        // yCŒ´“TzFysk_SM_Consecutive_Check
        var lines = new List<CircuitDescriptionLine>
        {
            Line("TM", "T1"),
            Line("SM", "S1"),
        };
        CircuitLineNormalizer.MergeConsecutiveSm(lines);
        Assert.Single(lines);
        Assert.Equal("T1,S1", lines[0].CircuitText);
    }

    [Fact]
    public void ConvertWlF1a_’¼‘OPs‚É420V‚ª‚ ‚ê‚Î_F_ST_‚Ö•ÏŠ·‚·‚é()
    {
        // yCŒ´“TzFysk_WL_Consecutive_Check •ÒWƒpƒ^[ƒ“1
        var lines = new List<CircuitDescriptionLine>
        {
            Line("P", "AC420V"),
            Line("PM", "F1A 100"),
        };
        CircuitLineNormalizer.ConvertWlF1a(lines);
        Assert.Equal("F+(ST) 100", lines[1].CircuitText);
    }

    [Fact]
    public void ConvertWlF1a_’¼‘OTMs‚È‚ç_F_‚Ö•ÏŠ·‚·‚é()
    {
        // yCŒ´“TzFysk_WL_Consecutive_Check •ÒWƒpƒ^[ƒ“2
        var lines = new List<CircuitDescriptionLine>
        {
            Line("TM", "T1"),
            Line("PM", "F1A 100"),
        };
        CircuitLineNormalizer.ConvertWlF1a(lines);
        Assert.Equal("F 100", lines[1].CircuitText);
    }

    [Fact]
    public void ChangeTmToMWhenBoFollows_TM’¼‰º‚ªBO‚È‚çTM‚ğM‚Ö()
    {
        // yCŒ´“TzFysk_BO_below_TM_Check
        var lines = new List<CircuitDescriptionLine>
        {
            Line("TM", "T1"),
            Line("BO", "B1"),
        };
        CircuitLineNormalizer.ChangeTmToMWhenBoFollows(lines);
        Assert.Equal("M", lines[0].LineType);
    }

    [Fact]
    public void DeleteCommaBeforeParen_ŠÛŠ‡ŒÊ’¼‘O‚ÌƒRƒ“ƒ}‚ğíœ‚·‚é()
    {
        // yCŒ´“TzFysk_Delete_Comma
        var lines = new List<CircuitDescriptionLine> { Line("M", "ABC,(DEF)") };
        CircuitLineNormalizer.DeleteCommaBeforeParen(lines);
        Assert.Equal("ABC(DEF)", lines[0].CircuitText);
    }

    [Fact]
    public void ApplyLwToMgMc_síƒuƒ‰ƒ“ƒN‚ÌMG‚É’¼ã‚ÌLW‚ğ”½‰f‚·‚é()
    {
        // yCŒ´“TzFysk_Add_LWToMGMC
        var lines = new List<CircuitDescriptionLine>
        {
            Line("M", "MCB 3P(LW=100)"),
            Line("", "-MG"),
        };
        CircuitLineNormalizer.ApplyLwToMgMc(lines);
        Assert.Equal("-MG(LW=100)", lines[1].CircuitText);
    }

    [Fact]
    public void ChangeTmToMBetweenPAndSp_P‚ÆSP‚ÌŠÔ‚ÉM‚ª–³‚¯‚ê‚ÎTM‚ğM‚Ö()
    {
        // yCŒ´“TzFysk_Chg_TMtoM_BetweenPandSP
        var lines = new List<CircuitDescriptionLine>
        {
            Line("P", "P1"),
            Line("TM", "T1"),
            Line("SP", "SP1"),
        };
        CircuitLineNormalizer.ChangeTmToMBetweenPAndSp(lines);
        Assert.Equal("M", lines[1].LineType);
    }

    [Fact]
    public void ChangeTmToMBetweenPAndSp_ŠÔ‚ÉM‚ª‚ ‚ê‚ÎTM‚ğ•ÏX‚µ‚È‚¢()
    {
        var lines = new List<CircuitDescriptionLine>
        {
            Line("P", "P1"),
            Line("TM", "T1"),
            Line("M", "M1"),
            Line("SP", "SP1"),
        };
        CircuitLineNormalizer.ChangeTmToMBetweenPAndSp(lines);
        Assert.Equal("TM", lines[1].LineType);
    }

    [Fact]
    public void ChangeOToBoUnderM_M‰º‚ÌO‚ğBO‚Ö•ÏX‚·‚é()
    {
        // yCŒ´“TzFysk_Chg_OtoBO_UnderM
        var lines = new List<CircuitDescriptionLine>
        {
            Line("P", "P1"),
            Line("M", "M1"),
            Line("O", "O1"),
            Line("SP", "SP1"),
        };
        CircuitLineNormalizer.ChangeOToBoUnderM(lines);
        Assert.Equal("BO", lines[2].LineType);
    }
}
