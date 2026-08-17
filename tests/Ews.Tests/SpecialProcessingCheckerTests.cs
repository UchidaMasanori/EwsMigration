using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// WH/CT/TS の特殊処理チェック(<see cref="SpecialProcessingChecker.Check"/>)の移植テスト。
/// 【C原典】Fysk0c_Check_Tokusyu(Fysk0c.c:146)。
/// </summary>
public sealed class SpecialProcessingCheckerTests
{
    private static string[] Types(params string[] values)
    {
        var t = new string[7];
        for (int i = 0; i < 7; i++)
        {
            t[i] = i < values.Length ? values[i] : "";
        }
        return t;
    }

    private static AttachedParameters Fp(char spkvn = ' ', char fpahu = ' ', char fpamh = ' ')
        => new() { SpFutureMountKind = spkvn, SealKind = fpahu, MeterSealKind = fpamh };

    [Fact]
    public void WHはSP枠かつ封印なら特殊処理しタイプ2を出力する()
    {
        string[] dtype = Types("", "", "WHM", "", "");

        SpecialProcessingResult r = SpecialProcessingChecker.Check(
            "WH ", dtype, Fp(spkvn: '1', fpahu: 'H'));

        Assert.Equal(1, r.Flag);
        Assert.Equal("WHM".PadRight(7), r.ShapeTypes[2]);
    }

    [Fact]
    public void WHはSP枠かつメータ封印なら特殊処理する()
    {
        string[] dtype = Types("", "", "WHM", "", "");

        SpecialProcessingResult r = SpecialProcessingChecker.Check(
            "WH ", dtype, Fp(spkvn: '1', fpamh: 'M'));

        Assert.Equal(1, r.Flag);
        Assert.Equal("WHM".PadRight(7), r.ShapeTypes[2]);
    }

    [Fact]
    public void WHのスマートメータは特殊処理しない()
    {
        string[] dtype = Types("", "", "WHM", "", "SM");

        SpecialProcessingResult r = SpecialProcessingChecker.Check(
            "WH ", dtype, Fp(spkvn: '1', fpahu: 'H'));

        Assert.Equal(0, r.Flag);
    }

    [Fact]
    public void WHでもSP枠でなければ特殊処理しない()
    {
        string[] dtype = Types("", "", "WHM", "", "");

        SpecialProcessingResult r = SpecialProcessingChecker.Check(
            "WH ", dtype, Fp(spkvn: ' ', fpahu: 'H'));

        Assert.Equal(0, r.Flag);
        Assert.Equal(new string(' ', 7), r.ShapeTypes[2]);
    }

    [Fact]
    public void CTはBOXタイプなら特殊処理しタイプ1を出力する()
    {
        string[] dtype = Types("", "BOX", "", "", "");

        SpecialProcessingResult r = SpecialProcessingChecker.Check("CT ", dtype, Fp());

        Assert.Equal(1, r.Flag);
        Assert.Equal("BOX".PadRight(7), r.ShapeTypes[1]);
    }

    [Fact]
    public void CTはBOX以外なら特殊処理しない()
    {
        string[] dtype = Types("", "PNL", "", "", "");

        SpecialProcessingResult r = SpecialProcessingChecker.Check("CT ", dtype, Fp());

        Assert.Equal(0, r.Flag);
    }

    [Fact]
    public void TSはSINタイプなら特殊処理しタイプ2を出力する()
    {
        string[] dtype = Types("", "", "SIN", "", "");

        SpecialProcessingResult r = SpecialProcessingChecker.Check("TS ", dtype, Fp());

        Assert.Equal(1, r.Flag);
        Assert.Equal("SIN".PadRight(7), r.ShapeTypes[2]);
    }

    [Fact]
    public void 対象外予約語は特殊処理せず全枠空白を返す()
    {
        string[] dtype = Types("A", "B", "C", "D", "E");

        SpecialProcessingResult r = SpecialProcessingChecker.Check(
            "MCB", dtype, Fp(spkvn: '1', fpahu: 'H'));

        Assert.Equal(0, r.Flag);
        for (int i = 0; i < 7; i++)
        {
            Assert.Equal(new string(' ', 7), r.ShapeTypes[i]);
        }
    }
}
