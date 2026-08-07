namespace Ews.Tests;

using Ews.Analysis;
using Ews.Domain.Masters;
using Xunit;

/// <summary>
/// <see cref="InverterOptionRadioNoiseSearchKey"/>(=Fysk01_Kiki_Set_INV_OP_teikaku_RN)の移植テスト。
/// </summary>
public sealed class InverterOptionRadioNoiseSearchKeyTests
{
    [Fact]
    public void 予約語とメーカーコードを固定値で設定する()
    {
        var cdata = new NearestRankReference();

        InverterOptionRadioNoiseSearchKey.Apply(cdata);

        Assert.Equal("PT      ", cdata.ReservedWord);
        Assert.Equal("M  ", cdata.MakerCode);
    }

    [Fact]
    public void パラメータタイプを全枠空白で設定する()
    {
        var cdata = new NearestRankReference();

        InverterOptionRadioNoiseSearchKey.Apply(cdata);

        Assert.Equal(7, cdata.ParameterTypes.Count);
        Assert.All(cdata.ParameterTypes, t => Assert.Equal("       ", t));
    }

    [Fact]
    public void 定格キーを品名FRBIFの80桁空白埋めで設定する()
    {
        var cdata = new NearestRankReference();

        InverterOptionRadioNoiseSearchKey.Apply(cdata);

        Assert.Equal(80, cdata.EquipmentMasterRatingKey.Length);
        Assert.Equal("FR-BIF", cdata.EquipmentMasterRatingKey.TrimEnd());
        Assert.StartsWith("FR-BIF", cdata.EquipmentMasterRatingKey);
    }

    [Fact]
    public void 既存の予約語を上書きする()
    {
        var cdata = new NearestRankReference { ReservedWord = "XX      " };

        InverterOptionRadioNoiseSearchKey.Apply(cdata);

        Assert.Equal("PT      ", cdata.ReservedWord);
    }
}
