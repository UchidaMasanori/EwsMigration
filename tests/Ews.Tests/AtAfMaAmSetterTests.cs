using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="AtAfMaAmSetter"/>(=Fysk01_Set_ATAFMA)の移植テスト。
/// </summary>
public sealed class AtAfMaAmSetterTests
{
    private const double Tolerance = 1e-9;

    private static NumericElectricalParameters[] MakeParams()
        => new[]
        {
            new NumericElectricalParameters(),
            new NumericElectricalParameters(),
            new NumericElectricalParameters(),
        };

    private static string[] Type(string first, string second)
        => new[] { first, second };

    private static SelectionWorkParameters Work(
        double energizingCurrent = 100.0,
        char parentPhase = '1',
        short phaseCount = 1,
        double voltage = 100.0,
        string loadKind = "H ")
        => new()
        {
            LoadKind = loadKind,
            EnergizingCurrent = energizingCurrent,
            LoadCapacity = 5000.0,
            PhaseCount = phaseCount,
            CircuitVoltage = voltage,
            StartKind = '1',
            ParentPhaseCount = parentPhase,
        };

    // ---------------- epno == 2(下位機器 sep[2]) ----------------

    [Fact]
    public void 下位_基準電流が負ならSYS_ERRを返す()
    {
        var sep = MakeParams();
        short ret = AtAfMaAmSetter.Apply("MCB   ", 2, sep, Type("       ", "       "),
            Work(loadKind: "ZZ"), new AreaRewriteFlags());
        Assert.Equal((short)-1, ret);
    }

    [Fact]
    public void 下位_CKSはA2に基準電流を設定する()
    {
        var sep = MakeParams();
        var flags = new AreaRewriteFlags();
        short ret = AtAfMaAmSetter.Apply("CKS   ", 2, sep, Type("       ", "       "), Work(), flags);
        Assert.Equal((short)0, ret);
        Assert.Equal(125.0, sep[2].A2, Tolerance); // H → den*1.25 = 100*1.25
        Assert.True(flags.A2[1]);
    }

    [Fact]
    public void 下位_MCBはATとAFに基準電流を設定する()
    {
        var sep = MakeParams();
        var flags = new AreaRewriteFlags();
        AtAfMaAmSetter.Apply("MCB   ", 2, sep, Type("       ", "       "), Work(), flags);
        Assert.Equal(125.0, sep[2].At, Tolerance);
        Assert.Equal(125.0, sep[2].Af, Tolerance);
        Assert.True(flags.At[1]);
        Assert.True(flags.Af[1]);
    }

    [Fact]
    public void 下位_AFは入力値がある場合は入力を優先する()
    {
        var sep = MakeParams();
        sep[0].Af = 63.0;
        AtAfMaAmSetter.Apply("MCB   ", 2, sep, Type("       ", "       "), Work(), new AreaRewriteFlags());
        Assert.Equal(63.0, sep[2].Af, Tolerance);
    }

    [Fact]
    public void 下位_NHMBはATのみ設定しAFは設定しない()
    {
        var sep = MakeParams();
        var flags = new AreaRewriteFlags();
        AtAfMaAmSetter.Apply("NHMB  ", 2, sep, Type("       ", "       "), Work(), flags);
        Assert.Equal(125.0, sep[2].At, Tolerance);
        Assert.True(flags.At[1]);
        Assert.False(flags.Af[1]);
    }

    [Fact]
    public void 下位_ELB感度電流未入力はSetELBkando2で設定しMAフラグを立てる()
    {
        var sep = MakeParams();
        var flags = new AreaRewriteFlags();
        // EnergizingCurrent=40 → ibs=50 → Af=50、動力回路50<=60・非EV → Ma[0]=30
        AtAfMaAmSetter.Apply("ELB   ", 2, sep, Type("       ", "       "),
            Work(energizingCurrent: 40.0, parentPhase: '3'), flags);
        Assert.Equal(30.0, sep[2].Ma[0], Tolerance);
        Assert.True(flags.Ma[1]);
    }

    [Fact]
    public void 下位_ELB感度電流入力ありは入力MAを複写する()
    {
        var sep = MakeParams();
        sep[0].Ma[0] = 15.0;
        sep[0].Ma[1] = 16.0;
        sep[0].Ma[2] = 17.0;
        var flags = new AreaRewriteFlags();
        AtAfMaAmSetter.Apply("ELB   ", 2, sep, Type("       ", "       "), Work(), flags);
        Assert.Equal(15.0, sep[2].Ma[0], Tolerance);
        Assert.Equal(16.0, sep[2].Ma[1], Tolerance);
        Assert.Equal(17.0, sep[2].Ma[2], Tolerance);
        Assert.True(flags.Ma[1]);
    }

