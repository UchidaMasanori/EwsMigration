using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Ews.Domain.Projects;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// Œn“•¶š—ñƒ`ƒFƒbƒN(<see cref="CircuitStringChecker"/>)‚ÌŒŸØB
/// yCŒ´“Tztoku/sekkei/src/Fyss11.c Fyss11_Mojiretu_Check / Fyss11_Check_MainB
/// </summary>
public sealed class CircuitStringCheckerTests
{
    private static CircuitDescriptionLine Line(string lineType, string text, int lineNumber, char command = ' ')
        => new()
        {
            LineType = lineType,
            CircuitText = text,
            LineNumber = lineNumber,
            Command = command,
        };

    private static CircuitParseResult Run(IEnumerable<CircuitDescriptionLine> lines)
    {
        var checker = new CircuitStringChecker();
        var project = new ProjectInfo();
        return checker.Check(project, project, lines.ToList());
    }

    [Fact]
    public void Check_Œn“‹N“_‚Ìsí‚Ì‚İKEITOU‚ğ¶¬‚·‚é()
    {
        // yCŒ´“Tzkei_chk_tbl(P/SP/MP/UP)‚ÉŠ®‘Sˆê’v‚µ‚½sí‚Ì‚İŒn“(KEITOU)‚ğ¶¬B
        // P ‚Æ SP ‚ÍŒn“‚ğ‹N‚±‚·‚ªA—\–ñŒê M ‚Í’¼‘O‚ÌŒn“‚É‘®‚µŒn“‚Í‘‚¦‚È‚¢B
        var result = Run(new[]
        {
            Line("P", "1P2W105V", 1),
            Line("M", "MCB", 2),
            Line("SP", "(10*20)", 3),
        });

        Assert.Equal(2, result.Systems.Count);   // P ‚Æ SP ‚Ì‚İ
        Assert.Equal(3, result.LineTypes.Count); // sí(GYOSYU)‚Í‘Ss
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("P", '1')]  // kei_chk_tbl[0] ¨ syu_tbl[0]
    [InlineData("SP", '2')] // kei_chk_tbl[1] ¨ syu_tbl[1]
    [InlineData("MP", '3')] // kei_chk_tbl[2] ¨ syu_tbl[2]
    [InlineData("UP", '4')] // kei_chk_tbl[3] ¨ syu_tbl[3]
    public void Check_kei_chk_tbl‚ªŒn“í•Ê‚ğŒˆ’è‚·‚é(string lineType, char expectedKind)
    {
        // yCŒ´“TzFyss11_Table_Set: syu = syu_tbl[i][0]; Kind = syuB
        var result = Run(new[] { Line(lineType, "X", 1) });

        Assert.Single(result.Systems);
        Assert.Equal(expectedKind, result.Systems[0].SystemKind);
    }

    [Fact]
    public void Check_síƒuƒ‰ƒ“ƒN‚ÌŒp‘±s‚ğ’¼‘Os‚ÖŒ‹‡‚·‚é()
    {
        // yCŒ´“Tzj!=0 ‚©‚Â NULLSTRING(tgyosyu) ¨ strcat(okairoar, tkairoar)
        var result = Run(new[]
        {
            Line("P", "1P2W105V", 1),
            Line("", "CV2SQ", 2),     // Œp‘±s
            Line("M", "ELB", 3),
        });

        // P ‚Æ M ‚ÌŒn“BŒp‘±s‚Í“Æ—§‚µ‚½Œn“‚É‚È‚ç‚È‚¢B
        Assert.Single(result.Systems); // Œn“‚ğ‹N‚±‚·‚Ì‚Í P ‚Ì‚İ(M ‚Í“¯ˆêŒn“)
        // P ‚Ìd—l•¶š—ñ‚ÉŒp‘±•ª‚ª˜AŒ‹‚³‚ê‚Ä‚¢‚éB
        Assert.Contains(result.Specs, s => s.Text.Contains("1P2W105V") && s.Text.Contains("CV2SQ"));
    }

