using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 親回路・入力パラメータから下流回路の主回路パラメータを決定する変換テーブル
/// 照合ユーティリティの検証。【C原典】toku/sekkei/src/Fyss14.c:1437 mcprmcnv。
/// </summary>
public sealed class MainCircuitParameterConverterTests
{
    private static MainCircuitParameter Prm(
        short ph, short wr, short p, short v0, short v1, short v2, short vkbn = 0)
    {
        var prm = new MainCircuitParameter { Phase = ph, WireType = wr, Pole = p, AcDcKind = vkbn };
        prm.Voltage[0] = v0;
        prm.Voltage[1] = v1;
        prm.Voltage[2] = v2;
        return prm;
    }

    private static void AssertPrm(
        MainCircuitParameter actual,
        short ph, short wr, short p, short v0, short v1, short v2, short vkbn)
    {
        Assert.Equal(ph, actual.Phase);
        Assert.Equal(wr, actual.WireType);
        Assert.Equal(p, actual.Pole);
        Assert.Equal(v0, actual.Voltage[0]);
        Assert.Equal(v1, actual.Voltage[1]);
        Assert.Equal(v2, actual.Voltage[2]);
        Assert.Equal(vkbn, actual.AcDcKind);
    }

    [Fact]
    public void 電圧テーブル_全電圧指定は下流も同一構成で親電圧を保持する()
    {
        // 【C原典】電圧テーブル先頭行 {{3,4,4,{1,1,1},0},{0,0,0,{0,0,0},0},{3,4,4,{1,1,1},0}}。
        var parent = Prm(3, 4, 4, 440, 220, 105);
        var input = Prm(0, 0, 0, 0, 0, 0);
        var output = new MainCircuitParameter();

        int rc = MainCircuitParameterConverter.ConvertParameter(parent, input, output);

        Assert.Equal(0, rc);
        AssertPrm(output, 3, 4, 4, 440, 220, 105, 0);
    }

    [Fact]
    public void 電圧テーブル_出力電圧は入力ではなく親の実電圧を採用する()
    {
        // 【C原典】{{3,4,4,{0,1,1},0},{0,0,0,{0,1,0},0},{1,4,4,{0,1,0},0}}。
        // 入力電圧(200)ではなく親電圧(210)が採用され、to の指定フラグ位置にのみ格納される。
        var parent = Prm(3, 4, 4, 0, 210, 105);
        var input = Prm(0, 0, 0, 0, 200, 0);
        var output = new MainCircuitParameter();

        int rc = MainCircuitParameterConverter.ConvertParameter(parent, input, output);

        Assert.Equal(0, rc);
        AssertPrm(output, 1, 4, 4, 0, 210, 0, 0);
    }

    [Fact]
    public void 極数テーブル_入力極数で下流構成を決定する()
    {
        // 【C原典】極数テーブル {{3,4,4,{0,1,1},0},{0,0,4,{0,1,0},0},{1,2,2,{0,1,0},0}}。
        var parent = Prm(3, 4, 4, 0, 210, 105);
        var input = Prm(0, 0, 4, 0, 200, 0);
        var output = new MainCircuitParameter();

        int rc = MainCircuitParameterConverter.ConvertParameter(parent, input, output);

        Assert.Equal(0, rc);
        AssertPrm(output, 1, 2, 2, 0, 210, 0, 0);
    }

    [Fact]
    public void 極数テーブル_梶川追加行は親の第2第3電圧を下流へ展開する()
    {
        // 【C原典】{{3,4,4,{1,1,1},0},{0,0,3,{0,0,1},0},{1,3,3,{0,1,1},0}}(1995.11.21 add)。
        // 3P LV=100V -> 1P3W210/105V 相当。to のフラグ {0,1,1} で親 v[1]/v[2] を展開。
        var parent = Prm(3, 4, 4, 440, 220, 105);
        var input = Prm(0, 0, 3, 0, 0, 100);
        var output = new MainCircuitParameter();

        int rc = MainCircuitParameterConverter.ConvertParameter(parent, input, output);

        Assert.Equal(0, rc);
        AssertPrm(output, 1, 3, 3, 0, 220, 105, 0);
    }

    [Fact]
    public void 線式テーブル_入力線式で下流構成を決定する()
    {
        // 【C原典】線式テーブル先頭行 {{3,4,4,{1,1,1},0},{3,4,0,{1,1,1},0},{3,4,4,{1,1,1},0}}。
        var parent = Prm(3, 4, 4, 440, 220, 105);
        var input = Prm(3, 4, 0, 1, 1, 1);
        var output = new MainCircuitParameter();

        int rc = MainCircuitParameterConverter.ConvertParameter(parent, input, output);

        Assert.Equal(0, rc);
        AssertPrm(output, 3, 4, 4, 440, 220, 105, 0);
    }

    [Fact]
    public void DC補正_親がDCなら相線式極を1P2W2Pに強制して照合する()
    {
        // 【C原典】DC補正 if(tp.vkbn){ tp.ph=1; tp.wr=2; tp.p=2; ti.vkbn=1; }。
        // 親 ph/wr/p が {3,4,4} でも DC(vkbn!=0)なら {1,2,2} に強制され DC 行に一致する。
        // 一致行 {{1,2,2,{0,0,1},1},{0,0,0,{0,0,0},1},{1,2,2,{0,0,1},1}}。
        var parent = Prm(3, 4, 4, 0, 0, 100, 1);
        var input = Prm(0, 0, 0, 0, 0, 0);
        var output = new MainCircuitParameter();

        int rc = MainCircuitParameterConverter.ConvertParameter(parent, input, output);

        Assert.Equal(0, rc);
        AssertPrm(output, 1, 2, 2, 0, 0, 100, 1);
    }

    [Fact]
    public void 非該当_マイナス1を返し出力を変更しない()
    {
        // 【C原典】終端到達 → return -1。oprm は if 内でのみ書き込まれるため不変。
        var parent = Prm(9, 9, 9, 1, 1, 1);
        var input = Prm(0, 0, 0, 0, 0, 0);
        var output = Prm(7, 7, 7, 11, 22, 33, 5);

        int rc = MainCircuitParameterConverter.ConvertParameter(parent, input, output);

        Assert.Equal(-1, rc);
        AssertPrm(output, 7, 7, 7, 11, 22, 33, 5); // 不変
    }

    [Fact]
    public void 入力パラメータは破壊的に更新されない()
    {
        // C の memcpy によるキー生成は一時データに対して行われ、iprm 本体は不変。
        var parent = Prm(3, 4, 4, 440, 220, 105);
        var input = Prm(0, 0, 4, 0, 200, 0);
        var output = new MainCircuitParameter();

        MainCircuitParameterConverter.ConvertParameter(parent, input, output);

        AssertPrm(input, 0, 0, 4, 0, 200, 0, 0);
    }
}
