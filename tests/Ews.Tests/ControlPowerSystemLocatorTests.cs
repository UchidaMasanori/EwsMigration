using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="ControlPowerSystemLocator"/>(【C原典】Fyss1k.c の getCtlDenKno)の単体テスト。
/// </summary>
public sealed class ControlPowerSystemLocatorTests
{
    private static MainCircuitResult Main(string fpac, string kno)
    {
        var r = new MainCircuitResult();
        r.Data.AttachedParameter.ControlPowerNumber = fpac;
        r.Data.SystemNumber = kno;
        return r;
    }

    [Fact]
    public void 制御電源番号一致で系統番号を返す()
    {
        var mains = new List<MainCircuitResult>
        {
            Main("01", "005"),
            Main("02", "010"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerSystemNumber("02", mains, out string kno);

        Assert.Equal(0, ret);
        Assert.Equal("010", kno);
    }

    [Fact]
    public void 先頭一致を優先する()
    {
        var mains = new List<MainCircuitResult>
        {
            Main("01", "005"),
            Main("01", "006"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerSystemNumber("01", mains, out string kno);

        Assert.Equal(0, ret);
        Assert.Equal("005", kno);
    }

    [Fact]
    public void 該当なしは負1で系統番号は空()
    {
        var mains = new List<MainCircuitResult>
        {
            Main("01", "005"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerSystemNumber("09", mains, out string kno);

        Assert.Equal(-1, ret);
        Assert.Equal(string.Empty, kno);
    }

    [Fact]
    public void 制御電源番号は2バイトで比較する()
    {
        // fpac は 2 バイト固定。3 文字目以降は比較対象外。
        var mains = new List<MainCircuitResult>
        {
            Main("01", "005"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerSystemNumber("012", mains, out string kno);

        Assert.Equal(0, ret);
        Assert.Equal("005", kno);
    }

    [Fact]
    public void 空の制御電源番号は空白2文字と一致する()
    {
        var mains = new List<MainCircuitResult>
        {
            Main(string.Empty, "005"),
        };

        int ret = ControlPowerSystemLocator.GetControlPowerSystemNumber("  ", mains, out string kno);

        Assert.Equal(0, ret);
        Assert.Equal("005", kno);
    }

    [Fact]
    public void 空テーブルは負1()
    {
        int ret = ControlPowerSystemLocator.GetControlPowerSystemNumber("01", new List<MainCircuitResult>(), out string kno);

        Assert.Equal(-1, ret);
        Assert.Equal(string.Empty, kno);
    }

    // ---- GetUpstreamControlPowerData(【C原典】GetSeivdnoUp, Fyss1k.c:3392) ----

    private static MainCircuitResult MainRow(string gyo, string gyocd, string datano, char bn)
    {
        var r = new MainCircuitResult();
        r.Data.DescriptionRow = gyo;
        r.Data.LineTypeCode = gyocd;
        r.SequenceNumber = datano;
        r.Data.ElectricalParameterSlots[0].Bn = bn;
        return r;
    }

    private static ControlSpecEntry Spec(short kgyo) => new() { DescriptionRow = kgyo };

    [Fact]
    public void 直上UP行からデータ追番と盤種類を取得する()
    {
        var mains = new List<MainCircuitResult>
        {
            MainRow("001", "UP ", "007", '2'),
            MainRow("002", "MC ", "008", '3'),
            MainRow("005", "MC ", "009", '4'),
        };

        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(3), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("007", seivdno);
        Assert.Equal('2', bn);
    }

    [Fact]
    public void 直上主回路行が無ければ負1()
    {
        var mains = new List<MainCircuitResult>
        {
            MainRow("005", "UP ", "001", '1'),
        };

        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(1), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
        Assert.Equal(string.Empty, seivdno);
        Assert.Equal('\0', bn);
    }

    [Fact]
    public void 直上はあるがUP行が無ければ負1()
    {
        var mains = new List<MainCircuitResult>
        {
            MainRow("001", "MC ", "007", '2'),
            MainRow("002", "MG ", "008", '3'),
        };

        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(5), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
        Assert.Equal(string.Empty, seivdno);
    }

    [Fact]
    public void 記述行と同一のgyoは直上とみなさない()
    {
        // memcmp(kgyou, gyo, 3) > 0 のみが直上。等しい行は対象外。
        var mains = new List<MainCircuitResult>
        {
            MainRow("003", "UP ", "007", '2'),
        };

        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(3), mains, out string seivdno, out char bn);

        Assert.Equal(-1, ret);
    }

    [Fact]
    public void 直近上位のUP行を優先する()
    {
        var mains = new List<MainCircuitResult>
        {
            MainRow("001", "UP ", "100", '1'),
            MainRow("002", "UP ", "200", '5'),
            MainRow("004", "MC ", "300", '9'),
        };

        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(3), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("200", seivdno);
        Assert.Equal('5', bn);
    }

    [Fact]
    public void 記述行は3桁ゼロ埋めで比較する()
    {
        // kgyo=12 は "012" に整形され "009" より大(数値順と一致)。
        var mains = new List<MainCircuitResult>
        {
            MainRow("009", "UP ", "050", '7'),
        };

        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(12), mains, out string seivdno, out char bn);

        Assert.Equal(0, ret);
        Assert.Equal("050", seivdno);
        Assert.Equal('7', bn);
    }

    [Fact]
    public void 上位検索は空テーブルで負1()
    {
        int ret = ControlPowerSystemLocator.GetUpstreamControlPowerData(
            Spec(3), new List<MainCircuitResult>(), out string seivdno, out char bn);

        Assert.Equal(-1, ret);
        Assert.Equal(string.Empty, seivdno);
        Assert.Equal('\0', bn);
    }
}
