using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 形状タイプ変換一覧作成(予約語別の type_tbl2 展開)の検証。
/// 【C原典】Fysk01_Type_Check2(toku/sekkei/src/Fysk01.c:3224)。
/// </summary>
public sealed class ShapeTypeCheckerTests
{
    /// <summary>7 枠のデータタイプ配列を作り、指定位置に値を設定する。</summary>
    private static string[] DataTypes(params (int Index, string Value)[] entries)
    {
        string[] dt = new string[7];
        for (int i = 0; i < dt.Length; i++)
        {
            dt[i] = new string(' ', 7);
        }
        foreach ((int index, string value) in entries)
        {
            dt[index] = value;
        }
        return dt;
    }

    [Fact]
    public void ELBのTLAはTLAとNTへ展開される()
    {
        ShapeTypeResult r = ShapeTypeChecker.ResolveShapeTypes("ELB ", DataTypes((1, "TLA")));

        Assert.Equal(1, r.Position);
        Assert.Equal(new[] { "TLA    ", "NT     " }, r.Types);
    }

    [Fact]
    public void ELBのNTはNT単独へ展開される()
    {
        ShapeTypeResult r = ShapeTypeChecker.ResolveShapeTypes("ELB ", DataTypes((1, "NT ")));

        Assert.Equal(1, r.Position);
        Assert.Equal(new[] { "NT     " }, r.Types);
    }

    [Fact]
    public void ELBのシンボル未ヒットは当該データタイプをそのまま採用する()
    {
        ShapeTypeResult r = ShapeTypeChecker.ResolveShapeTypes("ELB ", DataTypes((1, "XYZ")));

        Assert.Equal(1, r.Position);
        Assert.Equal(new[] { "XYZ    " }, r.Types);
    }

    [Fact]
    public void THRの空白データタイプは1A1Bと1Cへ展開される()
    {
        ShapeTypeResult r = ShapeTypeChecker.ResolveShapeTypes("THR ", DataTypes());

        Assert.Equal(1, r.Position);
        Assert.Equal(new[] { "1A1B   ", "1C     " }, r.Types);
    }

    [Fact]
    public void MGFRは位置4のデータタイプを参照する()
    {
        // ichi=4。位置4を空白にして "   " シンボルにヒットさせる。
        ShapeTypeResult r = ShapeTypeChecker.ResolveShapeTypes("MGFR ", DataTypes((4, "   ")));

        Assert.Equal(4, r.Position);
        Assert.Equal(new[] { "1A1B   ", "1C     " }, r.Types);
    }

    [Fact]
    public void CTのKTは3タイプへ展開される()
    {
        ShapeTypeResult r = ShapeTypeChecker.ResolveShapeTypes("CT  ", DataTypes((1, "KT ")));

        Assert.Equal(1, r.Position);
        Assert.Equal(new[] { "KT     ", "LT     ", "KE     " }, r.Types);
    }

    [Fact]
    public void TBの空白は3タイプへ展開される()
    {
        ShapeTypeResult r = ShapeTypeChecker.ResolveShapeTypes("TB  ", DataTypes());

        Assert.Equal(1, r.Position);
        Assert.Equal(new[] { "BT     ", "RT     ", "LTG    " }, r.Types);
    }

    [Fact]
    public void テーブル未登録予約語は位置1のデータタイプをそのまま採用する()
    {
        ShapeTypeResult r = ShapeTypeChecker.ResolveShapeTypes("ZZZ ", DataTypes((1, "ABC")));

        Assert.Equal(1, r.Position);
        Assert.Equal(new[] { "ABC    " }, r.Types);
    }

    [Fact]
    public void STMのNOTHINGは4タイプへ並べ替えられる()
    {
        ShapeTypeResult r = ShapeTypeChecker.ResolveShapeTypes("STM ", DataTypes((1, "NOTHING")));

        Assert.Equal(new[] { "NOTHING", "FC     ", "1C     ", "2C     " }, r.Types);
    }

    [Fact]
    public void STMの空白は空白始まりの4タイプへ並べ替えられる()
    {
        ShapeTypeResult r = ShapeTypeChecker.ResolveShapeTypes("STM ", DataTypes());

        Assert.Equal(new[] { "       ", "FC     ", "1C     ", "2C     " }, r.Types);
    }

    [Fact]
    public void STMの1Cは1C_2C_FCの順へ並べ替えられる()
    {
        ShapeTypeResult r = ShapeTypeChecker.ResolveShapeTypes("STM ", DataTypes((1, "1C     ")));

        Assert.Equal(new[] { "1C     ", "2C     ", "FC     " }, r.Types);
    }

    [Fact]
    public void STMの2Cは2C_FC_1Cの順へ並べ替えられる()
    {
        ShapeTypeResult r = ShapeTypeChecker.ResolveShapeTypes("STM ", DataTypes((1, "2C     ")));

        Assert.Equal(new[] { "2C     ", "FC     ", "1C     " }, r.Types);
    }
}
