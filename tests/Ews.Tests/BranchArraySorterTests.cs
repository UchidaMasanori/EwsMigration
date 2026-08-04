using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 分岐配列並べ替え(Fyss3C_Bunki_Sort)基盤ヘルパーのテスト。
/// </summary>
public class BranchArraySorterTests
{
    [Theory]
    [InlineData("MCB     ", BranchArraySorter.ReservedWordKind.MCB)]
    [InlineData("ELB     ", BranchArraySorter.ReservedWordKind.ELB)]
    [InlineData("2ERY    ", BranchArraySorter.ReservedWordKind.ERY2)]
    [InlineData("VVVF    ", BranchArraySorter.ReservedWordKind.VVVF)]
    [InlineData("P       ", BranchArraySorter.ReservedWordKind.P)]
    public void 予約語は識別子へ変換される(string yoyaku, BranchArraySorter.ReservedWordKind expected)
    {
        Assert.Equal(expected, BranchArraySorter.GetReservedWordKind(yoyaku));
    }

    [Fact]
    public void 短い予約語は8バイト右詰めで一致する()
    {
        Assert.Equal(BranchArraySorter.ReservedWordKind.MCB, BranchArraySorter.GetReservedWordKind("MCB"));
    }

    [Fact]
    public void 未知の予約語はNoneを返す()
    {
        Assert.Equal(BranchArraySorter.ReservedWordKind.None, BranchArraySorter.GetReservedWordKind("XXXX"));
    }

    [Fact]
    public void SetDecimalPointは末尾n桁の前に小数点を挿入する()
    {
        Assert.Equal("000.00", BranchArraySorter.SetDecimalPoint("00000", 2));
    }

    [Fact]
    public void SetDecimalPointはn以上の長さで先頭に0点を付す()
    {
        Assert.Equal("0.00000", BranchArraySorter.SetDecimalPoint("00000", 5));
    }

    [Fact]
    public void SetDecimalPointはn0以下で無変更()
    {
        Assert.Equal("00000", BranchArraySorter.SetDecimalPoint("00000", 0));
        Assert.Equal("00000", BranchArraySorter.SetDecimalPoint("00000", -1));
    }

    [Fact]
    public void FormatFixedWidthは幅3の0詰めにする()
    {
        Assert.Equal("007", BranchArraySorter.FormatFixedWidth(7, 3));
        Assert.Equal("123", BranchArraySorter.FormatFixedWidth(123, 3));
    }

    [Fact]
    public void FormatFixedWidthは超過時に先頭幅分を切り出す()
    {
        Assert.Equal("123", BranchArraySorter.FormatFixedWidth(12345, 3));
    }

    // --- 段階20: 作業モデル・収集関数 ---

    private static MainCircuitResult Node(
        string kaisono = "000", string chokuno = "000", string heino = "000",
        string joheino = "000", string oyatno = "000", string gyoglno = "000",
        char kiryoso = '1', string gyocd = "", char epabn = '0', string glheino = "000")
    {
        var r = new MainCircuitResult();
        r.Data.HierarchyNumber = kaisono;
        r.Data.SeriesNumber = chokuno;
        r.Data.ParallelNumber = heino;
        r.Data.UpperParallelNumber = joheino;
        r.Data.ParentSequenceNumber = oyatno;
        r.Data.LineTypeGroupNumber = gyoglno;
        r.Data.CircuitElement = kiryoso;
        r.Data.LineTypeCode = gyocd;
        r.Data.ElectricalParameterSlots[0].Bn = epabn;
        r.Data.GroupParallelNumber = glheino;
        return r;
    }

    [Fact]
    public void InitializeWorkAreaは数値変換しNewをNowの複製にする()
    {
        var mains = new[] { Node(kaisono: "002", heino: "005", kiryoso: '3') };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        Assert.Equal(2, sd[0].Now.HierarchyNumber);
        Assert.Equal(5, sd[0].Now.ParallelNumber);
        Assert.Equal(3, sd[0].Now.CircuitElement);
        Assert.Equal(BranchArraySorter.WorkStatus.NoDone, sd[0].Stat);
        Assert.Equal(sd[0].Now.ParallelNumber, sd[0].New.ParallelNumber);
        Assert.NotSame(sd[0].Now, sd[0].New);
    }

