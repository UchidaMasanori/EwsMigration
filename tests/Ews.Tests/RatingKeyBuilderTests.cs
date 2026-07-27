using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 定格値キー(kteichi 50バイト)生成の検証。
/// 【C原典】Fysk04_Make_Teikakuchi(toku/sekkei/src/Fysk04.c) / Fysk00_Get_Datachi(Fysk00.c) / fyrt817.h。
/// </summary>
public sealed class RatingKeyBuilderTests
{
    private static string Pad(string body) => body.PadRight(RatingKeyBuilder.KeyLength);

    [Fact]
    public void MakeRatingKey_MCB_全項目を連結する()
    {
        // at+af+p+e+v = 4+4+1+1+3 桁
        var p = new NumericElectricalParameters
        {
            At = 100,           // 項番 9
            Af = 225,           // 項番 8
            P = 3,              // 項番 6
            E = 2,              // 項番 7
        };
        p.V2[0] = 200;          // 項番 23

        string key = RatingKeyBuilder.MakeRatingKey(RatingKeyTables.Mcb, p);

        Assert.Equal(Pad("0100" + "0225" + "3" + "2" + "200"), key);
        Assert.Equal(50, key.Length);
    }

    [Fact]
    public void MakeRatingKey_ELB_電圧範囲行で打切る()
    {
        // ma+at+af+p+e まで採用し、vg(s_toku==-3)で打切る
        var p = new NumericElectricalParameters
        {
            At = 100,
            Af = 225,
            P = 3,
            E = 2,
        };
        p.Ma[0] = 30;           // 項番 16
        p.V2[0] = 200;          // 打切り後なのでキーには入らない

        string key = RatingKeyBuilder.MakeRatingKey(RatingKeyTables.Elb, p);

        Assert.Equal(Pad("030" + "0100" + "0225" + "3" + "2"), key);
    }

    [Fact]
    public void MakeRatingKey_THR_固定キーは空になる()
    {
        // 先頭 -2(スキップ)後すぐ -3(打切り)なので固定キーは 50 空白
        var p = new NumericElectricalParameters { At = 100 };

        string key = RatingKeyBuilder.MakeRatingKey(RatingKeyTables.Thr, p);

        Assert.Equal(new string(' ', 50), key);
    }

    [Fact]
    public void MakeRatingKey_MG_電流のみ採用し打切る()
    {
        // a(項番 11)採用後、制御電圧 vcl(-3)で打切る
        var p = new NumericElectricalParameters();
        p.A2 = 18;              // 項番 11

        string key = RatingKeyBuilder.MakeRatingKey(RatingKeyTables.Mg, p);

        Assert.Equal(Pad("018"), key);
    }

    [Fact]
    public void MakeRatingKey_MMCB_小数スケールを適用する()
    {
        // at は d_len=2 なので 2.5 → ×100 → 250 → 幅5 "00250"
        var p = new NumericElectricalParameters
        {
            At = 2.5,
            Af = 5,
            P = 3,
            E = 2,
        };
        p.V2[0] = 200;

        string key = RatingKeyBuilder.MakeRatingKey(RatingKeyTables.Mmcb, p);

        Assert.Equal(Pad("00250" + "005" + "3" + "2" + "200"), key);
    }

    [Fact]
    public void MakeRatingKey_SC_複数の小数スケールを適用する()
    {
        // p+hz+v+kvar(d_len=2)+uf(d_len=1)
        var p = new NumericElectricalParameters
        {
            Hz = 50,            // 項番 5
            Kvar = 7.5,         // 項番 14 → ×100 → 750
            Uf = 2.5,           // 項番 15 → ×10 → 25
        };
        p.Ph2[0] = 3;           // 項番 2
        p.V2[0] = 200;          // 項番 23

        string key = RatingKeyBuilder.MakeRatingKey(RatingKeyTables.Sc, p);

        Assert.Equal(Pad("3" + "50" + "200" + "00750" + "00025"), key);
    }

    [Theory]
    [InlineData('A', "12")]   // AC → n=1 → 1件目採用
    [InlineData('D', "34")]   // DC → n=2 → 2件目採用
    public void MakeRatingKey_区分読取りで採用行を切替える(char kbn, string expectedBody)
    {
        // -1 行で V2Kbn を "AD" と照合し n を決め、s_toku==n の行のみ採用する
        RatingKeyTableEntry[] table =
        {
            new(0, 0, 27, 0, 0, 0, 0, -1),   // 区分読取り(V2Kbn)
            new(2, 0, 9, 0, 0, 0, 0, 1),     // n==1(AC)で採用 → At
            new(2, 0, 34, 0, 0, 0, 0, 2),    // n==2(DC)で採用 → Ac
            new(-1, -1, -1, -1, -1, -1, -1, -1),
        };
        var p = new NumericElectricalParameters { V2Kbn = kbn, At = 12, Ac = 34 };

        string key = RatingKeyBuilder.MakeRatingKey(table, p);

        Assert.Equal(Pad(expectedBody), key);
    }

    [Fact]
    public void MakeRatingKey_区分不一致なら該当行は採用されない()
    {
        RatingKeyTableEntry[] table =
        {
            new(0, 0, 27, 0, 0, 0, 0, -1),   // V2Kbn を読取り
            new(2, 0, 9, 0, 0, 0, 0, 1),     // n==1 のみ採用
            new(-1, -1, -1, -1, -1, -1, -1, -1),
        };
        var p = new NumericElectricalParameters { V2Kbn = 'X', At = 12 };   // "AD" 外 → n=0

        string key = RatingKeyBuilder.MakeRatingKey(table, p);

        Assert.Equal(new string(' ', 50), key);
    }

    [Theory]
    [InlineData((short)9, 100.0)]    // epaat
    [InlineData((short)23, 200.0)]   // epav2[0]
    [InlineData((short)16, 30.0)]    // epama[0]
    public void GetDataValue_数値項目を返す(short itemNo, double expected)
    {
        var p = new NumericElectricalParameters { At = 100 };
        p.V2[0] = 200;
        p.Ma[0] = 30;

        RatingKeyBuilder.DataValue v = RatingKeyBuilder.GetDataValue(itemNo, p);

        Assert.Equal(expected, v.Numeric);
    }

    [Fact]
    public void GetDataValue_区分項目は文字を返す()
    {
        var p = new NumericElectricalParameters { V2Kbn = 'A', VcKbn = 'D', Bn = 'B' };

        Assert.Equal('A', RatingKeyBuilder.GetDataValue(27, p).Char);   // epav2kbn
        Assert.Equal('D', RatingKeyBuilder.GetDataValue(30, p).Char);   // epavckbn
        Assert.Equal('B', RatingKeyBuilder.GetDataValue(40, p).Char);   // epabn
    }
}