    [Fact]
    public void 下位_HPSBのAMタイプはAMに通電電流を設定する()
    {
        var sep = MakeParams();
        var flags = new AreaRewriteFlags();
        AtAfMaAmSetter.Apply("HPSB  ", 2, sep, Type("AM     ", "       "), Work(), flags);
        Assert.Equal(100.0, sep[2].Am, Tolerance);
        Assert.True(flags.Am[1]);
    }

    // ---------------- epno != 2(上位機器 sep[1]) ----------------

    [Fact]
    public void 上位_CKSは入力A2を優先する()
    {
        var sep = MakeParams();
        sep[0].A2 = 500.0;
        var flags = new AreaRewriteFlags();
        AtAfMaAmSetter.Apply("CKS   ", 1, sep, Type("       ", "       "), Work(), flags);
        Assert.Equal(500.0, sep[1].A2, Tolerance);
        Assert.True(flags.A2[0]);
    }

    [Fact]
    public void 上位_CKSは入力なしなら下位A2をフォールバックする()
    {
        var sep = MakeParams();
        sep[2].A2 = 77.0;
        AtAfMaAmSetter.Apply("CKS   ", 1, sep, Type("       ", "       "), Work(), new AreaRewriteFlags());
        Assert.Equal(77.0, sep[1].A2, Tolerance);
    }

    [Fact]
    public void 上位_CKSは異常大値なら0にする()
    {
        var sep = MakeParams();
        sep[0].A2 = 100000.0;
        AtAfMaAmSetter.Apply("CKS   ", 1, sep, Type("       ", "       "), Work(), new AreaRewriteFlags());
        Assert.Equal(0.0, sep[1].A2, Tolerance);
    }

    [Fact]
    public void 上位_MCBは入力ATとAFを優先する()
    {
        var sep = MakeParams();
        sep[0].At = 63.0;
        sep[0].Af = 100.0;
        var flags = new AreaRewriteFlags();
        AtAfMaAmSetter.Apply("MCB   ", 1, sep, Type("       ", "       "), Work(), flags);
        Assert.Equal(63.0, sep[1].At, Tolerance);
        Assert.Equal(100.0, sep[1].Af, Tolerance);
        Assert.True(flags.At[0]);
        Assert.True(flags.Af[0]);
    }

    [Fact]
    public void 上位_AT非直接予約語はW1からAT換算する()
    {
        var sep = MakeParams();
        sep[0].W1 = 3000.0;
        double expected = WattToAmpereConverter.Convert(3000.0, 3, 200.0);
        AtAfMaAmSetter.Apply("TR    ", 1, sep, Type("       ", "       "),
            Work(phaseCount: 3, voltage: 200.0), new AreaRewriteFlags());
        Assert.Equal(expected, sep[1].At, Tolerance);
    }

    [Fact]
    public void 上位_AF未入力はATをフォールバックする()
    {
        var sep = MakeParams();
        sep[0].At = 63.0;
        // sep[0].Af=0 → sep[1].At(=63)>TOL を採用
        AtAfMaAmSetter.Apply("MCB   ", 1, sep, Type("       ", "       "), Work(), new AreaRewriteFlags());
        Assert.Equal(63.0, sep[1].Af, Tolerance);
    }

    [Fact]
    public void 上位_ELB感度電流入力ありは複写しMAフラグを立てる()
    {
        var sep = MakeParams();
        sep[0].Ma[0] = 15.0;
        sep[0].Ma[1] = 16.0;
        sep[0].Ma[2] = 17.0;
        var flags = new AreaRewriteFlags();
        AtAfMaAmSetter.Apply("ELB   ", 1, sep, Type("       ", "       "), Work(), flags);
        Assert.Equal(15.0, sep[1].Ma[0], Tolerance);
        Assert.Equal(16.0, sep[1].Ma[1], Tolerance);
        Assert.Equal(17.0, sep[1].Ma[2], Tolerance);
        Assert.True(flags.Ma[0]);
    }

    [Fact]
    public void 上位_ELB感度電流未入力はSetELBkando2で設定しMAフラグは立てない()
    {
        var sep = MakeParams();
        sep[0].At = 50.0;
        sep[0].Af = 80.0;
        var flags = new AreaRewriteFlags();
        AtAfMaAmSetter.Apply("ELB   ", 1, sep, Type("       ", "       "),
            Work(parentPhase: '1'), flags);
        Assert.Equal(30.0, sep[1].Ma[0], Tolerance); // Af=80<=100・非EV → 30
        Assert.False(flags.Ma[0]);
    }

    [Fact]
    public void 上位_HSBのAMタイプはAMにATをフォールバックする()
    {
        var sep = MakeParams();
        sep[0].At = 50.0;
        var flags = new AreaRewriteFlags();
        AtAfMaAmSetter.Apply("HSB   ", 1, sep, Type("AM     ", "       "), Work(), flags);
        Assert.Equal(50.0, sep[1].Am, Tolerance); // 入力AM無し → sep[1].At(=50)
        Assert.True(flags.Am[0]);
    }
}
