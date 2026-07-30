using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 主回路機器選定(<see cref="NearestRankSelector"/>)の疎通検証。
/// 【C原典】Fysk01_Kikisearch_S1 / Fysk01_Chokisearch / Fysk01_Chokisearch_ALL(Fysk01.c)。
/// </summary>
public sealed class NearestRankSelectorTests
{
    private static readonly RatingKeyTableEntry End = new(-1, -1, -1, -1, -1, -1, -1, -1);

    // 定格値チェックを常に GOOD にする空テーブル(proc_no=PC_7=17 で汎用検索へ)。
    private static RatingCheckTable Table(string reservedWord = "TR ")
        => new(reservedWord, 17, 0, 0, 0, [End]);

    private static NearestRankReference Candidate(
        string reservedWord, string makerCode, char mainAcDc = ' ', char controlAcDc = ' ',
        string productName = "")
        => new()
        {
            ReservedWord = reservedWord,
            MakerCode = makerCode,
            ParameterTypes = ["", "", "", "", "", "", ""],
            MainPowerAcDc = mainAcDc,
            ControlPowerAcDc = controlAcDc,
            RatingKey = "0",
            ProductName = productName,
        };

    private static NumericElectricalParameters[] Params(NumericElectricalParameters self)
        => [self, new NumericElectricalParameters(), new NumericElectricalParameters()];

    private static string[] BlankTypes() => ["", "", "", "", "", "", ""];

    [Fact]
    public void 電流入力なしで該当ありはステータス3を返す()
    {
        var candidates = new List<NearestRankReference> { Candidate("TR ", "A", productName: "FOUND") };

        MainSelectionResult result = NearestRankSelector.SelectMain(
            0, Table(), Params(new NumericElectricalParameters()), BlankTypes(),
            [""], 1, ["A  "], string.Empty, -1, candidates);

        Assert.Equal(3, result.Status);
        Assert.Equal("FOUND", result.Result!.ProductName);
    }

    [Fact]
    public void 該当なしはステータス4を返す()
    {
        var candidates = new List<NearestRankReference> { Candidate("ELB", "A") };  // 予約語違い

        MainSelectionResult result = NearestRankSelector.SelectMain(
            0, Table(), Params(new NumericElectricalParameters()), BlankTypes(),
            [""], 1, ["A  "], string.Empty, -1, candidates);

        Assert.Equal(4, result.Status);
    }

    [Fact]
    public void 電流入力ありで該当ありはステータス1を返す()
    {
        var self = new NumericElectricalParameters { Af = 100.0 };   // 電流系入力 → epno=1
        var candidates = new List<NearestRankReference> { Candidate("TR ", "A", productName: "OK") };

        MainSelectionResult result = NearestRankSelector.SelectMain(
            0, Table(), Params(self), BlankTypes(), [""], 1, ["A  "], string.Empty, -1, candidates);

        Assert.Equal(1, result.Status);
    }

    [Fact]
    public void メーカー違いは該当せずステータス4()
    {
        var candidates = new List<NearestRankReference> { Candidate("TR ", "B") };

        MainSelectionResult result = NearestRankSelector.SelectMain(
            0, Table(), Params(new NumericElectricalParameters()), BlankTypes(),
            [""], 1, ["A  "], string.Empty, -1, candidates);

        Assert.Equal(4, result.Status);
    }

    [Fact]
    public void 電源区分が一致しないと該当しない()
    {
        var self = new NumericElectricalParameters { V2Kbn = 'A' };   // 主電源区分 A
        var candidates = new List<NearestRankReference> { Candidate("TR ", "A", mainAcDc: 'D') };  // 候補は D

        MainSelectionResult result = NearestRankSelector.SelectMain(
            0, Table(), Params(self), BlankTypes(), [""], 1, ["A  "], string.Empty, -1, candidates);

        Assert.Equal(4, result.Status);
    }

    [Fact]
    public void CT予約語は未実装で例外を投げる()
    {
        Assert.Throws<NotImplementedException>(() =>
            NearestRankSelector.SelectMain(
                0, Table("CT "), Params(new NumericElectricalParameters()), BlankTypes(),
                [""], 1, ["A  "], string.Empty, -1, []));
    }

    [Fact]
    public void 遮断器予約語は未実装で例外を投げる()
    {
        Assert.Throws<NotImplementedException>(() =>
            NearestRankSelector.SelectMain(
                0, Table("MCB "), Params(new NumericElectricalParameters()), BlankTypes(),
                [""], 1, ["A  "], string.Empty, -1, []));
    }
}
