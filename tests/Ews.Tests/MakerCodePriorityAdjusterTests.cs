using System.Collections.Generic;
using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="MakerCodePriorityAdjuster"/>(【C原典】PropAdjustMakerCode)の単体テスト。
/// </summary>
public class MakerCodePriorityAdjusterTests
{
    [Fact]
    public void 保存値に無いコードを除去して前詰めする()
    {
        // priority: FT/F/K/OT のうち、保存値に F と K のみ存在 → F,K を前詰め
        IReadOnlyList<string> result = MakerCodePriorityAdjuster.RemoveUnlistedCodes(
            ["FT ", "F  ", "K  ", "OT "],
            ["F  ", "K  ", "   ", "   "]);

        Assert.Equal(["F  ", "K  ", "   ", "   "], result);
    }

    [Fact]
    public void 全て保存値に含まれる場合は順序維持()
    {
        IReadOnlyList<string> result = MakerCodePriorityAdjuster.RemoveUnlistedCodes(
            ["FT ", "F  ", "K  ", "OT "],
            ["OT ", "K  ", "F  ", "FT "]);

        Assert.Equal(["FT ", "F  ", "K  ", "OT "], result);
    }

    [Fact]
    public void 保存値に一致無しは全て空白()
    {
        IReadOnlyList<string> result = MakerCodePriorityAdjuster.RemoveUnlistedCodes(
            ["FT ", "F  ", "K  ", "OT "],
            ["M  ", "   ", "   ", "   "]);

        Assert.Equal(["   ", "   ", "   ", "   "], result);
    }

    [Fact]
    public void 桁不足の入力は右空白詰めで比較する()
    {
        IReadOnlyList<string> result = MakerCodePriorityAdjuster.RemoveUnlistedCodes(
            ["F", "K"],
            ["F  ", "   ", "   ", "   "]);

        Assert.Equal(["F  ", "   ", "   ", "   "], result);
    }

    [Fact]
    public void 常に4スロット固定を返す()
    {
        IReadOnlyList<string> result = MakerCodePriorityAdjuster.RemoveUnlistedCodes(
            ["M  "],
            ["M  ", "   ", "   ", "   "]);

        Assert.Equal(4, result.Count);
    }
}
