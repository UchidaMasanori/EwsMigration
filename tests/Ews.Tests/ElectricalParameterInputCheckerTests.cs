using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 電気パラメータ入力有無チェック(<see cref="ElectricalParameterInputChecker"/>)の移植検証。
/// 【C原典】Fysk0a_EparInput_Check(Fysk0a.c:71)。
/// </summary>
public sealed class ElectricalParameterInputCheckerTests
{
    [Fact]
    public void 入力なしはepno2で全フラグ0()
    {
        ElectricalParameterInput result = ElectricalParameterInputChecker.Check(new NumericElectricalParameters());

        Assert.Equal(2, result.ParameterNumber);
        Assert.Equal(0, result.InputFlags[0]);
    }

    [Fact]
    public void フレーム電流入力はsfg8と総括フラグを立てepno1()
    {
        var p = new NumericElectricalParameters { Af = 100.0 };

        ElectricalParameterInput result = ElectricalParameterInputChecker.Check(p);

        Assert.Equal(1, result.InputFlags[8]);
        Assert.Equal(1, result.InputFlags[0]);
        Assert.Equal(1, result.ParameterNumber);
    }

    [Fact]
    public void 電流系でない入力はepno2のまま総括フラグは立つ()
    {
        var p = new NumericElectricalParameters { Va = 200.0 };

        ElectricalParameterInput result = ElectricalParameterInputChecker.Check(p);

        Assert.Equal(1, result.InputFlags[13]);
        Assert.Equal(1, result.InputFlags[0]);
        Assert.Equal(2, result.ParameterNumber);
    }

    [Theory]
    [InlineData("At")]
    [InlineData("A1")]
    [InlineData("A2")]
    [InlineData("W1")]
    public void 電流系入力はepno1になる(string field)
    {
        var p = new NumericElectricalParameters();
        switch (field)
        {
            case "At": p.At = 50.0; break;
            case "A1": p.A1 = 50.0; break;
            case "A2": p.A2 = 50.0; break;
            case "W1": p.W1 = 50.0; break;
        }

        ElectricalParameterInput result = ElectricalParameterInputChecker.Check(p);

        Assert.Equal(1, result.ParameterNumber);
    }

    [Fact]
    public void 感度電流MA0入力はepno1になる()
    {
        var p = new NumericElectricalParameters();
        p.Ma[0] = 30.0;

        ElectricalParameterInput result = ElectricalParameterInputChecker.Check(p);

        Assert.Equal(1, result.InputFlags[16]);
        Assert.Equal(1, result.ParameterNumber);
    }

    [Fact]
    public void 電圧区分入力はsfg27を立てるがepno2()
    {
        var p = new NumericElectricalParameters { V2Kbn = 'A' };

        ElectricalParameterInput result = ElectricalParameterInputChecker.Check(p);

        Assert.Equal(1, result.InputFlags[27]);
        Assert.Equal(2, result.ParameterNumber);
    }

    [Fact]
    public void 相数線式の2要素目はsfg51と52に対応する()
    {
        var p = new NumericElectricalParameters();
        p.Ph2[1] = 3.0;
        p.Wr2[1] = 2.0;

        ElectricalParameterInput result = ElectricalParameterInputChecker.Check(p);

        Assert.Equal(1, result.InputFlags[51]);
        Assert.Equal(1, result.InputFlags[52]);
    }
}
