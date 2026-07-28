using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 直近上下位参照ファイル検索(<see cref="NearestRankSearch"/>)の検証。
/// 【C原典】toku/sekkei/src/Fysk01.c Fysk01_Chokkin_Read_Check(_ALL/_TMS)、Fysk0a.c Fysk0a_CmpMojisu_Get。
/// 定数(fyrt808.h): GOOD=0 / NOGOOD=1 / TOL=0.001。
/// </summary>
public sealed class NearestRankSearchTests
{
    private static readonly RatingKeyTableEntry End = new(-1, -1, -1, -1, -1, -1, -1, -1);

    /// <summary>定格値チェックを常に GOOD にする空テーブル(終端のみ)。flag=0/proc_no=PC_7。</summary>
    private static RatingCheckTable EmptyTable(string reservedWord = "MCB", short readSize = 0)
        => new(reservedWord, 17, readSize, 0, 0, new[] { End });

    private static int[] NoInput() => new int[90];

    private static NearestRankReference Candidate(
        string reservedWord, string makerCode, char mainAcDc, char controlAcDc,
        string ratingKey, string productName = "", char handleLock = ' ')
        => new()
        {
            ReservedWord = reservedWord,
            MakerCode = makerCode,
            ParameterTypes = new[] { "", "", "", "", "", "", "" },
            MainPowerAcDc = mainAcDc,
            ControlPowerAcDc = controlAcDc,
            RatingKey = ratingKey,
            ProductName = productName,
            HandleLockKind = handleLock,
        };

    [Fact]
    public void SearchFirstMatch_前方一致した最初の候補を返す()
    {
        NearestRankReference query = Candidate("MCB", "A", '1', ' ', "0030");
        var candidates = new List<NearestRankReference>
        {
            Candidate("ELB", "A", '1', ' ', "0030"),  // 予約語違い=前方一致せず
            Candidate("MCB", "A", '1', ' ', "0030", "FIRST"),
            Candidate("MCB", "A", '1', ' ', "0030", "SECOND"),
        };

        NearestRankSearchResult result = NearestRankSearch.SearchFirstMatch(
            EmptyTable(), query, candidates, string.Empty, -1, new NumericElectricalParameters(), NoInput(), -1);

        Assert.Equal(NearestRankSearch.Good, result.Status);
        Assert.Equal("FIRST", result.Selected!.ProductName);
    }

    [Fact]
    public void SearchFirstMatch_品名不一致の候補は除外して次を返す()
    {
        NearestRankReference query = Candidate("MCB", "A", '1', ' ', "0030");
        var candidates = new List<NearestRankReference>
        {
            Candidate("MCB", "A", '1', ' ', "0030", "OTHER"),
            Candidate("MCB", "A", '1', ' ', "0030", "BW50AAG"),
        };

        NearestRankSearchResult result = NearestRankSearch.SearchFirstMatch(
            EmptyTable(), query, candidates, "BW50AAG", -1, new NumericElectricalParameters(), NoInput(), -1);

        Assert.Equal(NearestRankSearch.Good, result.Status);
        Assert.Equal("BW50AAG", result.Selected!.ProductName);
    }

    [Fact]
    public void SearchFirstMatch_ハンドルロック要求時は非該当を除外する()
    {
        NearestRankReference query = Candidate("MCB", "A", '1', ' ', "0030");
        var candidates = new List<NearestRankReference>
        {
            Candidate("MCB", "A", '1', ' ', "0030", "NOLOCK", ' '),
            Candidate("MCB", "A", '1', ' ', "0030", "LOCK", 'H'),
        };

        // hfg=0(>-1) なので hlkbn!='H' は除外される。
        NearestRankSearchResult result = NearestRankSearch.SearchFirstMatch(
            EmptyTable(), query, candidates, string.Empty, 0, new NumericElectricalParameters(), NoInput(), -1);

        Assert.Equal(NearestRankSearch.Good, result.Status);
        Assert.Equal("LOCK", result.Selected!.ProductName);
    }

    [Fact]
    public void SearchFirstMatch_前方一致する候補がなければNoGoodを返す()
    {
        NearestRankReference query = Candidate("MCB", "A", '1', ' ', "0030");
        var candidates = new List<NearestRankReference>
        {
            Candidate("ELB", "A", '1', ' ', "0030"),
            Candidate("MCB", "B", '1', ' ', "0030"),  // メーカー違い
        };

        NearestRankSearchResult result = NearestRankSearch.SearchFirstMatch(
            EmptyTable(), query, candidates, string.Empty, -1, new NumericElectricalParameters(), NoInput(), -1);

        Assert.Equal(NearestRankSearch.NoGood, result.Status);
        Assert.Null(result.Selected);
    }

    [Fact]
    public void SearchClosestByMidpoint_幅を持たない候補は即採用する()
    {
        // 空テーブルでは比較値 CMP_2=0 <= TOL のため先頭の前方一致候補を即採用する。
        NearestRankReference query = Candidate("TM", "A", '1', ' ', "0030");
        var candidates = new List<NearestRankReference>
        {
            Candidate("TM", "A", '1', ' ', "0030", "PICK"),
            Candidate("TM", "A", '1', ' ', "0030", "NEXT"),
        };

        NearestRankSearchResult result = NearestRankSearch.SearchClosestByMidpoint(
            EmptyTable("TM"), query, candidates, string.Empty, -1, new NumericElectricalParameters(), NoInput(), -1);

        Assert.Equal(NearestRankSearch.Good, result.Status);
        Assert.Equal("PICK", result.Selected!.ProductName);
    }

    [Fact]
    public void ComputeCompareSize_入力ありは幅を積み上げ範囲区分で打ち切る()
    {
        // ELB: ma(3)+at(4)+af(4)+p(1)+e(1) を積み上げ、次の電圧(s_toku=-3)で打ち切り=13。
        int[] sfg = NoInput();
        foreach (int item in new[] { 16, 9, 8, 6, 7 })
        {
            sfg[item] = 1;
        }

        short size = NearestRankSearch.ComputeCompareSize(RatingKeyTables.ElbTable, sfg);
        Assert.Equal((short)13, size);
    }

    [Fact]
    public void ComputeCompareSize_先頭項目が入力なしなら0を返す()
    {
        short size = NearestRankSearch.ComputeCompareSize(RatingKeyTables.ElbTable, NoInput());
        Assert.Equal((short)0, size);
    }

    [Fact]
    public void Search_特殊予約語のflagは定格値チェックへ伝播する()
    {
        // SC は flag=1(特殊)。移植済みの RatingValueChecker へ委譲され例外を投げずに判定される。
        NearestRankReference query = Candidate("SC", "A", '1', ' ', "0030");
        var candidates = new List<NearestRankReference>
        {
            Candidate("SC", "A", '1', ' ', "0030"),
        };

        NearestRankSearchResult result = NearestRankSearch.Search(
            RatingKeyTables.ScTable, query, candidates, string.Empty, -1, new NumericElectricalParameters(), NoInput(), -1);

        Assert.Contains(result.Status, new[] { NearestRankSearch.Good, NearestRankSearch.NoGood });
    }
}
