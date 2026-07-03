using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// å‰ñ˜H¶¬(<see cref="MainCircuitBuilder"/>)‚ÌŒŸØB
/// yCŒ´“Tztoku/sekkei/src/Fyss12.c Fyss12_Make_Main / Keitou_Check / Find_Gyosyu_SymB
/// –{ƒtƒF[ƒY‚Å‚Í“Š‡œ‘g‚İ‚ÆŒn“ƒ`ƒFƒbƒN(Keitou_Check)‚ğ‘ÎÛ‚Æ‚·‚éB
/// </summary>
public sealed class MainCircuitBuilderTests
{
    /// <summary>síƒe[ƒuƒ‹1Œ‚ğ¶¬‚·‚éByCŒ´“Tzstruct GYOSYU ‚Ìå—vƒtƒB[ƒ‹ƒhB</summary>
    private static LineTypeTableEntry Gyo(short systemNumber, string lineType, char kind, int row,
        short groupNumber = 0, string? raw = null)
        => new()
        {
            SystemNumber = systemNumber,        // yCŒ´“TzK_No
            LineType = lineType,                // yCŒ´“Tzgyosyu(®Œ`Ï)
            LineTypeRaw = raw ?? lineType,      // yCŒ´“TzGyosyu(Œ´•¶, Find_Numeric —p)
            DescriptionKind = kind,             // yCŒ´“TzK_kind
            DescriptionRow = row.ToString(),    // yCŒ´“TzK_Gyo
            GroupNumber = groupNumber,          // yCŒ´“TzG_No
        };

    private static CircuitParseResult MakeMain(params LineTypeTableEntry[] lineTypes)
    {
        var parse = new CircuitParseResult();
        parse.LineTypes.AddRange(lineTypes);
        var builder = new MainCircuitBuilder();
        builder.MakeMain(parse);
        return parse;
    }

    /// <summary>‰ñ˜HİŒvƒGƒŠƒA(imagea)•t‚«‚Å MakeMain ‚ğÀs‚·‚éByCŒ´“Tzimagec/imageaB</summary>
    private static CircuitParseResult MakeMainWithImage(
        LineTypeTableEntry[] lineTypes, params CircuitDescriptionLine[] designArea)
    {
        var parse = new CircuitParseResult();
        parse.LineTypes.AddRange(lineTypes);
        new MainCircuitBuilder().MakeMain(parse, designArea);
        return parse;
    }

