namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

/// <summary>
/// <see cref="InverterOptionComponentBuilder"/>(=Fysk01_Make_Koukiki_INV_OP)および
/// <see cref="InverterOptionState"/>(=Fysk01_SET_INV_OPNO)の移植テスト。
/// </summary>
public sealed class InverterOptionComponentBuilderTests
{
    private static NearestRankReference Reference(
        string reservedWord = "PT",
        string makerCode = "M  ",
        string[]? parameterTypes = null) =>
        new()
        {
            ReservedWord = reservedWord,
            MakerCode = makerCode,
            ParameterTypes = parameterTypes ?? ["A1     ", "", "", "", "", "", ""],
        };

    private static EquipmentMaster Master(
        string ratingKey = "INV-OP-01",
        string partName = "FR-BIF",
        string electricalParameters = "AF=100",
        string ratedAcVa = "200",
        string ratedDcW = "80") =>
        new()
        {
            RatingKey = ratingKey,
            PartName = partName,
            ElectricalParameters = electricalParameters,
            RatedCapacityAcVa = ratedAcVa,
            RatedCapacityDcW = ratedDcW,
        };

    private static ComponentEquipment Build(
        char occurrence = '4',
        string dataNumber = "010",
        string controlSpecNumber = "020",
        string generationNumber = "030",
        string lineType = "A    ",
        string powerSystemNumber = "001",
        string loadCapacityRaw = "     ") =>
        InverterOptionComponentBuilder.Build(
            occurrence, dataNumber, controlSpecNumber, generationNumber,
            lineType, Reference(), Master(), powerSystemNumber, loadCapacityRaw);

    [Fact]
    public void 固定リテラルと引数キーが設定される()
    {
        InverterOptionState.Set(0);
        ComponentEquipment c = Build();

        Assert.Equal('4', c.EquipmentOccurrenceKind);
        Assert.Equal("010", c.DataNumber);
        Assert.Equal("020", c.ControlSpecNumber);
        Assert.Equal("030", c.GenerationNumber);
        Assert.Equal("A    ", c.LineType);
        Assert.Equal("001", c.PowerSystemNumber);
        Assert.Equal('Y', c.ProductionTransferKind);
        Assert.Equal('I', c.DoorMountKind);
        Assert.Equal(string.Empty, c.SearchResultCode);
    }

    [Fact]
    public void 機器マスタキーは直近上下位から定格や品名は機器マスタから写像される()
    {
        InverterOptionState.Set(0);
        ComponentEquipment c = Build();

        Assert.Equal("PT", c.MachineKey.ReservedWord);       // ck->key.yoyaku
        Assert.Equal("M  ", c.MachineKey.MakerCode);         // ck->key.mkcd
        Assert.Equal("A1", c.MachineKey.ParameterTypes[0]);  // &ck->key.tjg(末尾空白除去)
        Assert.Equal("INV-OP-01", c.MachineKey.RatingKey);   // kk->pkey.teikkey
        Assert.Equal("AF=100", c.ElectricalParameterString); // kk->pstring
        Assert.Equal("FR-BIF", c.PartName);                  // kk->hinmei
        Assert.Equal("200", c.RatedCapacityAcVa);            // kk->hojg
        Assert.Equal("80", c.RatedCapacityDcW);
    }

    [Fact]
    public void 構成機器キーは発生区分と追番3種で構成される()
    {
        InverterOptionState.Set(0);
        ComponentEquipment c = Build(occurrence: '4', dataNumber: "010", controlSpecNumber: "020", generationNumber: "030");

        Assert.Equal("4010020030", c.ComponentKey);
    }

    [Fact]
    public void オプション番号が3以外なら手配数量は常に1()
    {
        InverterOptionState.Set(1);
        ComponentEquipment c = Build(loadCapacityRaw: "99999");

        Assert.Equal('1', c.OrderQuantity);
    }

    [Fact]
    public void ラインノイズフィルタで負荷容量が15kW超なら手配数量は4()
    {
        InverterOptionState.Set(3);
        ComponentEquipment c = Build(loadCapacityRaw: "15010"); // (15010/10)/100.0=15.01>15.0

        Assert.Equal('4', c.OrderQuantity);
    }

    [Fact]
    public void ラインノイズフィルタで負荷容量がちょうど15kWなら手配数量は1()
    {
        InverterOptionState.Set(3);
        ComponentEquipment c = Build(loadCapacityRaw: "15000"); // (15000/10)/100.0=15.0 は超えない

        Assert.Equal('1', c.OrderQuantity);
    }

    [Fact]
    public void ラインノイズフィルタで負荷容量が空白なら手配数量は1()
    {
        InverterOptionState.Set(3);
        ComponentEquipment c = Build(loadCapacityRaw: "       ");

        Assert.Equal('1', c.OrderQuantity);
    }

    [Fact]
    public void 空エリアへの追加で件数は1()
    {
        InverterOptionState.Set(0);
        var list = new List<ComponentEquipment>();

        int count = InverterOptionComponentBuilder.Append(
            list, '4', "010", "020", "030", "A    ", Reference(), Master(), "001", "     ");

        Assert.Equal(1, count);
        Assert.Single(list);
    }

    [Fact]
    public void 既存より小キーは前方へ割り込む()
    {
        InverterOptionState.Set(0);
        var list = new List<ComponentEquipment>();
        InverterOptionComponentBuilder.Append(list, '4', "500", "000", "000", "A    ", Reference(), Master(), "001", "     ");

        int count = InverterOptionComponentBuilder.Append(list, '4', "100", "000", "000", "A    ", Reference(), Master(), "001", "     ");

        Assert.Equal(2, count);
        Assert.Equal("4100000000", list[0].ComponentKey);
        Assert.Equal("4500000000", list[1].ComponentKey);
    }

    [Fact]
    public void 既存より大キーは末尾へ追加される()
    {
        InverterOptionState.Set(0);
        var list = new List<ComponentEquipment>();
        InverterOptionComponentBuilder.Append(list, '4', "100", "000", "000", "A    ", Reference(), Master(), "001", "     ");

        int count = InverterOptionComponentBuilder.Append(list, '4', "500", "000", "000", "A    ", Reference(), Master(), "001", "     ");

        Assert.Equal(2, count);
        Assert.Equal("4100000000", list[0].ComponentKey);
        Assert.Equal("4500000000", list[1].ComponentKey);
    }

    [Fact]
    public void SET_INV_OPNOはオプション番号を更新する()
    {
        InverterOptionState.Set(7);
        Assert.Equal(7, InverterOptionState.Current);

        InverterOptionState.Set(3);
        Assert.Equal(3, InverterOptionState.Current);
    }
}
