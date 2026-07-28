using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 電気パラメータのマージ(ep[2]ベース + ep[0]上書き)の検証。
/// 【C原典】Fysk0c_Edit_Epara(toku/sekkei/src/Fysk0c.c:66)。
/// </summary>
public sealed class ElectricalParameterMergerTests
{
    [Fact]
    public void Merge_own無入力なら全てupperを保持する()
    {
        var upper = new NumericElectricalParameters
        {
            At = 100,
            Af = 225,
            P = 3,
            V2Kbn = 'A',
        };
        upper.V2[0] = 200;
        var own = new NumericElectricalParameters();   // 全て入力なし(数値0・区分' ')

        NumericElectricalParameters wep = ElectricalParameterMerger.Merge(own, upper);

        Assert.Equal(100, wep.At);
        Assert.Equal(225, wep.Af);
        Assert.Equal(3, wep.P);
        Assert.Equal(200, wep.V2[0]);
        Assert.Equal('A', wep.V2Kbn);
    }

    [Fact]
    public void Merge_ownが入力を持つフィールドを上書きする()
    {
        var upper = new NumericElectricalParameters { At = 100, Af = 225, P = 3 };
        var own = new NumericElectricalParameters { At = 50 };   // At のみ入力

        NumericElectricalParameters wep = ElectricalParameterMerger.Merge(own, upper);

        Assert.Equal(50, wep.At);    // own で上書き
        Assert.Equal(225, wep.Af);   // own 未入力 → upper 保持
        Assert.Equal(3, wep.P);      // own 未入力 → upper 保持
    }

    [Fact]
    public void Merge_区分文字は空白以外のときだけ上書きする()
    {
        var upper = new NumericElectricalParameters { V2Kbn = 'A', VcKbn = 'A' };
        var own = new NumericElectricalParameters { V2Kbn = 'D', VcKbn = ' ' };

        NumericElectricalParameters wep = ElectricalParameterMerger.Merge(own, upper);

        Assert.Equal('D', wep.V2Kbn);   // own='D'(空白以外) → 上書き
        Assert.Equal('A', wep.VcKbn);   // own=' '(空白) → upper 保持
    }

    [Fact]
    public void Merge_QtyとBnはマージ対象外でupperを保持する()
    {
        var upper = new NumericElectricalParameters { Qty = 5, Bn = 'X' };
        var own = new NumericElectricalParameters { Qty = 9, Bn = 'Y' };

        NumericElectricalParameters wep = ElectricalParameterMerger.Merge(own, upper);

        Assert.Equal(5, wep.Qty);      // own を無視し upper 保持
        Assert.Equal('X', wep.Bn);     // own を無視し upper 保持
    }

    [Fact]
    public void Merge_配列フィールドを要素単位で扱う()
    {
        var upper = new NumericElectricalParameters();
        upper.Ma[0] = 10;
        upper.Ma[1] = 20;
        var own = new NumericElectricalParameters();
        own.Ma[1] = 99;   // Ma[1] のみ入力

        NumericElectricalParameters wep = ElectricalParameterMerger.Merge(own, upper);

        Assert.Equal(10, wep.Ma[0]);   // own 未入力 → upper
        Assert.Equal(99, wep.Ma[1]);   // own で上書き
    }

    [Fact]
    public void Merge_結果は入力から独立している()
    {
        var upper = new NumericElectricalParameters { At = 100 };
        upper.V2[0] = 200;
        var own = new NumericElectricalParameters();

        NumericElectricalParameters wep = ElectricalParameterMerger.Merge(own, upper);
        wep.At = 999;
        wep.V2[0] = 888;

        Assert.Equal(100, upper.At);      // upper は不変
        Assert.Equal(200, upper.V2[0]);   // 配列も独立
    }
}
