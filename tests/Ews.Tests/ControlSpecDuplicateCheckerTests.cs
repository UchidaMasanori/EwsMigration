using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlSpecDuplicateChecker"/>(【C原典】Fyss1k.c の SgtkkDoubleCheck)の単体テスト。
/// </summary>
public sealed class ControlSpecDuplicateCheckerTests
{
    private static ControlSpecEntry Spec(short row, short column, params short[] seikdno)
    {
        var e = new ControlSpecEntry
        {
            DescriptionRow = row,
            DescriptionColumn = column,
        };
        e.ControlTargetSequenceNumbers.AddRange(seikdno);
        return e;
    }

    [Fact]
    public void 重複が無ければnull()
    {
        var specs = new List<ControlSpecEntry>
        {
            Spec(1, 1, 10, 11),
            Spec(2, 1, 12, 13),
        };

        Assert.Null(ControlSpecDuplicateChecker.CheckDuplicateControlTargets(specs));
    }

    [Fact]
    public void 別エントリ間で追番が重複すればFY904E()
    {
        var specs = new List<ControlSpecEntry>
        {
            Spec(2, 3, 5),
            Spec(4, 6, 5),
        };

        CircuitParseError? err = ControlSpecDuplicateChecker.CheckDuplicateControlTargets(specs);

        Assert.NotNull(err);
        Assert.Equal("FY-904E", err!.ErrorCode);
        Assert.Equal("FYMEE80", err.MessageId);
    }

    [Fact]
    public void 重複時は記述行桁の小さい側を報告する()
    {
        var specs = new List<ControlSpecEntry>
        {
            Spec(2, 3, 5),
            Spec(1, 9, 5),
        };

        CircuitParseError? err = ControlSpecDuplicateChecker.CheckDuplicateControlTargets(specs);

        Assert.NotNull(err);
        // 追番→記述行→記述桁の昇順整列後、隣接の先頭(記述行=1,桁=9)を報告。
        Assert.Equal(1, err!.LineNumber);
        Assert.Equal(9, err.Column);
    }

    [Fact]
    public void 同一エントリ内の追番重複も検出する()
    {
        var specs = new List<ControlSpecEntry>
        {
            Spec(3, 4, 7, 7),
        };

        CircuitParseError? err = ControlSpecDuplicateChecker.CheckDuplicateControlTargets(specs);

        Assert.NotNull(err);
        Assert.Equal(3, err!.LineNumber);
        Assert.Equal(4, err.Column);
    }

    [Fact]
    public void ゼロ終端以降は無視する()
    {
        var specs = new List<ControlSpecEntry>
        {
            Spec(1, 1, 5, 0, 5),   // 2 個目の 5 は 0 終端後で無視される
        };

        Assert.Null(ControlSpecDuplicateChecker.CheckDuplicateControlTargets(specs));
    }

    [Fact]
    public void 空テーブルはnull()
    {
        Assert.Null(ControlSpecDuplicateChecker.CheckDuplicateControlTargets(new List<ControlSpecEntry>()));
    }

    [Fact]
    public void 追番が1件のみならnull()
    {
        var specs = new List<ControlSpecEntry>
        {
            Spec(1, 1, 5),
        };

        Assert.Null(ControlSpecDuplicateChecker.CheckDuplicateControlTargets(specs));
    }
}
