namespace Ews.Tests;

using Ews.Domain.Masters;
using Xunit;

/// <summary>
/// <see cref="HeatResistantBoxMasterKey"/>(=Fysk01_Kiki_Read_TainetuBOX のキー生成部)の移植テスト。
/// </summary>
public sealed class HeatResistantBoxMasterKeyTests
{
    [Fact]
    public void 予約語とメーカーコードは固定値()
    {
        Assert.Equal("PT", HeatResistantBoxMasterKey.ReservedWord);
        Assert.Equal("K", HeatResistantBoxMasterKey.MakerCode);
    }

    [Fact]
    public void 定格キーは機器品名をそのまま返す()
    {
        Assert.Equal("TB2P60A+(F1+BOX)", HeatResistantBoxMasterKey.RatingKeyFor("TB2P60A+(F1+BOX)"));
    }

    [Fact]
    public void 定格キーの最大長は80()
    {
        Assert.Equal(80, HeatResistantBoxMasterKey.RatingKeyLength);
    }

    [Fact]
    public void 機器品名が80文字を超える場合は80文字で切り詰める()
    {
        string name = new('A', 100);
        string key = HeatResistantBoxMasterKey.RatingKeyFor(name);
        Assert.Equal(80, key.Length);
        Assert.Equal(new string('A', 80), key);
    }

    [Fact]
    public void 機器品名がちょうど80文字ならそのまま返す()
    {
        string name = new('B', 80);
        Assert.Equal(name, HeatResistantBoxMasterKey.RatingKeyFor(name));
    }

    [Fact]
    public void 空文字の機器品名は空文字を返す()
    {
        Assert.Equal(string.Empty, HeatResistantBoxMasterKey.RatingKeyFor(string.Empty));
    }
}