    [Fact]
    public void SetResultsはnodone以外の並列追番等を書き戻す()
    {
        var mains = new[] { Node(heino: "001"), Node(heino: "002") };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        sd[0].New.ParallelNumber = 7;
        sd[0].New.UpperParallelNumber = 4;
        sd[0].New.GroupParallelNumber = 2;
        sd[0].Stat = BranchArraySorter.WorkStatus.Done;
        sd[1].New.ParallelNumber = 9;
        sd[1].Stat = BranchArraySorter.WorkStatus.NoDone;

        BranchArraySorter.SetResults(mains, sd);

        Assert.Equal("007", mains[0].Data.ParallelNumber);
        Assert.Equal("004", mains[0].Data.UpperParallelNumber);
        Assert.Equal("002", mains[0].Data.GroupParallelNumber);
        Assert.Equal("002", mains[1].Data.ParallelNumber); // nodone は無変更
    }

    [Theory]
    [InlineData("B", true)]
    [InlineData("BO", true)]
    [InlineData("O", true)]
    [InlineData("SB", false)]
    [InlineData("", false)]
    public void IsMatchLineTypeCodeはB系のみ真(string gyocd, bool expected)
    {
        Assert.Equal(expected, BranchArraySorter.IsMatchLineTypeCode(Node(gyocd: gyocd)));
    }

    [Theory]
    [InlineData('1', true)]
    [InlineData('4', true)]
    [InlineData('0', false)]
    [InlineData('2', false)]
    public void IsMatchPanelKindは1と4のみ真(char epabn, bool expected)
    {
        Assert.Equal(expected, BranchArraySorter.IsMatchPanelKind(Node(epabn: epabn)));
    }

    [Fact]
    public void GetFloorTopElementsはdoing階層一致直列1のみ()
    {
        var mains = new[]
        {
            Node(kaisono: "001", chokuno: "001"),
            Node(kaisono: "001", chokuno: "002"),
            Node(kaisono: "002", chokuno: "001"),
        };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        foreach (var w in sd) { w.Stat = BranchArraySorter.WorkStatus.Doing; }

        Assert.Equal(new[] { 0 }, BranchArraySorter.GetFloorTopElements(sd, 1));
    }

    [Fact]
    public void GetFloorElementsOfSeriesは直後の連続直列要素を得る()
    {
        var mains = new[]
        {
            Node(joheino: "001", kaisono: "001", heino: "001", chokuno: "001"),
            Node(joheino: "001", kaisono: "001", heino: "001", chokuno: "002"),
            Node(joheino: "001", kaisono: "001", heino: "001", chokuno: "003"),
            Node(joheino: "001", kaisono: "001", heino: "002", chokuno: "001"),
        };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        foreach (var w in sd) { w.Stat = BranchArraySorter.WorkStatus.Doing; }

        Assert.Equal(new[] { 1, 2 }, BranchArraySorter.GetFloorElementsOfSeries(sd, 0));
    }

    [Fact]
    public void GetBrothersは同一親データ追番を集める()
    {
        var mains = new[]
        {
            Node(oyatno: "005"),
            Node(oyatno: "007"),
            Node(oyatno: "005"),
        };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        Assert.Equal(new[] { 0, 2 }, BranchArraySorter.GetBrothers(sd, 0));
    }

    [Fact]
    public void 最小最大階層番号はdoingのみ対象()
    {
        var mains = new[]
        {
            Node(kaisono: "002"),
            Node(kaisono: "005"),
            Node(kaisono: "001"),
        };
        var sd = BranchArraySorter.InitializeWorkArea(mains);
        sd[0].Stat = BranchArraySorter.WorkStatus.Doing;
        sd[1].Stat = BranchArraySorter.WorkStatus.Doing;
        sd[2].Stat = BranchArraySorter.WorkStatus.NoDone; // 対象外

        Assert.Equal(2, BranchArraySorter.GetMinimumHierarchyNumber(sd));
        Assert.Equal(5, BranchArraySorter.GetMaximumHierarchyNumber(sd));
    }
}
