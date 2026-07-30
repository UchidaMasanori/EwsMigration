using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Ews.Domain.Configuration;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// イズミ製ランプ(制御回路)のデフォルト機器タイプ設定(PropChgLampType /
/// PropChgSeigyolTypeAndKei)の移植検証。
///
/// 【C原典】Fysk00.c(toku/sekkei/src, LAMP22 有効)。IZ 指定 RL/GL/OL/BL について回路記述・
/// 地区から RE/TR/AN/LED タイプと径サイズ 22 を設定する。
/// </summary>
public sealed class ControlLampDefaultTypeResolverTests
{
    private static ControlLampDefaultTypeResolver Build(int facilityGroup, string circuitText)
    {
        const string zone = "Z";
        var facility = new InMemoryFacilityAreaResolver([new FacilityAreaEntry(zone, facilityGroup)]);
        var parameters = new InMemoryRuntimeParameterProvider(
            new Dictionary<string, string?> { [RuntimeParameterNames.ZoneCode] = zone });
        var area = new CircuitDescriptionArea(
            [new CircuitDescriptionLine { LineNumber = 5, CircuitText = circuitText }]);
        return new ControlLampDefaultTypeResolver(facility, parameters, area);
    }

    private static ControlEquipmentInfo Lamp(string reservedWord = "RL", string dataType1 = "") =>
        new()
        {
            ReservedWord = reservedWord,
            DescriptionRow = "005",
            DescriptionColumn = "001",
            DataType = ["", dataType1, "", "", "", "", ""],
        };

    private static string[] Types() => ["", "", "", "", "", "", ""];

    [Fact]
    public void ランプ予約語でなければ何もしない()
    {
        ControlEquipmentInfo mc = Lamp("MC");
        string[] dataTypes = Types();

        Build(5, "RL,").Resolve(mc, ["IZ ", "   ", "   ", "   "], dataTypes, Types());

        Assert.Equal("", dataTypes[0]);
        Assert.Equal("", dataTypes[3]);
    }

    [Fact]
    public void 水俣工場はタイプ指定なしでREにする()
    {
        ControlEquipmentInfo lamp = Lamp("RL");
        string[] dataTypes = Types();

        Build(4, "RL,").Resolve(lamp, ["IZ ", "   ", "   ", "   "], dataTypes, Types());

        Assert.Equal("RE     ", dataTypes[1]);
    }

    [Fact]
    public void 水俣以外はタイプ指定なしでTRとANと径22にする()
    {
        ControlEquipmentInfo lamp = Lamp("RL");   // datatype[1] 未設定
        string[] dataTypes = Types();
        string[] displayTypes = Types();

        Build(5, "RL,").Resolve(lamp, ["IZ ", "   ", "   ", "   "], dataTypes, displayTypes);

        Assert.Equal("TR     ", dataTypes[0]);
        Assert.Equal("AN     ", dataTypes[1]);
        Assert.Equal("022.0", lamp.ElectricalParameterSlots[0].Ksize);
        Assert.Equal("LED    ", dataTypes[3]);
    }

    [Fact]
    public void タイプ指定ありでDIが無ければTRにする()
    {
        ControlEquipmentInfo lamp = Lamp("GL");
        string[] dataTypes = Types();

        Build(5, "GL+(TR),").Resolve(lamp, ["IZ ", "   ", "   ", "   "], dataTypes, Types());

        Assert.Equal("TR     ", dataTypes[0]);
    }

    [Fact]
    public void タイプ指定ありでDIがあればTRにしない()
    {
        ControlEquipmentInfo lamp = Lamp("OL");
        string[] dataTypes = Types();

        Build(5, "OL+(DI),").Resolve(lamp, ["IZ ", "   ", "   ", "   "], dataTypes, Types());

        Assert.Equal("", dataTypes[0]);
    }

    [Fact]
    public void IZ以外のメーカーはLEDのみ設定する()
    {
        ControlEquipmentInfo lamp = Lamp("BL");
        string[] dataTypes = Types();

        Build(5, "BL,").Resolve(lamp, ["M  ", "   ", "   ", "   "], dataTypes, Types());

        Assert.Equal("", dataTypes[0]);
        Assert.Equal("LED    ", dataTypes[3]);
    }
}
