namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

/// <summary>
/// <see cref="MainCircuitAreaCopier"/>(=Fysk01_Area_Copy_SY)の移植テスト。
/// </summary>
public sealed class MainCircuitAreaCopierTests
{
    private static MainCircuitResult MakeRecord()
    {
        return new MainCircuitResult
        {
            Data = MainCircuitData.Create(),
            Work = new CircuitWork(),
        };
    }

    [Fact]
    public void ep1とep2の値をコピー先へ複写する()
    {
        MainCircuitResult from = MakeRecord();
        MainCircuitResult to = MakeRecord();
        from.Data.ElectricalParameterSlots[1].At = "00000.123";
        from.Data.ElectricalParameterSlots[2].Af = "00000.456";

        MainCircuitAreaCopier.CopyArea([from, to], 0, 1);

        Assert.Equal("00000.123", to.Data.ElectricalParameterSlots[1].At);
        Assert.Equal("00000.456", to.Data.ElectricalParameterSlots[2].Af);
    }

    [Fact]
    public void ep0はコピーしない()
    {
        MainCircuitResult from = MakeRecord();
        MainCircuitResult to = MakeRecord();
        from.Data.ElectricalParameterSlots[0].At = "00000.999";

        MainCircuitAreaCopier.CopyArea([from, to], 0, 1);

        Assert.NotEqual("00000.999", to.Data.ElectricalParameterSlots[0].At);
    }

    [Fact]
    public void コピー後のep1は別インスタンスで独立している()
    {
        MainCircuitResult from = MakeRecord();
        MainCircuitResult to = MakeRecord();
        from.Data.ElectricalParameterSlots[1].At = "00000.100";

        MainCircuitAreaCopier.CopyArea([from, to], 0, 1);
        to.Data.ElectricalParameterSlots[1].At = "00000.200";

        Assert.Equal("00000.100", from.Data.ElectricalParameterSlots[1].At);
        Assert.NotSame(from.Data.ElectricalParameterSlots[1], to.Data.ElectricalParameterSlots[1]);
    }

    [Fact]
    public void タイプdatatypeをコピーする()
    {
        MainCircuitResult from = MakeRecord();
        MainCircuitResult to = MakeRecord();
        from.Data.DataType[0] = "MCB";
        from.Data.DataType[6] = "ELB";

        MainCircuitAreaCopier.CopyArea([from, to], 0, 1);

        Assert.Equal("MCB", to.Data.DataType[0]);
        Assert.Equal("ELB", to.Data.DataType[6]);
    }

    [Fact]
    public void 定格容量teiwvaをコピーする()
    {
        MainCircuitResult from = MakeRecord();
        MainCircuitResult to = MakeRecord();
        from.Work.RatedCapacity = 1234.5;

        MainCircuitAreaCopier.CopyArea([from, to], 0, 1);

        Assert.Equal(1234.5, to.Work.RatedCapacity);
    }

    [Fact]
    public void 配列内の別々のインデックス間でコピーできる()
    {
        MainCircuitResult r0 = MakeRecord();
        MainCircuitResult r1 = MakeRecord();
        MainCircuitResult r2 = MakeRecord();
        r2.Data.ElectricalParameterSlots[1].At = "00000.321";
        r2.Work.RatedCapacity = 99.0;

        MainCircuitAreaCopier.CopyArea([r0, r1, r2], 2, 0);

        Assert.Equal("00000.321", r0.Data.ElectricalParameterSlots[1].At);
        Assert.Equal(99.0, r0.Work.RatedCapacity);
    }
}
