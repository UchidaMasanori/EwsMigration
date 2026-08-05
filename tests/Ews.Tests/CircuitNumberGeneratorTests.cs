using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CircuitNumberGenerator"/>
/// (【C原典】Find_Kairo_Bangou + 静的 struct BANGOU Kbangoua / PM_flg、
/// toku/sekkei/src/Fyss14.c:4508)の単体テスト。
/// 並び替え区分 narakbn '3'/'4' で回路分類(kairobun)別カウンタを進め、それ以外は現在値を返す。
/// 回路分類 M は相を区別せず Mno1、行種 PM は Mno1 を戻す(PM_flg)、
/// B 系(gyocd="B")かつ制御電源 F は採番後に戻す挙動を検証する。
/// </summary>
public class CircuitNumberGeneratorTests
{
    /// <summary>採番対象要素を 1 件生成する。</summary>
    private static MainCircuitData Rec(
        char narakbn = '3',
        char kairobun = ' ',
        string gyocd = "",
        string yoyaku = "",
        string fpac = "  ")
    {
        var r = new MainCircuitResult();
        MainCircuitData d = r.Data;
        d.SortKind = narakbn;
        d.CircuitClass = kairobun;
        d.LineTypeCode = gyocd;
        d.ReservedWord = yoyaku;
        d.AttachedParameter.ControlPowerNumber = fpac;
        return d;
    }

    [Fact]
    public void 回路分類Mは相を区別せず連番する()
    {
        var gen = new CircuitNumberGenerator();

        Assert.Equal(1, gen.Find(Rec(kairobun: 'M'), '1'));
        Assert.Equal(2, gen.Find(Rec(kairobun: 'M'), '3')); // 相が違っても同じ Mno1 系
        Assert.Equal(3, gen.Find(Rec(kairobun: 'M'), '1'));
    }

    [Fact]
    public void 行種PMはMno1を戻し次のMと同番号になる()
    {
        var gen = new CircuitNumberGenerator();

        Assert.Equal(1, gen.Find(Rec(kairobun: 'M', gyocd: "PM"), '1')); // PM: 1 を返し Mno1 を 0 に戻す
        Assert.Equal(1, gen.Find(Rec(kairobun: 'M'), '1'));              // 次の M も 1(同番号)
    }

    [Fact]
    public void narakbn以外の既存参照は現在値を返す()
    {
        var gen = new CircuitNumberGenerator();
        gen.Find(Rec(kairobun: 'B'), '1'); // Bno1=1

        Assert.Equal(1, gen.Find(Rec(narakbn: '1', kairobun: 'B'), '1')); // カウントアップせず現在値
        Assert.Equal(1, gen.Find(Rec(narakbn: '1', kairobun: 'B'), '1'));
    }

    [Fact]
    public void PM採番後の既存M参照はMno1に1加算した値を返す()
    {
        var gen = new CircuitNumberGenerator();
        Assert.Equal(1, gen.Find(Rec(kairobun: 'M', gyocd: "PM"), '1')); // PM_flg=1, Mno1=0 に戻る

        Assert.Equal(1, gen.Find(Rec(narakbn: '1', kairobun: 'M'), '1')); // Mno1(0)+1
    }

    [Fact]
    public void 相1と相3でB系カウンタが別になる()
    {
        var gen = new CircuitNumberGenerator();

        Assert.Equal(1, gen.Find(Rec(kairobun: 'B'), '1')); // Bno1
        Assert.Equal(1, gen.Find(Rec(kairobun: 'B'), '3')); // Bno3(別カウンタ)
        Assert.Equal(2, gen.Find(Rec(kairobun: 'B'), '1')); // Bno1
    }

    [Fact]
    public void S系カウンタは相を共有する()
    {
        var gen = new CircuitNumberGenerator();

        Assert.Equal(1, gen.Find(Rec(kairobun: 'S'), '1'));
        Assert.Equal(2, gen.Find(Rec(kairobun: 'S'), '3')); // 同一 Sno を共有
    }

    [Fact]
    public void 空分類でgyocdがBのときN系を採番する()
    {
        var gen = new CircuitNumberGenerator();

        Assert.Equal(1, gen.Find(Rec(kairobun: ' ', gyocd: "B"), '1')); // Nno1
        Assert.Equal(2, gen.Find(Rec(kairobun: ' ', gyocd: "B"), '1'));
    }

    [Fact]
    public void 制御電源のFヒューズはN系を進めず戻す()
    {
        var gen = new CircuitNumberGenerator();

        // 制御電源 F(fpac 有り): 採番後に戻すため現在値のまま。
        Assert.Equal(1, gen.Find(Rec(kairobun: ' ', gyocd: "B", yoyaku: "F", fpac: "01"), '1'));
        // 続く通常 B は Nno1 が進んでいない前提で 1。
        Assert.Equal(1, gen.Find(Rec(kairobun: ' ', gyocd: "B"), '1'));
    }

    [Fact]
    public void 空分類でgyocdがB以外なら0を返す()
    {
        var gen = new CircuitNumberGenerator();

        Assert.Equal(0, gen.Find(Rec(kairobun: ' ', gyocd: "M"), '1'));
    }
}
