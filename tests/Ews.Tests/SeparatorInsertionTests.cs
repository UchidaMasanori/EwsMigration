using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// ƒZƒpƒŒ[ƒ^(SEP)’Ç‰ÁƒƒWƒbƒN‚ÌŒŸØByCŒ´“TzFyss12.c Kikitable_SEP_Make / sep_flg / sep_del(‰ü’ù&lt;7&gt;/&lt;12&gt;)B
/// </summary>
public sealed class SeparatorInsertionTests
{
    private static PartNumberInfo Hbn(string inputPartNumber = "", string boxType = "", string generatedBox = "")
        => new()
        {
            InputPartNumber = inputPartNumber,
            BoxType = boxType,
            GeneratedBoxPartNumber = generatedBox,
        };

    private static LineTypeTableEntry Lt(short systemNumber, string phaseWires)
        => new() { SystemNumber = systemNumber, PhaseWires = phaseWires };

    [Fact]
    public void CreateSeparatorEntry_Œn“––”ö‹@Ší‚©‚çSEP‹@Ší‚ğ¶¬‚·‚é()
    {
        var last = new EquipmentTableEntry
        {
            SystemNumber = 2,
            StringSequence = 3,
            CircuitNumberSequence = 4,
            EquipmentNumber = 100,       // D_No(10”{Ï‚İ)
            Ban = BanKind.End,
        };

        EquipmentTableEntry sep = SeparatorInsertion.CreateSeparatorEntry(last);

        Assert.Equal("SEP", sep.ReservedWord);
        Assert.Equal((short)105, sep.EquipmentNumber);   // D_No + 5
        Assert.Equal((short)2, sep.SystemNumber);        // K_No ˆøŒp‚¬
        Assert.Equal((short)3, sep.StringSequence);      // B_No ˆøŒp‚¬
        Assert.Equal((short)4, sep.CircuitNumberSequence); // N_No ˆøŒp‚¬
        Assert.Equal((short)0, sep.GroupNumber);
        Assert.Equal('M', sep.CircuitDivision);          // K_Kubun
        Assert.Equal('1', sep.AutoGenerationKind);       // yoyakkbn
        Assert.Equal('1', sep.TopFlag);                  // TOP_Flg
        Assert.Equal("00", sep.ReservedWordNumber);      // ysno
        Assert.Equal("000", sep.DescriptionRow);         // K_Gyo
        Assert.Equal("000", sep.DescriptionColumn);      // K_Ket
        Assert.Equal(last.Ban, sep.Ban);                 // ban ˆøŒp‚¬
    }

    [Fact]
    public void IsSeparatorApplicable_BOX‚ªSEP‘ÎÛ‚È‚çì}‚ ‚è()
    {
        // PropChkSEPBox==0(JBR + 350) ¨ sep_flg=0B
        var hbn = Hbn(boxType: "JBR");
        Assert.True(SeparatorInsertion.IsSeparatorApplicable(hbn, "00350", new List<string> { "GVT" }));
    }

    [Fact]
    public void IsSeparatorApplicable_•300”ñŠY“–‚È‚çì}‚ ‚è()
    {
        // PropChkSEPBox!=0(BX) ‚©‚Â PropChkHbnHB300!=0(inputhb ‚É•300•i”Ô‚È‚µ) ¨ sep_flg=0B
        var hbn = Hbn(inputPartNumber: "GSP05-GM1-GQ20", boxType: "BX");
        Assert.True(SeparatorInsertion.IsSeparatorApplicable(hbn, "00350", new List<string> { "GVT" }));
    }

    [Fact]
    public void IsSeparatorApplicable_BOX”ñ‘ÎÛ‚©‚Â•300ŠY“–‚È‚çì}‚È‚µ()
    {
        // PropChkSEPBox!=0(BX) ‚©‚Â PropChkHbnHB300==0(GVT ‚ğŠÜ‚Ş) ¨ sep_flg=-1B
        var hbn = Hbn(inputPartNumber: "GSP05-GVT-100", boxType: "BX");
        Assert.False(SeparatorInsertion.IsSeparatorApplicable(hbn, "00350", new List<string> { "GVT" }));
    }

    [Fact]
    public void HasSeparatorDeletionCondition_2Œn“ˆÈã‚Å1P3W‚Æ3P3W¬İ‚È‚ç^()
    {
        var lineTypes = new List<LineTypeTableEntry> { Lt(1, "1P3W"), Lt(2, "3P3W") };
        Assert.True(SeparatorInsertion.HasSeparatorDeletionCondition(lineTypes));
    }

    [Fact]
    public void HasSeparatorDeletionCondition_‘Šü‚ª’Pˆê‚È‚ç‹U()
    {
        var lineTypes = new List<LineTypeTableEntry> { Lt(1, "1P3W"), Lt(2, "1P3W") };
        Assert.False(SeparatorInsertion.HasSeparatorDeletionCondition(lineTypes));
    }

    [Fact]
    public void HasSeparatorDeletionCondition_1Œn“‚È‚ç‹U()
    {
        var lineTypes = new List<LineTypeTableEntry> { Lt(1, "1P3W") };
        Assert.False(SeparatorInsertion.HasSeparatorDeletionCondition(lineTypes));
    }

    [Fact]
    public void IsSeparatorInsertionAllowed_ì}‚ ‚è‚È‚çí‚É‹–‰Â()
    {
        var lineTypes = new List<LineTypeTableEntry> { Lt(1, "1P3W"), Lt(2, "3P3W") };
        // íœğŒ‚ğ–‚½‚µ‚Ä‚àAsep_flg==0(applicable=true)‚È‚ç’Ç‰Á‹–‰ÂB
        Assert.True(SeparatorInsertion.IsSeparatorInsertionAllowed(true, lineTypes));
    }

    [Fact]
    public void IsSeparatorInsertionAllowed_ì}‚È‚µ‚©‚ÂíœğŒ‚Å•s‹–‰Â()
    {
        var lineTypes = new List<LineTypeTableEntry> { Lt(1, "1P3W"), Lt(2, "3P3W") };
        Assert.False(SeparatorInsertion.IsSeparatorInsertionAllowed(false, lineTypes));
    }

    [Fact]
    public void IsSeparatorInsertionAllowed_ì}‚È‚µ‚Å‚àíœğŒ‚È‚µ‚È‚ç‹–‰Â()
    {
        var lineTypes = new List<LineTypeTableEntry> { Lt(1, "1P3W"), Lt(2, "1P3W") };
        Assert.True(SeparatorInsertion.IsSeparatorInsertionAllowed(false, lineTypes));
    }
}
