namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

/// <summary>
/// <see cref="InverterOptionSearchKey"/>(=Fysk01_Kiki_Set_INV_OP_teikaku)の移植テスト。
/// </summary>
public sealed class InverterOptionSearchKeyTests
{
    private static IReadOnlyList<InverterOptionConstant> Constants() =>
    [
        new InverterOptionConstant("T", 10.0, "OPT-10"),
        new InverterOptionConstant("T", 20.0, "OPT-20"),
        new InverterOptionConstant("T", 30.0, "OPT-30"),
    ];

    [Fact]
    public void 該当ありで予約語とメーカーコードを固定値で設定しGOODを返す()
    {
        var cdata = new NearestRankReference();

        int ret = InverterOptionSearchKey.Apply(cdata, Constants(), 5.0);

        Assert.Equal(InverterOptionSearchKey.Good, ret);
        Assert.Equal("PT      ", cdata.ReservedWord);
        Assert.Equal("M  ", cdata.MakerCode);
    }

    [Fact]
    public void パラメータタイプを全枠空白で設定する()
    {
        var cdata = new NearestRankReference();

        InverterOptionSearchKey.Apply(cdata, Constants(), 5.0);

        Assert.Equal(7, cdata.ParameterTypes.Count);
        Assert.All(cdata.ParameterTypes, t => Assert.Equal("       ", t));
    }

    [Fact]
    public void 入力kw以上となる最初の定格を80桁空白埋めで定格キーへ設定する()
    {
        var cdata = new NearestRankReference();

        // 入力 15kw → 直近上位は 20kw の OPT-20。
        int ret = InverterOptionSearchKey.Apply(cdata, Constants(), 15.0);

        Assert.Equal(InverterOptionSearchKey.Good, ret);
        Assert.Equal(80, cdata.EquipmentMasterRatingKey.Length);
        Assert.Equal("OPT-20", cdata.EquipmentMasterRatingKey.TrimEnd());
    }

    [Fact]
    public void 該当なしはNOGOODでcdataを変更しない()
    {
        var cdata = new NearestRankReference { ReservedWord = "XX      ", MakerCode = "Z  " };

        // 全コンスタントの kw を超える入力 → 該当なし。
        int ret = InverterOptionSearchKey.Apply(cdata, Constants(), 100.0);

        Assert.Equal(InverterOptionSearchKey.NoGood, ret);
        Assert.Equal("XX      ", cdata.ReservedWord);
        Assert.Equal("Z  ", cdata.MakerCode);
    }
}
