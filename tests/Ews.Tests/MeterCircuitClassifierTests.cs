using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="MeterCircuitClassifier"/>(【C原典】Keiki_Check/LGR_Check/ZCT_Check)の単体テスト。
/// </summary>
public class MeterCircuitClassifierTests
{
    private static MainCircuitResult Rec(string reserved = "", string lineTypeCode = "", string circuitCount = "000")
    {
        var r = new MainCircuitResult();
        r.Data.ReservedWord = reserved;
        r.Data.LineTypeCode = lineTypeCode;
        r.Data.ElectricalParameterSlots[2].K = circuitCount;
        return r;
    }

    [Theory]
    [InlineData("CT")]
    [InlineData("F")]
    [InlineData("DSW")]
    [InlineData("VT")]
    [InlineData("PLTR")]
    public void TryClassifyMeter_計器予約語は追加してtrue(string reserved)
    {
        var meters = new List<MeterCircuitEntry>();

        bool added = MeterCircuitClassifier.TryClassifyMeter(meters, reserved, 5);

        Assert.True(added);
        Assert.Single(meters);
        Assert.Equal(5, meters[0].Rec);
        Assert.Equal(0, meters[0].Katei);
    }

    [Theory]
    [InlineData("MCB")]
    [InlineData("CTX")]
    [InlineData("VTR")]
    [InlineData("")]
    public void TryClassifyMeter_非計器予約語は追加せずfalse(string reserved)
    {
        var meters = new List<MeterCircuitEntry>();

        bool added = MeterCircuitClassifier.TryClassifyMeter(meters, reserved, 3);

        Assert.False(added);
        Assert.Empty(meters);
    }

    [Fact]
    public void TryClassifyMeter_複数追加でRecが積み上がる()
    {
        var meters = new List<MeterCircuitEntry>();

        MeterCircuitClassifier.TryClassifyMeter(meters, "CT", 1);
        MeterCircuitClassifier.TryClassifyMeter(meters, "VT", 4);

        Assert.Equal(2, meters.Count);
        Assert.Equal(1, meters[0].Rec);
        Assert.Equal(4, meters[1].Rec);
    }

    [Fact]
    public void TryClassifyLeakageGroundRelay_回路数正かつ2文字目非Pなら追加してtrue()
    {
        var records = new List<MainCircuitResult> { Rec(circuitCount: "001", lineTypeCode: "LG") };
        var relays = new List<MeterCircuitEntry>();

        bool added = MeterCircuitClassifier.TryClassifyLeakageGroundRelay(relays, records, 0);

        Assert.True(added);
        Assert.Single(relays);
        Assert.Equal(0, relays[0].Rec);
    }

    [Fact]
    public void TryClassifyLeakageGroundRelay_回路数0なら追加せずfalse()
    {
        var records = new List<MainCircuitResult> { Rec(circuitCount: "000", lineTypeCode: "LG") };
        var relays = new List<MeterCircuitEntry>();

        bool added = MeterCircuitClassifier.TryClassifyLeakageGroundRelay(relays, records, 0);

        Assert.False(added);
        Assert.Empty(relays);
    }

    [Fact]
    public void TryClassifyLeakageGroundRelay_行種コード2文字目Pなら追加せずfalse()
    {
        // gyocd[1]=='P'(例: "LP")は対象外。
        var records = new List<MainCircuitResult> { Rec(circuitCount: "002", lineTypeCode: "LP") };
        var relays = new List<MeterCircuitEntry>();

        bool added = MeterCircuitClassifier.TryClassifyLeakageGroundRelay(relays, records, 0);

        Assert.False(added);
        Assert.Empty(relays);
    }

    [Fact]
    public void ClassifyZeroCurrentTransformer_無条件で追加する()
    {
        var transformers = new List<MeterCircuitEntry>();

        MeterCircuitClassifier.ClassifyZeroCurrentTransformer(transformers, 7);

        Assert.Single(transformers);
        Assert.Equal(7, transformers[0].Rec);
        Assert.Equal(0, transformers[0].Katei);
    }
}