    [Fact]
    public void Check_æ“ª‚ªŒp‘±s‚È‚çFY004EƒGƒ‰[()
    {
        // yCŒ´“Tzj==0 ‚©‚Â NULLSTRING(tgyosyu) ¨ Error_Proc("FY-004E")
        var result = Run(new[]
        {
            Line("", "100A", 1),
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-004E");
    }

    [Fact]
    public void Check_ENDs‚Å‰ğÍ‚ğ‘Å‚¿Ø‚é()
    {
        var result = Run(new[]
        {
            Line("P", "1P2W105V", 1),
            Line("END", "", 2),
            Line("M", "–³‹‚³‚ê‚é", 3),
        });

        Assert.Single(result.Systems);
    }

    [Fact]
    public void Check_ƒRƒƒ“ƒgsí‚ğƒXƒLƒbƒv‚·‚é()
    {
        // # @ \ CM % ‚ÍƒXƒLƒbƒvB
        var result = Run(new[]
        {
            Line("#", "ƒRƒƒ“ƒg", 1),
            Line("CM", "ƒRƒƒ“ƒg", 2),
            Line("P", "1P2W105V", 3),
        });

        Assert.Single(result.Systems);
        Assert.Equal("P", result.Systems[0].LineType);
    }

    [Fact]
    public void Check_íœƒRƒ}ƒ“ƒhs‚ğƒXƒLƒbƒv‚·‚é()
    {
        // yCŒ´“Tzcmd == 'D' ‚Í continueB
        var result = Run(new[]
        {
            Line("P", "íœ‘ÎÛ", 1, command: 'D'),
            Line("M", "MCB 2P", 2),
        });

        // íœ‚³‚ê‚½ P ‚ÍƒXƒLƒbƒv‚³‚êAc‚é M ‚ÍŒn“‚ğ‹N‚±‚³‚È‚¢B
        Assert.Empty(result.Systems);
        Assert.Contains(result.LineTypes, g => g.LineType == "M");
    }

    [Fact]
    public void Check_–¢’m‚Ìsí‚ÍFY605EƒGƒ‰[()
    {
        // yCŒ´“Tzdefault ¨ Error_Proc("FY-605E")
        var result = Run(new[]
        {
            Line("ZZ", "•s–¾", 1),
            Line("P", "MCB", 2),
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-605E");
    }

    [Fact]
    public void Check_“üü•¶P‚Í‹@Šíƒe[ƒuƒ‹‚É“WŠJ‚³‚ê‚é()
    {
        var result = Run(new[]
        {
            Line("P", "1P2W105V", 1),
            Line("M", "ELB", 2),
        });

        Assert.Contains(result.MainEquipment, k => k.LineType == "P" && k.Attributes.GetValueOrDefault("11") == "P");
    }

    // ==== Check_Kikimei ¨ Parm_Check_Main(“d‹Cƒpƒ‰ƒ[ƒ^¨’èŠi’l key_tbl Ši”[) ====

    [Fact]
    public void Check_“d‹Cƒpƒ‰ƒ[ƒ^‚ğ‰ğÍ‚µ’èŠi’l‚ğ‹@Ší‚ÖŠi”[‚·‚é()
    {
        // yCŒ´“TzCheck_Kikimei()¨Parm_Check_Main()¨kikitable_add("2", electron, &f811)B
        // '=' ‚ÌŒã‚ë(sym_EQUAL •ªŠò)‚ª“d‹Cƒpƒ‰ƒ[ƒ^(d_parm)‚Æ‚È‚éB
        var result = Run(new[]
        {
            Line("P", "1P2W105V", 1),
            Line("M", "MCB=3P225AF150AT", 2),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment, k => k.LineType == "M");
        Assert.Equal("MCB", kiki.ProductName);
        Assert.NotNull(kiki.RatingValues);
        Assert.Equal("3", kiki.RatingValues!.Get("p"));    // ‹É”
        Assert.Equal("225", kiki.RatingValues.Get("af"));  // ƒtƒŒ[ƒ€
        Assert.Equal("150", kiki.RatingValues.Get("at"));  // ƒgƒŠƒbƒv
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Check_“d‹Cƒpƒ‰ƒ[ƒ^–³‚µ‚Ì—\–ñŒê‚Í’èŠi’l‚ğŠi”[‚µ‚È‚¢()
    {
        // yCŒ´“TzNULLSTRING(d_parm) ¨ Parm_Check_Main ‚ğŒÄ‚Î‚È‚¢(key_tbl –¢İ’è)B
        var result = Run(new[]
        {
            Line("P", "1P2W105V", 1),
            Line("M", "MCB", 2),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment, k => k.LineType == "M");
        Assert.Null(kiki.RatingValues);
    }

    [Fact]
    public void Check_•s³‚È“d‹Cƒpƒ‰ƒ[ƒ^‚ÍƒGƒ‰[‚É‚È‚è’èŠi’l‚ğŠi”[‚µ‚È‚¢()
    {
        // yCŒ´“TzParm_Check_Main ‚ª -1 ‚ğ•Ô‚·‚Æ Check_Kikimei ‚ªˆÙí(ErrNo •Û)B
        // ƒsƒŠƒIƒh2ŒÂˆÈã ¨ FY-880E(Get_1_Group)B
        var result = Run(new[]
        {
            Line("P", "1P2W105V", 1),
            Line("M", "MCB=2..5AF", 2),
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-880E");
        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment, k => k.LineType == "M");
        Assert.Null(kiki.RatingValues);
    }


    [Theory]
    [InlineData("BUN", BanKind.Branch)]
    [InlineData("HIK", BanKind.Incoming)]
    [InlineData("HIKI", BanKind.Incoming)]
    [InlineData("HIKK", BanKind.Incoming)]
    [InlineData("KAI", BanKind.Switch)]
    [InlineData("SYU", BanKind.Main)]
    [InlineData("SHU", BanKind.Main)]
    [InlineData("SEI", BanKind.Control)]
    [InlineData("KEI", BanKind.Meter)]
    [InlineData("BOX", BanKind.Box)]
    [InlineData("NAI", BanKind.Internal)]
    public void CheckBN_”Õ–¼ÌƒL[ƒ[ƒh‚©‚çBanKind‚ğŠm’è‚·‚é(string keyword, BanKind expected)
    {
        // yCŒ´“TzFyss11_Check_BN ¨ Find_BNB
        var result = Run(new[]
        {
            Line("BN", keyword, 1),
            Line("M", "MCB", 2),
        });

        Assert.Equal(expected, result.CurrentBan);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CheckBN_”Õ–¼Ì–³‚µ‚Í•ªŠò”Õ‚É‚È‚é()
    {
        // yCŒ´“Tzfindban == ban_END ¨ return(ban_BUN)B
        var result = Run(new[]
        {
            Line("BN", "", 1),
            Line("M", "MCB", 2),
        });

        Assert.Equal(BanKind.Branch, result.CurrentBan);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CheckBN_•s³‚È”Õ–¼Ì‚ÍFY620EƒGƒ‰[()
    {
        // yCŒ´“Tz‚ ‚â‚µ‚°‚Èƒf[ƒ^ ¨ Error_Proc("FY-620E")B
        var result = Run(new[]
        {
            Line("BN", "XYZ", 1),
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-620E");
    }

    [Fact]
    public void CheckBN_”Õ–¼Ì‚ÌŒã‚É—]•ª‚Èƒf[ƒ^‚ª‚ ‚ê‚ÎFY611EƒGƒ‰[()
    {
        // yCŒ´“Tzfindend != ban_END ¨ Error_Proc("FY-611E")B
        var result = Run(new[]
        {
            Line("BN", "SYU XYZ", 1),
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-611E");
    }

    [Fact]
    public void CheckBN_Šm’è‚µ‚½”Õ‹æ•ª‚ªŒã‘±‹@Ší‚É“`”d‚·‚é()
    {
        var result = Run(new[]
        {
            Line("BN", "SYU", 1),
            Line("P", "1P2W105V", 2),
        });

        Assert.Contains(result.MainEquipment, k => k.LineType == "P" && k.Ban == BanKind.Main);
    }

    // ==== Fyss11_Mojiretu_Find(—\–ñŒê•¶‚Ì•ªŠò•ª‰ğ) ====

    [Fact]
    public void MojiretuFind_•ªŠòó‚¯‚Å•¡”‚Ì‹@Ší‚É“WŠJ‚·‚é()
    {
        // yCŒ´“Tz"MCB--ELB" ¨ Bun_No=1(MCB), Bun_No=2(ELB)B
        var result = Run(new[]
        {
            Line("M", "MCB--ELB", 1),
        });

        var reserved = result.MainEquipment.Where(k => k.LineType == "M").ToList();
        Assert.Equal(2, reserved.Count);
        Assert.Contains(reserved, k => k.StringSequence == 1 && k.CircuitText == "MCB");
        Assert.Contains(reserved, k => k.StringSequence == 2 && k.CircuitText == "ELB");
    }

    [Fact]
    public void MojiretuFind_’Pˆê•¶‚ÍBunNo1‚Å“WŠJ‚·‚é()
    {
        var result = Run(new[]
        {
            Line("M", "MCB", 1),
        });

        var reserved = result.MainEquipment.Where(k => k.LineType == "M").ToList();
        Assert.Single(reserved);
        Assert.Equal(1, reserved[0].StringSequence);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MojiretuFind_MˆÈŠO‚Ì‹ó•¶‚ÍFY623EƒGƒ‰[()
    {
        // yCŒ´“TzNULLSTRING(control) ‚©‚Â gyosyu != "M" ¨ Error_Proc("FY-623E")B
        var result = Run(new[]
        {
            Line("B", "", 1),
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-623E");
    }

    [Fact]
    public void MojiretuFind_Ms‚Ì‹ó•¶‚Í‹–—e‚³‚ê‚é()
    {
        // yCŒ´“Tzgyosyu == "M" ‚Ì‚Æ‚«‚Ì‚İ‹ó•¶‚ğ‹–—eB
        var result = Run(new[]
        {
            Line("M", "", 1),
        });

        Assert.DoesNotContain(result.Errors, e => e.ErrorCode == "FY-623E");
    }

    // ==== Mojiretu_Check –{‘Ì(—\–ñŒêÆ‡EKIKITABLE “WŠJ) ====

    [Fact]
    public void MojiretuCheck_ƒJƒ“ƒ}‹æØ‚è‚Ì—\–ñŒê‚ğŒÂ•Ê‚Ì‹@Ší‚É“WŠJ‚·‚é()
    {
        // yCŒ´“TzFind_Control ‚ªƒJƒ“ƒ}(sym_KANMA)‚Ü‚Å’Šo‚µA—\–ñŒê‚²‚Æ‚É kikitable_setB
        var result = Run(new[]
        {
            Line("M", "MCB,ELB,MC", 1),
        });

        var kiki = result.MainEquipment.Where(k => k.LineType == "M").ToList();
        Assert.Equal(3, kiki.Count);
        Assert.Contains(kiki, k => k.CircuitText == "MCB");
        Assert.Contains(kiki, k => k.CircuitText == "ELB");
        Assert.Contains(kiki, k => k.CircuitText == "MC");
    }

    [Fact]
    public void MojiretuCheck_—\–ñŒê‚Æ—\–ñŒê”Ô†‚ğ•ª‰ğ‚·‚é()
    {
        // yCŒ´“Tzkikitable_add("1", ...) ¨ Find_Alphabetto(—\–ñŒê) + Find_Bangou(—\–ñŒê”Ô†)B
        var result = Run(new[]
        {
            Line("M", "MCB3", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("MCB", kiki.ReservedWord);      // ‰pš•”
        Assert.Equal("03", kiki.ReservedWordNumber); // Œã‘±”’l(2Œ…)
        Assert.Equal(1, kiki.EquipmentNumber);       // D_No
    }

    [Fact]
    public void MojiretuCheck_ŒÅ’è—\–ñŒê‚Í‚»‚Ì‚Ü‚Ü—\–ñŒê‚É‚È‚é()
    {
        // yCŒ´“TzG1?G4/SL*/FLT* ‚Í yoyakugo ‚ğ‚»‚Ì‚Ü‚Ü yoyaku ‚ÖB
        var result = Run(new[]
        {
            Line("M", "FLT2", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("FLT2", kiki.ReservedWord);
        Assert.Equal(string.Empty, kiki.ReservedWordNumber);
    }

    [Fact]
    public void MojiretuCheck_•¡‡—\–ñŒê‚Ì•s³‚ÈÚ”öŒê‚ÍFY613EƒGƒ‰[()
    {
        // yCŒ´“TzŠ‡ŒÊƒOƒ‹[ƒvŒã‚ÌÚ”öŒê‚ª (LN=/(LW=/(BK=/(BKO= ˆÈŠO ¨ FY-613EB
        var result = Run(new[]
        {
            Line("M", "K(MCB)XYZ", 1),
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-613E");
    }

    [Fact]
    public void MojiretuCheck_Š‡ŒÊ‚ª•Â‚¶‚È‚¢•¡‡—\–ñŒê‚ÍFY617EƒGƒ‰[()
    {
        // yCŒ´“TzSelect_Control ‚ª”ñ•½t(kakko!=0) ¨ FY-617EB
        var result = Run(new[]
        {
            Line("M", "K(MCB", 1),
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-617E");
    }

    // ==== Kairo_Bunrui_Set / Kairo_Bangou_Set(sí‹æ•ªE‰ñ˜H”Ô†) ====

    [Theory]
    [InlineData("TM", 'M')]
    [InlineData("M", 'M')]
    [InlineData("SM", 'M')]
    [InlineData("S", 'S')]
    [InlineData("O", 'O')]
    [InlineData("B", ' ')]
    [InlineData("BO", 'B')]
    [InlineData("PM", ' ')]
    public void sí‹æ•ª_Kairo_Bunrui_Set‚ªG_kind‚ğŒˆ’è‚·‚é(string lineType, char expectedClass)
    {
        // yCŒ´“TzKairo_Bunrui_Set: TM/M/SM¨'M', S¨'S', O¨'O', BO¨'B', B/PM¨' ', ‘¼¨'P'B
        var result = Run(new[]
        {
            Line(lineType, "MCB", 1),
        });

        LineTypeTableEntry gyosyu = Assert.Single(result.LineTypes);
        Assert.Equal(expectedClass, gyosyu.CircuitClass);
    }

    [Fact]
    public void ‰ñ˜H”Ô†_“¯ˆê‹æ•ª‚Ì˜A”Ô‚ª3Œ…‚ÅÌ”Ô‚³‚ê‚é()
    {
        // yCŒ´“TzKairo_Bangou_Set: ‹æ•ª‚²‚Æ‚É 1 n‚Ü‚è‚Ì’Ê‚µ”Ô†‚ğŒã’uÌ”Ô‚µ "%03d" ‚ÅŠi”[B
        var result = Run(new[]
        {
            Line("M", "MCB", 1),
            Line("M", "ELB", 2),
            Line("M", "MC", 3),
        });

        var mains = result.LineTypes.Where(g => g.LineType == "M").ToList();
        Assert.Equal(3, mains.Count);
        Assert.Equal("001", mains[0].CircuitNumber);
        Assert.Equal("002", mains[1].CircuitNumber);
        Assert.Equal("003", mains[2].CircuitNumber);
    }

    [Fact]
    public void ‰ñ˜H”Ô†_PMs‚ÍÌ”Ô‚³‚ê‚¸000‚É‚È‚é()
    {
        // yCŒ´“TzKairo_Bangou_Set: ‹æ•ª ' ' ‚©‚Â "PM" ‚Í return(0)B
        var result = Run(new[]
        {
            Line("PM", "MCB", 1),
        });

        LineTypeTableEntry gyosyu = Assert.Single(result.LineTypes);
        Assert.Equal("000", gyosyu.CircuitNumber);
    }

    // ==== Fyss11_Check_P / PS / UP(’èŠi’l•ÒW f811) ====

    [Fact]
    public void CheckP_“dŒ¹‹Lq‚©‚ç‘Š”‚Æ’èŠiƒR[ƒh‚ğŠm’è‚·‚é()
    {
        // yCŒ´“Tzdengen_kijyutu_table Æ‡ ¨ KAIROSOU/KAIRODEN/KAIROSOUSEN + f811(P_F)B
        var result = Run(new[]
        {
            Line("P", "1P2W105V", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("P", kiki.Attributes.GetValueOrDefault("11"));
        Assert.Equal("12A  105", kiki.Attributes.GetValueOrDefault("f811.cp"));
        Assert.Equal('1', result.CircuitPhase);              // KAIROSOU
        Assert.Equal("1P2W", result.CircuitPhaseWires);      // KAIROSOUSEN(æ“ª4•¶š)
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CheckP_“düƒTƒCƒY‚Æc”‚Æ–{”‚ğ‰ğÍ‚·‚é()
    {
        // yCŒ´“Tz“düí—Ş(dkind)+ƒTƒCƒY(SQœ‹)+'-'c”(Cœ‹)+'*'–{”B
        var result = Run(new[]
        {
            Line("P", "1P2W105VCV2SQ-2C*2", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("CV", kiki.Attributes.GetValueOrDefault("LN"));   // “düí—Ş
        Assert.Equal("2", kiki.Attributes.GetValueOrDefault("f811.sq")); // “düƒTƒCƒY
        Assert.Equal("2", kiki.Attributes.GetValueOrDefault("f811.c"));  // c”
        Assert.Equal("2", kiki.Attributes.GetValueOrDefault("f811.k"));  // –{”
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CheckP_•s–¾‚È“dŒ¹‹Lq‚ÍFY650EƒGƒ‰[()
    {
        // yCŒ´“Tzdengen_kijyutu_table –¢ˆê’v ¨ FY-650EB
        var result = Run(new[]
        {
            Line("P", "9Z9W999V", 1),
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-650E");
    }

    [Fact]
    public void CheckP_ƒRƒ}ƒ“ƒhCM‚ğ‰ğÍ‚·‚é()
    {
        // yCŒ´“TzKAKKO_PROC: "(CM=xxx)" ¨ cmdatB
        var result = Run(new[]
        {
            Line("P", "1P2W105V(CM=TEST)", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("TEST", kiki.Attributes.GetValueOrDefault("CM"));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CheckUP_“dŒ¹í•Ê‚©‚ç’èŠiƒR[ƒh‚ğŠm’è‚·‚é()
    {
        // yCŒ´“Tzdengen_syu_table Æ‡ ¨ f811(UP_F.fv)B
        var result = Run(new[]
        {
            Line("UP", "AC100V", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("UP", kiki.Attributes.GetValueOrDefault("11"));
        Assert.Equal("A100", kiki.Attributes.GetValueOrDefault("f811.fv"));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CheckUP_•s–¾‚È“dŒ¹í•Ê‚ÍFY656EƒGƒ‰[()
    {
        // yCŒ´“Tzdengen_syu_table –¢ˆê’v ¨ FY-656EB
        var result = Run(new[]
        {
            Line("UP", "XX999V", 1),
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-656E");
    }

    [Fact]
    public void CheckPS_“dŒ¹‹Lq‚ÆƒRƒ}ƒ“ƒh‚ğ‰ğÍ‚·‚é()
    {
        // yCŒ´“TzFyss11_Check_PS: “dŒ¹‹Lq + "(CM=xxx)"B
        var result = Run(new[]
        {
            Line("PS", "1P2W105V(CM=A)", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("PS", kiki.Attributes.GetValueOrDefault("11"));
        Assert.Equal("A", kiki.Attributes.GetValueOrDefault("CM"));
        Assert.True(result.IsValid);
    }

    // ==== Check_KikiMeisyou / Yoyaku_Check_Main(—\–ñŒêƒ}ƒXƒ^Æ‡) ====

    [Fact]
    public void CheckKikiMeisyou_—\–ñŒêƒ}ƒXƒ^‚Éˆê’v‚·‚é•i–¼‚ğŠm’è‚·‚é()
    {
        // yCŒ´“TzFyss1c_Mojiretu_Check ¨ Yoyaku_Check_Main: fyak_tbl Æ‡ ¨ s_yoyaku(kikimei)B
        var result = Run(new[]
        {
            Line("M", "MCB", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("MCB", kiki.ProductName);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CheckKikiMeisyou_Ú”ö”š‚ğí‚Á‚Ä—\–ñŒê‚ğÆ‡‚·‚é()
    {
        // yCŒ´“TzYoyaku_Check_Main: æ“ª1•¶šŒã‚ÌÅ‰‚Ì”š‚Ü‚Å(Ú”ö”šíœ)‚ÅÆ‡B
        var result = Run(new[]
        {
            Line("M", "MCB3", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("MCB", kiki.ProductName);      // Æ‡Ï‚İ—\–ñŒê
        Assert.Equal("MCB", kiki.ReservedWord);     // ‰pš•”
        Assert.Equal("03", kiki.ReservedWordNumber); // —\–ñŒê”Ô†
    }

    [Fact]
    public void CheckKikiMeisyou_“ÁêƒL[‚ÍÚ”ö”š‚ğ•Û‚µ‚ÄÆ‡‚·‚é()
    {
        // yCŒ´“Tztokusyu_key(FLT1?FLT4/SL*/G1?G4)‚Í‘O•ûˆê’v‚Å’·‚³Šm’èB
        var result = Run(new[]
        {
            Line("M", "FLT2", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("FLT2", kiki.ProductName);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CheckKikiMeisyou_æ“ª”š•t‚«—\–ñŒê‚ğÆ‡‚·‚é()
    {
        // yCŒ´“TzYoyaku_Check_Main: æ“ª1•¶š‚ğ”ò‚Î‚·‚½‚ß "2ERY" ‚Í‘S’·Æ‡B
        var result = Run(new[]
        {
            Line("M", "2ERY", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("2ERY", kiki.ProductName);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CheckKikiMeisyou_æ“ª”š—\–ñŒê‚Ì“d‹Cƒpƒ‰ƒ[ƒ^‚ğ•ª—£‚µ‚ÄŒŸØ‚·‚é()
    {
        // yCŒ´“TzCheck_KikimeiC else •ªŠò(”šn‚Ü‚è): æ“ª”š{‘±‚­‰pš‚ğ—\–ñŒêA
        // ˆÈ~‚Ì”š•”‚ğ electron ‚É•ª—£B"2ERY100AF" ¨ —\–ñŒê 2ERY / d_parm 100AFB
        var result = Run(new[]
        {
            Line("M", "2ERY100AF", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("2ERY", kiki.ProductName);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CheckKikiMeisyou_‰pš—\–ñŒê‚Ì”šn‚Ü‚è“d‹Cƒpƒ‰ƒ[ƒ^‚ğ•Û‚·‚é()
    {
        // yCŒ´“TzCheck_KikimeiC else •ªŠò(‰pšn‚Ü‚è): Å‰‚Ì”š‚Ü‚Å‚ª—\–ñŒê‚ÅA
        // ”šˆÈ~‚Í electronB"MCB3P" ¨ —\–ñŒê MCB / d_parm 3P(æ“ª‚Ì '3' ‚ğŒ‡—‚³‚¹‚È‚¢)B
        var result = Run(new[]
        {
            Line("M", "MCB3P", 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("MCB", kiki.ProductName);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CheckKikiMeisyou_ƒ}ƒXƒ^‚É–³‚¢—\–ñŒê‚ÍFY879EƒGƒ‰[()
    {
        // yCŒ´“TzYoyaku_Check_Main: fyak_tbl –¢ˆê’v ¨ FY-879EB
        var result = Run(new[]
        {
            Line("M", "XYZ", 1),
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-879E");
    }

    [Theory]
    [InlineData("27A")]
    [InlineData("27B")]
    [InlineData("27C")]
    public void CheckKikiMeisyou_27Œn—\–ñŒê‚ÍCR‚Æ‚µ‚Äˆµ‚¤(string control)
    {
        // yCŒ´“Tz‰ü’ù<8>(tokusyu_key ’Ç‰Á)+ ‰ü’ù<42>(Fyss1b.c): 27A/27B/27C ‚Í CR ‚Æ‚µ‚Äˆµ‚¤B
        var result = Run(new[]
        {
            Line("M", control, 1),
        });

        EquipmentTableEntry kiki = Assert.Single(result.MainEquipment);
        Assert.Equal("CR", kiki.ProductName);
        Assert.DoesNotContain(result.Errors, e => e.ErrorCode == "FY-879E");
    }

    [Fact]
    public void CheckKikiMeisyou_síB‚Ì27Œn‚ÍFY760EƒGƒ‰[()
    {
        // yCŒ´“Tz‰ü’ù<46>(Fyss1b.c): síB ‚Ì 27 Œn—\–ñŒê‚Í“ü—Í•s‰Â ¨ FY-760EB
        var result = Run(new[]
        {
            Line("B", "27A", 1),
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-760E");
    }
}
