using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Ews.Domain.Configuration;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// イズミ製 WL ランプ(主回路)のデフォルト機器タイプ・径サイズ設定(PropChgWlLampType /
/// PropChgWlTypeAndKei / PropChkHbnPEKOB)の移植検証。
///
/// 【C原典】Fysk00.c(toku/sekkei/src, LAMP22 有効)。IZ/MAN 指定 WL について回路記述・地区・
/// 前段記述・ヒューズ個数・PEKOB 品番から RE/TR/WP/LED タイプと径サイズ・電圧を設定する。
/// </summary>
public sealed class WlLampDefaultTypeResolverTests
{
    private sealed class StubPartNumberRepository(PartNumberInfo? info) : IPartNumberInfoRepository
    {
        public PartNumberInfo? Find(string requestDetailNumber) => info;
    }

    private static WlLampDefaultTypeResolver Build(int facilityGroup, string circuitText,
        PartNumberInfo? partNumber = null)
    {
        const string zone = "Z";
        var facility = new InMemoryFacilityAreaResolver([new FacilityAreaEntry(zone, facilityGroup)]);
        var parameters = new InMemoryRuntimeParameterProvider(
            new Dictionary<string, string?> { [RuntimeParameterNames.ZoneCode] = zone });
        var area = new CircuitDescriptionArea(
            [new CircuitDescriptionLine { LineNumber = 5, CircuitText = circuitText }]);
        return new WlLampDefaultTypeResolver(facility, parameters, area,
            new StubPartNumberRepository(partNumber));
    }

    private static MainCircuitResult Wl(string dataType1 = "", char qty = '1') =>
        new()
        {
            SequenceNumber = "010",
            Data = new MainCircuitData
            {
                ReservedWord = "WL",
                LineTypeCode = "PM ",
                DescriptionRow = "005",
                DescriptionColumn = "003",
                ParentSequenceNumber = "005",
                DataType = ["", dataType1, "", "", "", "", ""],
                ElectricalParameterSlots = [new ElectricalParameters { Qty = qty }, new(), new()],
            },
        };

    private static MainCircuitResult PowerSource(char phase = '1', char wire = '3', string voltage = "105") =>
        new()
        {
            SequenceNumber = "005",
            Data = new MainCircuitData
            {
                ReservedWord = "P",
                CircuitPhaseCount = phase,
                CircuitWireType = wire,
                CircuitVoltage = [voltage, "000", "000"],
            },
        };

    private static NumericElectricalParameters[] Sep() => [new(), new(), new()];
    private static string[] Types() => ["", "", "", "", "", "", ""];

    [Fact]
    public void 予約語がWLでなければ何もしない()
    {
        MainCircuitResult mcb = Wl();
        mcb.Data.ReservedWord = "MCB";
        string[] dataTypes = Types();

        Build(5, "F,WL,").Resolve(mcb, ["IZ ", "   ", "   ", "   "], dataTypes, Types(), Sep(), [mcb], "2607AL01");

        Assert.Equal("", dataTypes[0]);
        Assert.Equal("", dataTypes[3]);
    }

    [Fact]
    public void 水俣工場でAN指定はREタイプにする()
    {
        MainCircuitResult wl = Wl(dataType1: "AN     ");
        string[] dataTypes = Types();

        Build(4, "F,WL,").Resolve(wl, ["IZ ", "   ", "   ", "   "], dataTypes, Types(), Sep(), [wl], "2607AL01");

        Assert.Equal("RE     ", dataTypes[1]);
    }

    [Fact]
    public void 水俣以外でFの後のWLはTRとWPと径22と電圧にする()
    {
        MainCircuitResult wl = Wl();
        MainCircuitResult p = PowerSource(phase: '1', wire: '3');
        string[] dataTypes = Types();
        string[] displayTypes = Types();
        NumericElectricalParameters[] sep = Sep();

        Build(5, "F,WL,").Resolve(wl, ["IZ ", "   ", "   ", "   "], dataTypes, displayTypes, sep, [p, wl], "2607AL01");

        Assert.Equal("TR     ", dataTypes[0]);
        Assert.Equal("WP     ", dataTypes[4]);
        Assert.Equal(22.0, sep[0].Ksize);
        Assert.Equal("022.0", wl.Data.ElectricalParameterSlots[0].Ksize);
        Assert.Equal(110.0, sep[0].V2[0]);   // 1P3W → 110V
    }

    [Fact]
    public void 電源が三相ならWL電圧を220にする()
    {
        MainCircuitResult wl = Wl();
        MainCircuitResult p = PowerSource(phase: '3', wire: '3');
        NumericElectricalParameters[] sep = Sep();

        Build(5, "F,WL,").Resolve(wl, ["IZ ", "   ", "   ", "   "], Types(), Types(), sep, [p, wl], "2607AL01");

        Assert.Equal(220.0, sep[0].V2[0]);
    }

    [Fact]
    public void PEKOB品番なら径サイズを25にする()
    {
        MainCircuitResult wl = Wl();
        MainCircuitResult p = PowerSource();
        NumericElectricalParameters[] sep = Sep();
        var partNumber = new PartNumberInfo { InputPartNumber = "PEKOB-01" };

        Build(5, "F,WL,", partNumber).Resolve(wl, ["IZ ", "   ", "   ", "   "], Types(), Types(), sep, [p, wl], "2607AL01");

        Assert.Equal(25.0, sep[0].Ksize);
        Assert.Equal("025.0", wl.Data.ElectricalParameterSlots[0].Ksize);
    }

    [Fact]
    public void ヒューズ2個以上はTRに変更しない()
    {
        MainCircuitResult wl = Wl(qty: '2');   // ヒューズ2個
        MainCircuitResult p = PowerSource();
        string[] dataTypes = Types();

        Build(5, "F,WL,").Resolve(wl, ["IZ ", "   ", "   ", "   "], dataTypes, Types(), Sep(), [p, wl], "2607AL01");

        Assert.Equal("", dataTypes[0]);   // TR にならない
    }

    [Fact]
    public void IZ以外のメーカーはLEDのみ設定する()
    {
        MainCircuitResult wl = Wl();
        string[] dataTypes = Types();

        Build(5, "F,WL,").Resolve(wl, ["M  ", "   ", "   ", "   "], dataTypes, Types(), Sep(), [wl], "2607AL01");

        Assert.Equal("", dataTypes[0]);
        Assert.Equal("LED    ", dataTypes[3]);
    }
}