    [Fact]
    public void Keitou_Check_³í‚ÈŒn“\¬‚ÍƒGƒ‰[‚ğo‚³‚È‚¢()
    {
        // Œn“í•Ê'1'(PŒn): P/M/BN/NP ‚Í‚¢‚¸‚ê‚à‹–‰ÂsíBBN ‚ÌŒã‘±‚Í NP ‚Ì‚İ(FY-672E‰ñ”ğ)B
        // P ‚É‚ÍŒã‘±‚Ì M ‚ª•K—v(Gyosyu_Check ‚Ì FY-677E‰ñ”ğ)B
        var result = MakeMain(
            Gyo(1, "P", '1', 1),
            Gyo(1, "M", '1', 2),
            Gyo(1, "BN", '1', 3),
            Gyo(1, "NP", '1', 4));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Keitou_Check_Œn““à‚ÉBN‚ª2‚Â‚ÅFY671E()
    {
        // yCŒ´“Tz”Õƒ^ƒCƒgƒ‹(BN)‚ÍŒn““à‚É1‚ÂˆÈ‰ºB2‚Â–Ú‚Å FY-671EB
        var result = MakeMain(
            Gyo(1, "BN", '1', 1),
            Gyo(1, "BN", '1', 2));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-671E");
    }

    [Fact]
    public void Keitou_Check_BN‚ÌŒã‚ÉNPCˆÈŠO‚ª‘±‚­‚ÆFY672E()
    {
        // yCŒ´“TzBN ŒŸoŒãA“¯ˆêŒn““à‚Å NP/C ˆÈŠO(‚±‚±‚Å‚Í P)‚ªŒ»‚ê‚é‚Æ FY-672EB
        var result = MakeMain(
            Gyo(1, "BN", '1', 1),
            Gyo(1, "P", '1', 2));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-672E");
        Assert.Contains(result.Errors, e => e.LineNumber == 2);
    }

    [Fact]
    public void Keitou_Check_Œn““à‚ÉSEP‚ª2‚Â‚ÅFY673E()
    {
        // yCŒ´“TzŒn“I—¹(SEP)‚ÍŒn““à‚É1‚ÂˆÈ‰ºB2‚Â–Ú‚Å FY-673EB
        var result = MakeMain(
            Gyo(1, "SEP", '1', 1),
            Gyo(1, "SEP", '1', 2));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-673E");
    }

    [Fact]
    public void Keitou_Check_SEP‚ÌŒã‚ÉNPCBNˆÈŠO‚ª‘±‚­‚ÆFY674E()
    {
        // yCŒ´“TzSEP ŒŸoŒãA“¯ˆêŒn““à‚Å NP/C/BN ˆÈŠO(‚±‚±‚Å‚Í P)‚ªŒ»‚ê‚é‚Æ FY-674EB
        var result = MakeMain(
            Gyo(1, "SEP", '1', 1),
            Gyo(1, "P", '1', 2));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-674E");
    }

    [Fact]
    public void Keitou_Check_PS‚ª1‚Â‚Ì‚İ‚ÅFY679E()
    {
        // yCŒ´“TzPS ‚ÍŒn““à‚É2‚Â‘¶İ‚Å‚«‚éB1‚Â‚Ì‚İ‚Å FY-679EB
        var result = MakeMain(
            Gyo(1, "PS", '1', 1));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-679E");
    }

    [Fact]
    public void Keitou_Check_PS‚ª3‚ÂˆÈã‚ÅFY678E()
    {
        // yCŒ´“TzPS ‚ª3‚ÂˆÈã‚Å FY-678EB
        var result = MakeMain(
            Gyo(1, "PS", '1', 1),
            Gyo(1, "PS", '1', 2),
            Gyo(1, "PS", '1', 3));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-678E");
    }

    [Fact]
    public void Keitou_Check_PS‚ª2‚Â‚Í³í()
    {
        // yCŒ´“TzPS ‚ª‚¿‚å‚¤‚Ç2‚Â‚Í³íB
        var result = MakeMain(
            Gyo(1, "PS", '1', 1),
            Gyo(1, "PS", '1', 2));

        Assert.DoesNotContain(result.Errors, e => e.ErrorCode is "FY-678E" or "FY-679E");
    }

    [Fact]
    public void Keitou_Check_Œn“í•Ê1‚É‹–‰ÂŠOsí‚ÅFY675E()
    {
        // yCŒ´“TzŒn“í•Ê'1'(PŒn)‚Í SP ‚ğ‹–‰Â‚µ‚È‚¢ ¨ FY-675EB
        var result = MakeMain(
            Gyo(1, "SP", '1', 5));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-675E");
    }

    [Fact]
    public void Keitou_Check_Œn“í•Ê2‚É‹–‰ÂŠOsí‚ÅFY675E()
    {
        // yCŒ´“TzŒn“í•Ê'2'(SPŒn)‚Í P ‚ğ‹–‰Â‚µ‚È‚¢ ¨ FY-675EB
        var result = MakeMain(
            Gyo(1, "P", '2', 5));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-675E");
    }

    [Fact]
    public void Keitou_Check_Œn“‚²‚Æ‚ÉBN”»’è‚ªƒŠƒZƒbƒg‚³‚ê‚é()
    {
        // Œn“(K_No)‚ª•Ï‚í‚é‚Æ exist_BN ‚ªƒŠƒZƒbƒg‚³‚ê‚é‚½‚ßA•ÊŒn“‚Ì BN ‚Íd•¡ˆµ‚¢‚É‚È‚ç‚È‚¢B
        var result = MakeMain(
            Gyo(1, "BN", '1', 1),
            Gyo(2, "BN", '1', 2));

        Assert.DoesNotContain(result.Errors, e => e.ErrorCode == "FY-671E");
    }

    // ---- Gyosyu_Check(síŠÖ˜Aƒ`ƒFƒbƒN / ã‰ºŠÖŒW)----

    [Fact]
    public void Gyosyu_Check_P‚©‚çM‚Ö‚ÌŠK‘w‚Í³í‚Åe‚ªİ’è‚³‚ê‚é()
    {
        // yCŒ´“TzP ‚ÍŒã‘±‚É M/S ‚ª•K—vBM ‚Í‘O•û‚Ì P ‚ğe(O_No)‚É‚·‚éB
        var p = Gyo(1, "P", '1', 1, groupNumber: 10);
        var m = Gyo(1, "M", '1', 2, groupNumber: 20);
        var result = MakeMain(p, m);

        Assert.True(result.IsValid);
        Assert.Equal((short)10, m.ParentGroupNumber); // M.O_No = P.G_No
    }

    [Fact]
    public void Gyosyu_Check_P‚ÉMS‚ªŒã‘±‚µ‚È‚¢‚ÆFY677E()
    {
        // yCŒ´“TzP ‚ÌŒã‚É“¯ˆêŒn““à‚Å M ‚à S ‚à–³‚¯‚ê‚Î FY-677EB
        var result = MakeMain(Gyo(1, "P", '1', 3));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-677E");
        Assert.Contains(result.Errors, e => e.LineNumber == 3);
    }

    [Fact]
    public void Gyosyu_Check_M‚ÉTMP‚ª‘O’u‚µ‚È‚¢‚ÆFY677E()
    {
        // yCŒ´“TzM ‚Í‘O•û‚É TM ‚© P ‚ª•K—vB–³‚¯‚ê‚Î FY-677EB
        var result = MakeMain(Gyo(1, "M", '1', 4));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-677E");
    }

    [Fact]
    public void Gyosyu_Check_P‚©‚çTM‚©‚çM‚ÌŠK‘w‚Í³í‚Åe‚ª˜A½‚·‚é()
    {
        // yCŒ´“TzTM ‚Í‘O•û‚Ì P ‚ğe‚É‚·‚éBM ‚Í‘O•û‚Ì TM ‚ğe‚É‚·‚éB
        var p = Gyo(1, "P", '1', 1, groupNumber: 10);
        var tm = Gyo(1, "TM", '1', 2, groupNumber: 20);
        var m = Gyo(1, "M", '1', 3, groupNumber: 30);
        var result = MakeMain(p, tm, m);

        Assert.True(result.IsValid);
        Assert.Equal((short)10, tm.ParentGroupNumber); // TM.O_No = P.G_No
        Assert.Equal((short)20, m.ParentGroupNumber);  // M.O_No = TM.G_No
    }

    [Fact]
    public void Gyosyu_Check_SM‚ÍM‚É˜AŒ‹‚µe‚ªİ’è‚³‚ê‚é()
    {
        // yCŒ´“TzSM(”Ô†1)‚Í‘O•û‚Ì M ‚ğe(O_No)‚É‚·‚éBFind_Numeric("SM")=1B
        var p = Gyo(1, "P", '1', 1, groupNumber: 10);
        var m = Gyo(1, "M", '1', 2, groupNumber: 20);
        var sm = Gyo(1, "SM", '1', 3, groupNumber: 30, raw: "SM");
        var result = MakeMain(p, m, sm);

        Assert.True(result.IsValid);
        Assert.Equal((short)20, sm.ParentGroupNumber); // SM.O_No = M.G_No
    }

    [Fact]
    public void Gyosyu_Check_B‚ÉTMMSM‚ª‘O’u‚µ‚È‚¢‚ÆFY677E()
    {
        // yCŒ´“TzB/BO ‚Í‘O•û‚É TM/M/SM ‚ª•K—vBP ‚Ì‚İ‚ª‘O’u‚¾‚Æ FY-677EB
        // P ‚ÌŒã‘± M(––”ö)‚Å P ƒ`ƒFƒbƒN‚Í’Ê‰ß‚µAB ‚Å FY-677E ‚É‚È‚éB
        var result = MakeMain(
            Gyo(1, "P", '1', 1),
            Gyo(1, "B", '1', 2),
            Gyo(1, "M", '1', 3));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-677E");
        Assert.Contains(result.Errors, e => e.LineNumber == 2);
    }

    [Fact]
    public void Gyosyu_Check_PM‚ÍŒã‘±‚ÌM‚ğe‚ÉŒp³‚·‚é()
    {
        // yCŒ´“TzPM ‚ÍŒã•û‚Ì M “™‚ğe(O_No)‚Æ G_kind ‚ÉŒp³‚·‚éB
        var p = Gyo(1, "P", '1', 1, groupNumber: 10);
        var pm = Gyo(1, "PM", '1', 2, groupNumber: 20);
        var m = Gyo(1, "M", '1', 3, groupNumber: 30);
        m.CircuitClass = 'X';
        var result = MakeMain(p, pm, m);

        Assert.True(result.IsValid);
        Assert.Equal(m.ParentGroupNumber, pm.ParentGroupNumber); // PM.O_No = M.O_No
        Assert.Equal('X', pm.CircuitClass);                       // PM.G_kind = M.G_kind
    }

    [Fact]
    public void Gyosyu_Check_PM‚ÉŒã‘±‚Ì‘ÎÛsí‚ª–³‚¢‚ÆFY677E()
    {
        // yCŒ´“TzPM ‚ÌŒã•û‚É M/B/BO/TM/SM/S ‚ª–³‚¯‚ê‚Î FY-677EB
        var result = MakeMain(
            Gyo(1, "P", '1', 1),
            Gyo(1, "M", '1', 2),
            Gyo(1, "PM", '1', 3));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-677E");
        Assert.Contains(result.Errors, e => e.LineNumber == 3);
    }

    [Fact]
    public void Gyosyu_Check_‰ü’ù15_PMÅ‰º’i‚Å‚à‰ñ˜H‹Lq‚É27A‚ ‚ê‚Î³í()
    {
        // yCŒ´“Tz‰ü’ù<15>: PM ‚ªŒn“Å‰º’i(Œã•û‚É‘ÎÛsí‚È‚µ)‚Å‚àA“¯ˆês”Ô†‚Ì
        //   ‰ñ˜HİŒvƒGƒŠƒA‹Lq‚É 27A/27B/27C ‚ª‚ ‚ê‚Î FY-677E ‚É‚µ‚È‚¢B
        var lineTypes = new[]
        {
            Gyo(1, "P", '1', 1),
            Gyo(1, "M", '1', 2),
            Gyo(1, "PM", '1', 3),
        };
        var image = new CircuitDescriptionLine { LineNumber = 3, CircuitText = "AB27ACD" };

        var result = MakeMainWithImage(lineTypes, image);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Gyosyu_Check_‰ü’ù15_‰ñ˜H‹Lq‚É27Œn‚ª–³‚¯‚ê‚ÎFY677E()
    {
        // yCŒ´“Tz‰ü’ù<15>: ‰ñ˜H‹Lq‚É 27A/27B/27C ‚ª–³‚¯‚ê‚Î]—ˆ‚Ç‚¨‚è FY-677EB
        var lineTypes = new[]
        {
            Gyo(1, "P", '1', 1),
            Gyo(1, "M", '1', 2),
            Gyo(1, "PM", '1', 3),
        };
        var image = new CircuitDescriptionLine { LineNumber = 3, CircuitText = "AB99XYZ" };

        var result = MakeMainWithImage(lineTypes, image);

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-677E");
    }

    // ==== E.3: Ele_Equal_Check(“d‹Cƒpƒ‰ƒ[ƒ^“¯ˆêƒ`ƒFƒbƒN) ====

    /// <summary>‹@Šíƒe[ƒuƒ‹1Œ‚ğ¶¬‚·‚éByCŒ´“Tzstruct KIKITABLE ‚Ìå—vƒtƒB[ƒ‹ƒhB</summary>
    private static EquipmentTableEntry Kiki(
        string reservedWord, string ysno, short row = 1, short column = 0,
        params (string Field, string Value)[] rating)
    {
        var kiki = new EquipmentTableEntry
        {
            ReservedWord = reservedWord,        // yCŒ´“Tzyoyaku
            ReservedWordNumber = ysno,          // yCŒ´“Tzysno
            LineNumber = row,                   // yCŒ´“TzK_Gyo
            Column = column,                    // yCŒ´“TzK_Ket
        };
        if (rating.Length > 0)
        {
            var values = new RatingValues(reservedWord); // yCŒ´“Tzkey_tbl
            foreach ((string field, string value) in rating)
            {
                values.Set(field, value);
            }
            kiki.RatingValues = values;
        }
        return kiki;
    }

    private static CircuitParseResult RunEleEqual(params EquipmentTableEntry[] equipment)
    {
        var parse = new CircuitParseResult();
        parse.MainEquipment.AddRange(equipment);
        new MainCircuitBuilder().MakeMain(parse);
        return parse;
    }

    [Fact]
    public void EleEqual_MCDTƒyƒA‚Å’èŠiˆê’v‚Í³í()
    {
        // yCŒ´“Tz“¯ˆê ysno ‚Ì MCDT ‚ª2ŒAp/a/v/vc ˆê’v ¨ ƒGƒ‰[‚È‚µB
        var result = RunEleEqual(
            Kiki("MCDT", "1", 1, 0, ("p", "3"), ("a", "100"), ("v", "200"), ("vc", "110")),
            Kiki("MCDT", "1", 2, 0, ("p", "3"), ("a", "100"), ("v", "200"), ("vc", "110")));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EleEqual_MCDT’P“Æ‚ÍFY630E()
    {
        // yCŒ´“Tz“¯ˆê ysno ‚ÌƒyƒA‚ª‘¶İ‚µ‚È‚¢(n != 1) ¨ FY-630EB
        var result = RunEleEqual(
            Kiki("MCDT", "1", 5, 0, ("p", "3")));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-630E" && e.LineNumber == 5);
    }

    [Fact]
    public void EleEqual_MCDTƒyƒA‚Å’èŠi•sˆê’v‚ÍFY630E()
    {
        // yCŒ´“TzƒyƒA‚¾‚ª v ‚ªˆÙ‚È‚é ¨ FY-630EB
        var result = RunEleEqual(
            Kiki("MCDT", "1", 3, 0, ("p", "3"), ("a", "100"), ("v", "200"), ("vc", "110")),
            Kiki("MCDT", "1", 4, 0, ("p", "3"), ("a", "100"), ("v", "400"), ("vc", "110")));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-630E");
    }

    [Fact]
    public void EleEqual_CSDTƒyƒA‚Å’èŠiˆê’v‚Í³í()
    {
        // yCŒ´“TzCSDT ‚Í p/a/v/fv ‚ğ”äŠr‚·‚éB
        var result = RunEleEqual(
            Kiki("CSDT", "2", 1, 0, ("p", "2"), ("a", "100"), ("v", "200"), ("fv", "A")),
            Kiki("CSDT", "2", 2, 0, ("p", "2"), ("a", "100"), ("v", "200"), ("fv", "A")));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EleEqual_MC_2Œ–Ú‚É“ü—Í‚ª‚ ‚èFY631E()
    {
        // yCŒ´“TzMC ‚Ì2Œ–ÚˆÈ~‚ª”ñ‹ó ¨ FY-631EB
        var result = RunEleEqual(
            Kiki("MC", "1", 1, 0, ("p", "3"), ("a", "100")),
            Kiki("MC", "1", 7, 0, ("p", "3")));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-631E" && e.LineNumber == 7);
    }

    [Fact]
    public void EleEqual_MC_2Œ–Ú‚ª‹ó‚È‚çŠî€’l‚ğ•¡Ê‚·‚é()
    {
        // yCŒ´“TzMC ‚Ì2Œ–Ú‚ª‹ó ¨ Šî€(1Œ–Ú)‚Ì p/a/v/fv “™‚ğ•¡ÊB
        var baseMc = Kiki("MC", "1", 1, 0, ("p", "3"), ("a", "100"), ("v", "200"), ("fv", "A"));
        var dupMc = Kiki("MC", "1", 2, 0);

        var parse = new CircuitParseResult();
        parse.MainEquipment.Add(baseMc);
        parse.MainEquipment.Add(dupMc);
        new MainCircuitBuilder().MakeMain(parse);

        Assert.True(parse.IsValid);
        Assert.NotNull(dupMc.RatingValues);
        Assert.Equal("3", dupMc.RatingValues!.Get("p"));
        Assert.Equal("100", dupMc.RatingValues.Get("a"));
        Assert.Equal("200", dupMc.RatingValues.Get("v"));
        Assert.Equal("A", dupMc.RatingValues.Get("fv"));
    }

    [Fact]
    public void EleEqual_TSWd•¡‚ÍFY630E()
    {
        // yCŒ´“Tz“¯ˆê ysno ‚Ì TSW ‚ª•¡” ¨ FY-630EB
        var result = RunEleEqual(
            Kiki("TSW", "1", 8, 0),
            Kiki("TSW", "1", 9, 0));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-630E" && e.LineNumber == 8);
    }

    [Fact]
    public void EleEqual_ysno‚ª0‚Ì‹@Ší‚Í‘ÎÛŠO()
    {
        // yCŒ´“Tzysno==0(atoi)‚ÍŒp‘±(ƒ`ƒFƒbƒN‘ÎÛŠO)B’P“Æ MCDT ‚Å‚àƒGƒ‰[‚È‚µB
        var result = RunEleEqual(
            Kiki("MCDT", "0", 1, 0, ("p", "3")));

        Assert.True(result.IsValid);
    }
}
