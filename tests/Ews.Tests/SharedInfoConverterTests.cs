using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 直近上下位共用情報の数値変換検証。
/// 【C原典】Fysk01_Change_Chokin(toku/sekkei/src/Fysk01.c:4232)、
/// struct kyoyojg_s (fyscommon.h)。
/// </summary>
public sealed class SharedInfoConverterTests
{
    private static NearestRankReference Reference(NearestRankSharedInfo jg, string vcFrom, string vcTo)
        => new()
        {
            ReservedWord = "MC",
            SharedInfo = jg,
            ControlVoltageRangeFrom = vcFrom,
            ControlVoltageRangeTo = vcTo,
        };

    private static NearestRankSharedInfo SampleSharedInfo() => new()
    {
        MainPowerSharedAcDc = 'A',
        ControlPowerSharedAcDc = 'D',
        SensitivityCurrents = ["0200", "0030", "0005", "9999"],
        PrimaryVoltageValues = ["100", "200", "300"],
        PrimaryVoltageKinds = ['X', 'Y'],
        SecondaryVoltageValues = ["080", "484", "010", "020"],
        SecondaryVoltageKinds = ['Z', 'P', 'Q'],
        ControlVoltageValues = ["100", "110", "120", "130"],
        ControlVoltageKinds = ['a', 'b', 'c'],
    };

    [Fact]
    public void 変換_電源区分と電圧値を数値化する()
    {
        NumericSharedInfo r = SharedInfoConverter.Convert(Reference(SampleSharedInfo(), "085", "110"));

        Assert.Equal('A', r.MainPowerSharedAcDc);
        Assert.Equal('D', r.ControlPowerSharedAcDc);
        Assert.Equal(new[] { 100.0, 200.0, 300.0 }, r.PrimaryVoltageValues);
        Assert.Equal(new[] { 80.0, 484.0, 10.0, 20.0 }, r.SecondaryVoltageValues);
        Assert.Equal(new[] { 100.0, 110.0, 120.0, 130.0 }, r.ControlVoltageValues);
    }

    [Fact]
    public void 変換_感度電流は3枠のみ保持し4枠目は破棄される()
    {
        NumericSharedInfo r = SharedInfoConverter.Convert(Reference(SampleSharedInfo(), "085", "110"));

        Assert.Equal(3, r.SensitivityCurrents.Count);
        Assert.Equal(new[] { 200.0, 30.0, 5.0 }, r.SensitivityCurrents);
    }

    [Fact]
    public void 変換_区分は原典のコピー仕様で複写される()
    {
        NumericSharedInfo r = SharedInfoConverter.Convert(Reference(SampleSharedInfo(), "085", "110"));

        // 一次: そのまま。
        Assert.Equal(new[] { 'X', 'Y' }, r.PrimaryVoltageKinds);
        // 二次: k1 は一次の k1(X)、k2/k3 は二次の k2/k3(P/Q)。二次 k1(Z)は無視。
        Assert.Equal(new[] { 'X', 'P', 'Q' }, r.SecondaryVoltageKinds);
        // 制御: k1=一次k1(X)、k2=二次k2(P)、k3=二次k3(Q)。制御自身の区分(a/b/c)は無視。
        Assert.Equal(new[] { 'X', 'P', 'Q' }, r.ControlVoltageKinds);
    }

    [Fact]
    public void 変換_制御電圧適応範囲は100で除算する()
    {
        NumericSharedInfo r = SharedInfoConverter.Convert(Reference(SampleSharedInfo(), "085", "110"));

        Assert.Equal(0.85, r.ControlVoltageRangeFrom, 6);
        Assert.Equal(1.10, r.ControlVoltageRangeTo, 6);
    }

    [Fact]
    public void 変換_制御電圧適応範囲が0なら1に補正される()
    {
        NumericSharedInfo r = SharedInfoConverter.Convert(Reference(SampleSharedInfo(), "000", "000"));

        Assert.Equal(1.0, r.ControlVoltageRangeFrom, 6);
        Assert.Equal(1.0, r.ControlVoltageRangeTo, 6);
    }
}
