using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 整形済み固定長文字列(eparmg)から数値(eparmg_s)への変換の検証。
/// 【C原典】Fysk01_Change_Epara(toku/sekkei/src/Fysk01.c:4108)。
/// </summary>
public sealed class ElectricalParameterConverterTests
{
    [Fact]
    public void Convert_全ゼロ入力なら全数値フィールドが0になる()
    {
        var source = new ElectricalParameters();   // 既定は全フィールド '0' 埋め

        NumericElectricalParameters result = ElectricalParameterConverter.Convert(source);

        Assert.Equal(0, result.Ph1);
        Assert.Equal(0, result.At);
        Assert.Equal(0, result.Af);
        Assert.Equal(0, result.W1);
        Assert.Equal(0, result.Sset);
        Assert.Equal(0, result.C2);
        Assert.All(result.Ph2, v => Assert.Equal(0, v));
        Assert.All(result.Wr2, v => Assert.Equal(0, v));
        Assert.All(result.Ma, v => Assert.Equal(0, v));
        Assert.All(result.V1, v => Assert.Equal(0, v));
        Assert.All(result.V2, v => Assert.Equal(0, v));
        Assert.Equal(0, result.V1Idx);
        Assert.Equal((char)0, result.V2Idx);
    }

    [Fact]
    public void Convert_数値文字列を先頭size桁でatofする()
    {
        var source = new ElectricalParameters
        {
            Af = "000000225",   // 9桁 -> 225
            At = "000000100",   // 9桁 -> 100
            P = "003",          // 3桁 -> 3
            Hz = "60",          // 2桁 -> 60
            W1 = "0000012.34",  // 10桁 -> 12.34
            Kvar = "005.50",    // 6桁 -> 5.5
        };

        NumericElectricalParameters result = ElectricalParameterConverter.Convert(source);

        Assert.Equal(225, result.Af);
        Assert.Equal(100, result.At);
        Assert.Equal(3, result.P);
        Assert.Equal(60, result.Hz);
        Assert.Equal(12.34, result.W1, 3);
        Assert.Equal(5.5, result.Kvar, 3);
    }

    [Fact]
    public void Convert_区分文字は原典どおりそのまま複写する()
    {
        var source = new ElectricalParameters
        {
            V2Kbn = 'A',
            VcKbn = 'D',
            Bn = '5',
        };

        NumericElectricalParameters result = ElectricalParameterConverter.Convert(source);

        Assert.Equal('A', result.V2Kbn);
        Assert.Equal('D', result.VcKbn);
        Assert.Equal('5', result.Bn);
    }

    [Fact]
    public void Convert_V2Idxはdouble値を整数へ切り捨ててcharに格納する()
    {
        var source = new ElectricalParameters
        {
            V1Idx = "2",   // double フィールド -> 2.0
            V2Idx = "3",   // char フィールド   -> (char)3(生値。'3'=51 ではない)
        };

        NumericElectricalParameters result = ElectricalParameterConverter.Convert(source);

        Assert.Equal(2, result.V1Idx);
        Assert.Equal((char)3, result.V2Idx);
    }

    [Fact]
    public void Convert_char型フィールド_Qty_C_Ksu_を1桁数値化する()
    {
        var source = new ElectricalParameters
        {
            Qty = '2',
            C = '3',
            Ksu = '4',
        };

        NumericElectricalParameters result = ElectricalParameterConverter.Convert(source);

        Assert.Equal(2, result.Qty);
        Assert.Equal(3, result.C);
        Assert.Equal(4, result.Ksu);
    }

    [Fact]
    public void Convert_配列フィールドを要素単位で数値化する()
    {
        var source = new ElectricalParameters();
        source.Ph2[0] = "1";
        source.Ph2[1] = "2";
        source.Ma[0] = "0100";
        source.Ma[3] = "0400";       // epama[3] まで(eparmg_s は [3] 宣言だが j<4 で書き込む)
        source.V1[0] = "00006600";   // 6600
        source.V2[2] = "00000210";   // 210

        NumericElectricalParameters result = ElectricalParameterConverter.Convert(source);

        Assert.Equal(1, result.Ph2[0]);
        Assert.Equal(2, result.Ph2[1]);
        Assert.Equal(100, result.Ma[0]);
        Assert.Equal(400, result.Ma[3]);
        Assert.Equal(6600, result.V1[0]);
        Assert.Equal(210, result.V2[2]);
    }

    [Fact]
    public void Convert_変換結果はマージのベースとして利用できる()
    {
        // 上位(ep[2])を変換 -> 自機(ep[0])を変換 -> マージ、の一連が破綻しないことを確認。
        var upperChar = new ElectricalParameters { At = "000000225", Af = "000000225" };
        var ownChar = new ElectricalParameters { At = "000000100" };

        NumericElectricalParameters upper = ElectricalParameterConverter.Convert(upperChar);
        NumericElectricalParameters own = ElectricalParameterConverter.Convert(ownChar);
        NumericElectricalParameters merged = ElectricalParameterMerger.Merge(own, upper);

        Assert.Equal(100, merged.At);    // 自機に入力あり -> 上書き
        Assert.Equal(225, merged.Af);    // 自機に入力なし -> 上位を保持
    }
}
