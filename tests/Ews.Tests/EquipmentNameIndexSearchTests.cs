using Ews.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="EquipmentNameIndexSearch"/>(【C原典】Fysk01_Kikisearch_PT / PT2)のテスト。
/// 機器マスター品名索引(FYDF817)を品名+データ追番で照合し PT レコードを取得する。
/// </summary>
public sealed class EquipmentNameIndexSearchTests
{
    private static EquipmentNameIndex Rec(string productName, string dataNo, string reservedWord) =>
        new() { ProductName = productName, DataNo = dataNo, ReservedWord = reservedWord };

    [Fact]
    public void 品名と追番が一致すればデータ有りを返す()
    {
        var target = Rec("VT", "0002", "PT");
        var index = new List<EquipmentNameIndex>
        {
            Rec("VT", "0001", "CT"),
            target,
        };

        EquipmentNameIndexSearchResult result = EquipmentNameIndexSearch.SearchByDataNo("VT", "0002", index);

        Assert.Equal(EquipmentNameIndexSearch.DataFound, result.Status);
        Assert.Same(target, result.Record);
    }

    [Fact]
    public void 一致する追番がなければデータ無しを返す()
    {
        var index = new List<EquipmentNameIndex>
        {
            Rec("VT", "0001", "PT"),
        };

        EquipmentNameIndexSearchResult result = EquipmentNameIndexSearch.SearchByDataNo("VT", "0002", index);

        Assert.Equal(EquipmentNameIndexSearch.DataNothing, result.Status);
        Assert.Null(result.Record);
    }

    [Fact]
    public void 品名が異なれば一致しない()
    {
        var index = new List<EquipmentNameIndex>
        {
            Rec("CT", "0001", "PT"),
        };

        EquipmentNameIndexSearchResult result = EquipmentNameIndexSearch.SearchByDataNo("VT", "0001", index);

        Assert.Equal(EquipmentNameIndexSearch.DataNothing, result.Status);
    }

    [Fact]
    public void PT検索は先頭がPT以外なら追番を進めてPTを取得する()
    {
        var pt = Rec("VT", "0003", "PT");
        var index = new List<EquipmentNameIndex>
        {
            Rec("VT", "0001", "CT"),
            Rec("VT", "0002", "ZCT"),
            pt,
        };

        EquipmentNameIndexSearchResult result = EquipmentNameIndexSearch.SearchPt("VT", index);

        Assert.Equal(EquipmentNameIndexSearch.DataFound, result.Status);
        Assert.Same(pt, result.Record);
    }

    [Fact]
    public void PT検索は最初の追番がPTならそれを返す()
    {
        var pt = Rec("VT", "0001", "PT");
        var index = new List<EquipmentNameIndex>
        {
            pt,
            Rec("VT", "0002", "CT"),
        };

        EquipmentNameIndexSearchResult result = EquipmentNameIndexSearch.SearchPt("VT", index);

        Assert.Equal(EquipmentNameIndexSearch.DataFound, result.Status);
        Assert.Same(pt, result.Record);
    }

    [Fact]
    public void PT検索は追番が途切れたらデータ無しを返す()
    {
        var index = new List<EquipmentNameIndex>
        {
            Rec("VT", "0001", "CT"),
            Rec("VT", "0002", "ZCT"),
        };

        EquipmentNameIndexSearchResult result = EquipmentNameIndexSearch.SearchPt("VT", index);

        Assert.Equal(EquipmentNameIndexSearch.DataNothing, result.Status);
        Assert.Null(result.Record);
    }

    [Fact]
    public void 予約語PT2は先頭3バイトがPTスペースでないため一致しない()
    {
        var index = new List<EquipmentNameIndex>
        {
            Rec("VT", "0001", "PT2"),
            Rec("VT", "0002", "PT"),
        };

        EquipmentNameIndexSearchResult result = EquipmentNameIndexSearch.SearchPt("VT", index);

        Assert.Equal(EquipmentNameIndexSearch.DataFound, result.Status);
        Assert.Equal("0002", result.Record!.DataNo);
    }
}
