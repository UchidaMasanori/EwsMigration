using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 特別予約語(MCB/ELB/…/CKS)専用の直近上下位検索
/// (<see cref="NearestRankSelector.SearchSpecialReservedWord"/>)の移植テスト。
/// 【C原典】Fysk01_Chokisearch_T(Fysk01.c:4588)。
/// </summary>
public sealed class NearestRankSelectorSpecialTests
{
    private static readonly RatingKeyTableEntry End = new(-1, -1, -1, -1, -1, -1, -1, -1);

    // 定格値チェックを常に GOOD にする空テーブル(proc_no=PC_7=17 で汎用前方一致へ)。
    private static RatingCheckTable Table(string reservedWord)
        => new(reservedWord, 17, 0, 0, 0, [End]);

    private static NearestRankReference Candidate(
        string reservedWord, string makerCode, string productName = "", string ratingKey = "0")
        => new()
        {
            ReservedWord = reservedWord,
            MakerCode = makerCode,
            ParameterTypes = ["", "", "", "", "", "", ""],
            MainPowerAcDc = ' ',
            ControlPowerAcDc = ' ',
            RatingKey = ratingKey,
            ProductName = productName,
        };

    private static string[] BlankTypes() => ["", "", "", "", "", "", ""];

    private static SelectionWorkParameters Work(string loadKind = "H ")
        => new()
        {
            LoadKind = loadKind,
            EnergizingCurrent = 100.0,
            LoadCapacity = 5000.0,
            PhaseCount = 1,
            CircuitVoltage = 100.0,
            StartKind = '1',
            ParentPhaseCount = '1',
        };

    // 電流系入力あり(Af)→ epno=1(上位機器 ApplyUpper、SYS_ERR なし)。
    private static NearestRankReference? Run(
        string reservedWord, IReadOnlyList<NearestRankReference> candidates,
        out int status, IReadOnlyList<string>? makerCodes = null, string loadKind = "H ")
    {
        var self = new NumericElectricalParameters { Af = 100.0 };
        NumericElectricalParameters[] sep = [self, new(), new()];
        ElectricalParameterInput input = ElectricalParameterInputChecker.Check(self);

        NearestRankSearchResult r = NearestRankSelector.SearchSpecialReservedWord(
            Table(reservedWord), input.ParameterNumber, sep, input.InputFlags,
            [""], 1, BlankTypes(), makerCodes ?? ["A  "], string.Empty, -1,
            Work(loadKind), new AreaRewriteFlags(), candidates);

        status = r.Status;
        return r.Selected;
    }

    [Fact]
    public void 該当ありは該当候補を返す()
    {
        var candidates = new List<NearestRankReference> { Candidate("MCB", "A", productName: "FOUND") };

        NearestRankReference? selected = Run("MCB ", candidates, out int status);

        Assert.Equal(NearestRankSearch.Good, status);
        Assert.Equal("FOUND", selected!.ProductName);
    }

    [Fact]
    public void 予約語違いは該当なし()
    {
        var candidates = new List<NearestRankReference> { Candidate("ELB", "A", productName: "OTHER") };

        Run("MCB ", candidates, out int status);

        Assert.Equal(NearestRankSearch.NoGood, status);
    }

    [Fact]
    public void メーカー違いは該当なし()
    {
        var candidates = new List<NearestRankReference> { Candidate("MCB", "B", productName: "OTHER") };

        Run("MCB ", candidates, out int status);

        Assert.Equal(NearestRankSearch.NoGood, status);
    }

    [Fact]
    public void 二番目のメーカーで該当する()
    {
        var candidates = new List<NearestRankReference> { Candidate("MCB", "B", productName: "SECOND") };

        NearestRankReference? selected = Run("MCB ", candidates, out int status, makerCodes: ["A  ", "B  "]);

        Assert.Equal(NearestRankSearch.Good, status);
        Assert.Equal("SECOND", selected!.ProductName);
    }

    [Fact]
    public void 該当なしは先頭キーを定格値空白で返す()
    {
        var candidates = new List<NearestRankReference> { Candidate("ELB", "A") };

        NearestRankReference? selected = Run("MCB ", candidates, out int status);

        Assert.Equal(NearestRankSearch.NoGood, status);
        Assert.NotNull(selected);
        Assert.Equal("MCB ", selected!.ReservedWord);
        Assert.Equal(new string(' ', selected.RatingKey.Length), selected.RatingKey);
    }

    [Fact]
    public void 下位機器で基準電流が負ならシステムエラーを返す()
    {
        // epno=2(下位機器)かつ負荷種類が表に無い → Get_Ibs が -1 → SYS_ERR。
        var self = new NumericElectricalParameters();       // 電流系入力なし → epno=2
        NumericElectricalParameters[] sep = [self, new(), new()];
        ElectricalParameterInput input = ElectricalParameterInputChecker.Check(self);
        Assert.Equal(2, input.ParameterNumber);

        NearestRankSearchResult r = NearestRankSelector.SearchSpecialReservedWord(
            Table("MCB "), input.ParameterNumber, sep, input.InputFlags,
            [""], 1, BlankTypes(), ["A  "], string.Empty, -1,
            Work(loadKind: "ZZ"), new AreaRewriteFlags(),
            new List<NearestRankReference> { Candidate("MCB", "A", productName: "X") });

        Assert.Equal(NearestRankSearch.SystemError, r.Status);
    }

    [Fact]
    public void 下位機器で該当ありは基準電流を設定して該当を返す()
    {
        // epno=2・負荷種類 H(=通電電流×1.25)で下位機器 sep[2] へ設定し検索。
        var self = new NumericElectricalParameters();       // epno=2
        NumericElectricalParameters[] sep = [self, new(), new()];
        ElectricalParameterInput input = ElectricalParameterInputChecker.Check(self);

        NearestRankSearchResult r = NearestRankSelector.SearchSpecialReservedWord(
            Table("MCB "), input.ParameterNumber, sep, input.InputFlags,
            [""], 1, BlankTypes(), ["A  "], string.Empty, -1,
            Work(loadKind: "H "), new AreaRewriteFlags(),
            new List<NearestRankReference> { Candidate("MCB", "A", productName: "LOWER") });

        Assert.Equal(NearestRankSearch.Good, r.Status);
        Assert.Equal("LOWER", r.Selected!.ProductName);
        Assert.Equal(125.0, sep[2].At, 1e-9);            // 100×1.25
    }
}
