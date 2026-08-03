using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="TerminalBreakerSelector"/>(【C原典】Fyss3B_Breaker_Sentei,
/// toku/sekkei/src/Fyss3B.c)の単体テスト。
/// フラグ設定部(機器選定指示フラグ ksflg / 機器サーチフラグ kikisflg)の設定条件を検証する。
/// </summary>
public class TerminalBreakerSelectorTests
{
    /// <summary>
    /// 主回路データを 1 件生成する。
    /// </summary>
    private static MainCircuitResult Rec(
        string reservedWord,
        char equipmentSelectionKind = ' ',
        char externalMountKind = ' ',
        string loadKind = "M",
        char leadingFlag = ' ')
    {
        var r = new MainCircuitResult { SequenceNumber = "001" };
        r.Data.ReservedWord = reservedWord;
        r.Data.AttachedParameter.ExternalMountKind = externalMountKind;
        r.Data.AttachedParameter.LoadKind = loadKind;
        r.Work.EquipmentSelectionKind = equipmentSelectionKind;
        r.Work.LeadingEquipmentFlag = leadingFlag;
        return r;
    }

    private static void AssertFlags(MainCircuitResult r, char expected)
    {
        Assert.Equal(expected, r.Work.SelectionInstructionFlag);
        Assert.Equal(expected, r.Data.EquipmentSearchFlag);
    }

    [Theory]
    [InlineData("MCB")]
    [InlineData("ELB")]
    [InlineData("MMCB")]
    [InlineData("ELMB")]
    [InlineData("SB")]
    [InlineData("RMCB")]
    [InlineData("RELB")]
    [InlineData("RMMCB")]
    [InlineData("RELMB")]
    [InlineData("NHMB")]
    [InlineData("HPSB")]
    [InlineData("HSB")]
    [InlineData("CP")]
    [InlineData("CKS")]
    public void 機器選定区分1_外部取付なし_負荷種類あり_ブレーカ予約語はフラグを立てる(string word)
    {
        var r = Rec(word, equipmentSelectionKind: '1', externalMountKind: ' ', loadKind: "M");

        TerminalBreakerSelector.PrepareSelectionFlags([r]);

        AssertFlags(r, '1');
    }

    [Fact]
    public void MC予約語は第1条件から除外される()
    {
        // MC は負荷容量決定テーブルには存在するが、第1 if で明示的に除外される。
        var r = Rec("MC", equipmentSelectionKind: '1', externalMountKind: ' ', loadKind: "M", leadingFlag: ' ');

        TerminalBreakerSelector.PrepareSelectionFlags([r]);

        AssertFlags(r, ' ');
    }

    [Fact]
    public void MC予約語でも先頭機器フラグと外部取付なしならフラグを立てる()
    {
        // 第2 if(sentflg=='1' AND fpag==' ')は予約語 MC でも成立する。
        var r = Rec("MC", equipmentSelectionKind: ' ', externalMountKind: ' ', loadKind: "  ", leadingFlag: '1');

        TerminalBreakerSelector.PrepareSelectionFlags([r]);

        AssertFlags(r, '1');
    }

    [Fact]
    public void 負荷容量決定テーブル外の予約語はスキップされフラグは立たない()
    {
        var r = Rec("F", equipmentSelectionKind: '1', externalMountKind: ' ', loadKind: "M", leadingFlag: '1');

        TerminalBreakerSelector.PrepareSelectionFlags([r]);

        AssertFlags(r, ' ');
    }

    [Fact]
    public void 外部取付ありは両条件とも不成立でフラグは立たない()
    {
        var r = Rec("MCB", equipmentSelectionKind: '1', externalMountKind: 'G', loadKind: "M", leadingFlag: '1');

        TerminalBreakerSelector.PrepareSelectionFlags([r]);

        AssertFlags(r, ' ');
    }

    [Fact]
    public void 機器選定区分1でも負荷種類が空白なら第1条件は不成立()
    {
        var r = Rec("MCB", equipmentSelectionKind: '1', externalMountKind: ' ', loadKind: "  ", leadingFlag: ' ');

        TerminalBreakerSelector.PrepareSelectionFlags([r]);

        AssertFlags(r, ' ');
    }

    [Fact]
    public void 機器選定区分が1以外なら第1条件は不成立()
    {
        var r = Rec("MCB", equipmentSelectionKind: ' ', externalMountKind: ' ', loadKind: "M", leadingFlag: ' ');

        TerminalBreakerSelector.PrepareSelectionFlags([r]);

        AssertFlags(r, ' ');
    }

    [Fact]
    public void 先頭機器フラグと外部取付なしのみでフラグを立てる()
    {
        // 機器選定区分!='1'・負荷種類空白でも、第2 if だけで成立する。
        var r = Rec("MCB", equipmentSelectionKind: ' ', externalMountKind: ' ', loadKind: "  ", leadingFlag: '1');

        TerminalBreakerSelector.PrepareSelectionFlags([r]);

        AssertFlags(r, '1');
    }

    [Fact]
    public void 事前に立っていたフラグは条件不成立でクリアされる()
    {
        var r = Rec("F", equipmentSelectionKind: '1', externalMountKind: ' ', loadKind: "M", leadingFlag: '1');
        r.Work.SelectionInstructionFlag = '1';
        r.Data.EquipmentSearchFlag = '1';

        TerminalBreakerSelector.PrepareSelectionFlags([r]);

        AssertFlags(r, ' ');
    }

    [Fact]
    public void 複数レコードを個別に判定する()
    {
        var set = Rec("MCB", equipmentSelectionKind: '1', externalMountKind: ' ', loadKind: "M");
        var skip = Rec("F", equipmentSelectionKind: '1', externalMountKind: ' ', loadKind: "M", leadingFlag: '1');
        var lead = Rec("ELB", equipmentSelectionKind: ' ', externalMountKind: ' ', loadKind: "  ", leadingFlag: '1');

        TerminalBreakerSelector.PrepareSelectionFlags([set, skip, lead]);

        AssertFlags(set, '1');
        AssertFlags(skip, ' ');
        AssertFlags(lead, '1');
    }
}
