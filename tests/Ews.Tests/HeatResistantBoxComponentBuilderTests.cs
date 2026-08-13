namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

/// <summary>
/// <see cref="HeatResistantBoxComponentBuilder"/>(=Fysk01_Make_Koukiki_TainetuBox)の移植テスト。
/// </summary>
public sealed class HeatResistantBoxComponentBuilderTests
{
    private static EquipmentMaster Master(
        string reservedWord = "PT",
        string makerCode = "K",
        string parameterType = "",
        string ratingKey = "TB2P60A+(F1+BOX)",
        string partName = "耐熱BOX",
        string electricalParameters = "P=2 W=3",
        string ratedAcVa = "100",
        string ratedDcW = "50") =>
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
        ComponentEquipment c = HeatResistantBoxComponentBuilder.Build(Master());

        Assert.Equal('4', c.EquipmentOccurrenceKind);
        Assert.Equal("000", c.DataNumber);
        Assert.Equal("000", c.ControlSpecNumber);
        Assert.Equal("000", c.GenerationNumber);
        Assert.Equal("B    ", c.LineType);
        Assert.Equal('1', c.OrderQuantity);
        Assert.Equal('Y', c.ProductionTransferKind);
        Assert.Equal('I', c.DoorMountKind);
        Assert.Equal(string.Empty, c.SearchResultCode);
    }

    [Fact]
    public void 機器マスタキーが写像される()
    {
        ComponentEquipment c = HeatResistantBoxComponentBuilder.Build(Master());

        Assert.Equal("PT", c.MachineKey.ReservedWord);
        Assert.Equal("K", c.MachineKey.MakerCode);
        Assert.Equal("TB2P60A+(F1+BOX)", c.MachineKey.RatingKey);
    }

    [Fact]
    public void 電気パラメータと品名と補助情報が写像される()
    {
        ComponentEquipment c = HeatResistantBoxComponentBuilder.Build(Master());

        Assert.Equal("P=2 W=3", c.ElectricalParameterString);
        Assert.Equal("耐熱BOX", c.PartName);
        Assert.Equal("100", c.RatedCapacityAcVa);
        Assert.Equal("50", c.RatedCapacityDcW);
    }

    [Fact]
    public void パラメータタイプは7桁7面に分割される()
    {
        ComponentEquipment c = HeatResistantBoxComponentBuilder.Build(
            Master(parameterType: "AAAAAAABBBBBBB"));

        Assert.Equal(7, c.MachineKey.ParameterTypes.Length);
        Assert.Equal("AAAAAAA", c.MachineKey.ParameterTypes[0]);
        Assert.Equal("BBBBBBB", c.MachineKey.ParameterTypes[1]);
        Assert.Equal(string.Empty, c.MachineKey.ParameterTypes[2]);
    }

    [Fact]
    public void 構成機器キーは10バイトのFYRT804KEY相当()
    {
        ComponentEquipment c = HeatResistantBoxComponentBuilder.Build(Master());
        Assert.Equal("4000000000", c.ComponentKey);
    }

    [Fact]
    public void 空エリアへの追加は件数1で先頭に格納する()
    {
        var list = new List<ComponentEquipment>();
        int count = HeatResistantBoxComponentBuilder.Append(list, Master());

        Assert.Equal(1, count);
        Assert.Single(list);
        Assert.Equal('4', list[0].EquipmentOccurrenceKind);
    }

    [Fact]
    public void 同一キーが既存の場合は末尾に追加する()
    {
        var list = new List<ComponentEquipment>
        {
            new() { EquipmentOccurrenceKind = '4', DataNumber = "000" },
        };
        int count = HeatResistantBoxComponentBuilder.Append(list, Master());

        Assert.Equal(2, count);
        Assert.Equal("PT", list[1].MachineKey.ReservedWord);
    }

    [Fact]
    public void 既存キーが大きい場合は前方へ割り込む()
    {
        var list = new List<ComponentEquipment>
        {
            new() { EquipmentOccurrenceKind = '5', DataNumber = "000" },
        };
        int count = HeatResistantBoxComponentBuilder.Append(list, Master());

        Assert.Equal(2, count);
        Assert.Equal("4000000000", list[0].ComponentKey);
        Assert.Equal("5000000000", list[1].ComponentKey);
    }

    [Fact]
    public void キー昇順を保つ位置へ割り込む()
    {
        var list = new List<ComponentEquipment>
        {
            new() { EquipmentOccurrenceKind = '3', DataNumber = "000" },
            new() { EquipmentOccurrenceKind = '5', DataNumber = "000" },
        };
        int count = HeatResistantBoxComponentBuilder.Append(list, Master());

        Assert.Equal(3, count);
        Assert.Equal("3000000000", list[0].ComponentKey);
        Assert.Equal("4000000000", list[1].ComponentKey);
        Assert.Equal("5000000000", list[2].ComponentKey);
    }
}
