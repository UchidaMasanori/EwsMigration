using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// ‚b‚s©“®¶¬(<see cref="CtAutoGenerator"/>)‚ÌˆÚAŒŸØByCŒ´“TzPre_CT_Make(Fyss15.c)B
/// </summary>
public sealed class CtAutoGeneratorTests
{
    private static MainCircuitResult Am(string a2, char kiryoso = '1', string yoyaku = "AM      ")
    {
        var r = new MainCircuitResult();
        r.Data.ReservedWord = yoyaku;
        r.Data.CircuitElement = kiryoso;
        r.Data.ElectricalParameterSlots[1].A2 = a2;
        return r;
    }

    private static MainCircuitResult Dummy()
    {
        var r = new MainCircuitResult();
        r.Data.ReservedWord = "MCB     ";
        r.Data.CircuitElement = '1';
        return r;
    }

    [Fact]
    public void PrepareCtCreation‚ÍAM‚Ì‘OŒã2‰ÓŠ‚ÌCTˆÊ’u‚ğì‚é()
    {
        var mains = new[] { Am("00050.000") };

        var result = CtAutoGenerator.PrepareCtCreation(mains);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].CauseDataNumber);
        Assert.Equal(0, result[0].InsertBeforeDataNumber); // ‘O‘}“üˆÊ’u i
        Assert.Equal(1, result[1].CauseDataNumber);
        Assert.Equal(1, result[1].InsertBeforeDataNumber); // Œã‘}“üˆÊ’u i+1
    }

    [Fact]
    public void PrepareCtCreation‚Í’èŠi“d—¬2‚ª30AˆÈ‰º‚È‚ç¶¬‚µ‚È‚¢()
    {
        Assert.Empty(CtAutoGenerator.PrepareCtCreation(new[] { Am("00030.000") }));
        Assert.Empty(CtAutoGenerator.PrepareCtCreation(new[] { Am("00000.000") }));
    }

    [Fact]
    public void PrepareCtCreation‚Í‰ñ˜H—v‘f‚ª1ˆÈŠO‚È‚ç¶¬‚µ‚È‚¢()
    {
        Assert.Empty(CtAutoGenerator.PrepareCtCreation(new[] { Am("00050.000", kiryoso: '2') }));
    }

    [Fact]
    public void PrepareCtCreation‚ÍAMˆÈŠO‚È‚ç¶¬‚µ‚È‚¢()
    {
        Assert.Empty(CtAutoGenerator.PrepareCtCreation(new[] { Am("00050.000", yoyaku: "WH      ") }));
    }

    [Fact]
    public void PrepareCtCreation‚Í•¡”AM‚ğdatanoCT¸‡‚É®—ñ‚·‚é()
    {
        var mains = new[] { Dummy(), Am("00050.000"), Dummy(), Am("00050.000") };

        var result = CtAutoGenerator.PrepareCtCreation(mains);

        Assert.Equal(4, result.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Select(c => c.InsertBeforeDataNumber));
    }
}
