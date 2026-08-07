namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Masters;
using Xunit;

/// <summary>
/// <see cref="InverterOptionEquipmentChecker"/>(=PropChkInvOPKiki)‚ÌˆÚAƒeƒXƒgB
/// </summary>
public sealed class InverterOptionEquipmentCheckerTests
{
    private static NearestRankReference Ref(string yoyaku) =>
        new() { ReservedWord = yoyaku };

    private static EquipmentMaster Master(string hinmei) =>
        new() { PartName = hinmei };

    [Fact]
    public void —\–ñŒêPT‚©‚Â•i–¼FR‚Ån‚Ü‚é‚Æ^()
    {
        Assert.True(InverterOptionEquipmentChecker.IsInverterOptionEquipment(
            Ref("PT"), Master("FR-A840")));
    }

    [Fact]
    public void —\–ñŒê‚ª‹ó”’–„‚ßPT‚Å‚à^()
    {
        Assert.True(InverterOptionEquipmentChecker.IsInverterOptionEquipment(
            Ref("PT      "), Master("FR-E720")));
    }

    [Fact]
    public void —\–ñŒê‚ªˆÙ‚È‚é‚Æ‹U()
    {
        Assert.False(InverterOptionEquipmentChecker.IsInverterOptionEquipment(
            Ref("MC"), Master("FR-A840")));
    }

    [Fact]
    public void •i–¼‚ªFRn‚Ü‚è‚Å‚È‚¢‚Æ‹U()
    {
        Assert.False(InverterOptionEquipmentChecker.IsInverterOptionEquipment(
            Ref("PT"), Master("MR-J4")));
    }

    [Fact]
    public void —\–ñŒêPTM‚È‚Ç3•¶š–Ú‚ª‹ó”’‚Å‚È‚¢‚Æ‹U()
    {
        // yCŒ´“Tzstrncmp(yoyaku,"PT ",3): 3 •¶š–Ú‚ª‹ó”’‚Å‚È‚¢‚Æ•sˆê’vB
        Assert.False(InverterOptionEquipmentChecker.IsInverterOptionEquipment(
            Ref("PTM"), Master("FR-A840")));
    }

    [Fact]
    public void •i–¼‚ªFR‚Ì‚İ‚ÅƒnƒCƒtƒ“–³‚µ‚Í‹U()
    {
        Assert.False(InverterOptionEquipmentChecker.IsInverterOptionEquipment(
            Ref("PT"), Master("FRA840")));
    }

    [Fact]
    public void —¼•û‚Æ‚àğŒŠO‚È‚ç‹U()
    {
        Assert.False(InverterOptionEquipmentChecker.IsInverterOptionEquipment(
            Ref("MG"), Master("TH-N")));
    }
}
