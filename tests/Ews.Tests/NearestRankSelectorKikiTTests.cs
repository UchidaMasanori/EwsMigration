using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 特別予約語(MCB/ELB/…/CKS)の機器選定
/// (<see cref="NearestRankSelector.SelectSpecialReservedWord"/>)の移植テスト。
/// 【C原典】Fysk01_Kikisearch_T(Fysk01.c:4467)。
/// 電流系入力あり(epno==1)は 1(該当)/2(なし)、なし(epno==2)は 3(該当)/4(なし)を返す。
/// </summary>
public sealed class NearestRankSelectorKikiTTests
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

    // currentInput=true → parameters[0] に Af を与え epno=1(下位検索後に上位検索を実施)。
    private static MainSelectionResult Run(
        string reservedWord, IReadOnlyList<NearestRankReference> candidates,
        bool currentInput, IReadOnlyList<string>? makerCodes = null, string loadKind = "H ")
    {
        NumericElectricalParameters self = currentInput
            ? new NumericElectricalParameters { Af = 100.0 }
            : new NumericElectricalParameters();
        NumericElectricalParameters[] sep = [self, new(), new()];

        return NearestRankSelector.SelectSpecialReservedWord(
            Table(reservedWord), sep, BlankTypes(), [""], 1,
            makerCodes ?? ["A  "], string.Empty, -1,
            Work(loadKind), new AreaRewriteFlags(), candidates);
    }

    [Fact]
    public void 電流系入力ありで該当すれば状態1と該当候補を返す()
    {
        var candidates = new List<NearestRankReference> { Candidate("MCB", "A", productName: "UPPER") };

        MainSelectionResult r = Run("MCB ", candidates, currentInput: true);

        Assert.Equal(1, r.Status);
        Assert.Equal("UPPER", r.Result!.ProductName);
    }

    [Fact]
    public void 電流系入力ありで該当なしは状態2を返す()
    {
        var candidates = new List<NearestRankReference> { Candidate("ELB", "A", productName: "OTHER") };

        MainSelectionResult r = Run("MCB ", candidates, currentInput: true);

        Assert.Equal(2, r.Status);
    }

    [Fact]
    public void 電流系入力なしで該当すれば状態3と該当候補を返す()
    {
        var candidates = new List<NearestRankReference> { Candidate("MCB", "A", productName: "LOWER") };

        MainSelectionResult r = Run("MCB ", candidates, currentInput: false);

        Assert.Equal(3, r.Status);
        Assert.Equal("LOWER", r.Result!.ProductName);
    }

    [Fact]
    public void 電流系入力なしで該当なしは状態4を返す()
    {
        var candidates = new List<NearestRankReference> { Candidate("ELB", "A") };

        MainSelectionResult r = Run("MCB ", candidates, currentInput: false);

        Assert.Equal(4, r.Status);
    }

    [Fact]
    public void 下位機器でシステムエラーなら該当なし状態4を返す()
    {
        // epno=2・負荷種類が表に無い → 基準電流 -1 → SYS_ERR → GOOD 以外なので状態4。
        var candidates = new List<NearestRankReference> { Candidate("MCB", "A", productName: "X") };

        MainSelectionResult r = Run("MCB ", candidates, currentInput: false, loadKind: "ZZ");

        Assert.Equal(4, r.Status);
    }

    [Fact]
    public void 呼び出し時に項目書替えフラグを初期化する()
    {
        // 事前に汚したフラグが memset 相当で初期化されることを確認(上位添字[0]は epno=2 検索で触れない)。
        var flags = new AreaRewriteFlags();
        flags.At[0] = true;
        flags.Af[0] = true;
        NumericElectricalParameters[] sep = [new(), new(), new()];   // 電流系なし → epno=2
        var candidates = new List<NearestRankReference> { Candidate("ELB", "A") };

        NearestRankSelector.SelectSpecialReservedWord(
            Table("MCB "), sep, BlankTypes(), [""], 1, ["A  "], string.Empty, -1,
            Work(), flags, candidates);

        Assert.False(flags.At[0]);
        Assert.False(flags.Af[0]);
    }
}
