namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

/// <summary>
/// <see cref="MechanicalInterlockComponentBuilder"/>(=Fysk01_Make_Koukiki_MI)の移植テスト。
/// </summary>
public sealed class MechanicalInterlockComponentBuilderTests
{
    private static EquipmentMaster Master(
        string reservedWord = "PT",
        string makerCode = "M",
        string parameterType = "",
        string ratingKey = "MI-05SV3",
        string partName = "MI連動子",
        string electricalParameters = "AF=100",
        string ratedAcVa = "200",
        string ratedDcW = "80") =>
        new()
        {
            ReservedWord = reservedWord,
            MakerCode = makerCode,
            ParameterType = parameterType,
            RatingKey = ratingKey,
            PartName = partName,
            ElectricalParameters = electricalParameters,
            RatedCapacityAcVa = ratedAcVa,
            RatedCapacityDcW = ratedDcW,
        };

    [Fact]
    public void 固定リテラルフィールドが設定される()
    {
        ComponentEquipment c = MechanicalInterlockComponentBuilder.Build(Master());

        Assert.Equal('4', c.EquipmentOccurrenceKind);
        Assert.Equal("999", c.DataNumber);
        Assert.Equal("999", c.GenerationNumber);
        Assert.Equal('1', c.OrderQuantity);
        Assert.Equal('Y', c.ProductionTransferKind);
        Assert.Equal(string.Empty, c.SearchResultCode);
    }

    [Fact]
    public void 制御回路仕様名称追番と行種と扉取付区分は空白のまま()
    {
        ComponentEquipment c = MechanicalInterlockComponentBuilder.Build(Master());

        Assert.Equal(string.Empty, c.ControlSpecNumber);
        Assert.Equal(string.Empty, c.LineType);
        Assert.Equal(' ', c.DoorMountKind);
    }

    [Fact]
    public void 機器マスタキーと電気パラメータと品名と補助情報が写像される()
    {
        ComponentEquipment c = MechanicalInterlockComponentBuilder.Build(Master());

        Assert.Equal("PT", c.MachineKey.ReservedWord);
        Assert.Equal("M", c.MachineKey.MakerCode);
        Assert.Equal("MI-05SV3", c.MachineKey.RatingKey);
        Assert.Equal("AF=100", c.ElectricalParameterString);
        Assert.Equal("MI連動子", c.PartName);
        Assert.Equal("200", c.RatedCapacityAcVa);
        Assert.Equal("80", c.RatedCapacityDcW);
    }

    [Fact]
    public void 構成機器キーは発生区分4と追番999と空白追番で構成する()
    {
        ComponentEquipment c = MechanicalInterlockComponentBuilder.Build(Master());
        Assert.Equal("4999   999", c.ComponentKey);
    }

    [Fact]
    public void 空エリアへの追加は件数1で先頭に格納する()
    {
        var list = new List<ComponentEquipment>();
        int count = MechanicalInterlockComponentBuilder.Append(list, Master());

        Assert.Equal(1, count);
        Assert.Single(list);
        Assert.Equal("4999   999", list[0].ComponentKey);
    }

    [Fact]
    public void 既存キーが大きい場合は前方へ割り込む()
    {
        var list = new List<ComponentEquipment>
        {
            new() { EquipmentOccurrenceKind = '5', DataNumber = "000", ControlSpecNumber = "000", GenerationNumber = "000" },
        };
        int count = MechanicalInterlockComponentBuilder.Append(list, Master());

        Assert.Equal(2, count);
        Assert.Equal("4999   999", list[0].ComponentKey);
        Assert.Equal("5000000000", list[1].ComponentKey);
    }

    [Fact]
    public void 既存キーが小さい場合は末尾へ追加する()
    {
        var list = new List<ComponentEquipment>
        {
            new() { EquipmentOccurrenceKind = '4', DataNumber = "000", ControlSpecNumber = "000", GenerationNumber = "000" },
        };
        int count = MechanicalInterlockComponentBuilder.Append(list, Master());

        Assert.Equal(2, count);
        Assert.Equal("4000000000", list[0].ComponentKey);
        Assert.Equal("4999   999", list[1].ComponentKey);
    }

    [Fact]
    public void パラメータタイプは7桁7面に分割される()
    {
        ComponentEquipment c = MechanicalInterlockComponentBuilder.Build(
            Master(parameterType: "AAAAAAABBBBBBB"));

        Assert.Equal(7, c.MachineKey.ParameterTypes.Length);
        Assert.Equal("AAAAAAA", c.MachineKey.ParameterTypes[0]);
        Assert.Equal("BBBBBBB", c.MachineKey.ParameterTypes[1]);
        Assert.Equal(string.Empty, c.MachineKey.ParameterTypes[2]);
    }
}
