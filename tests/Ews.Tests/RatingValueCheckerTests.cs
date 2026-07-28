using Ews.Analysis;
using Ews.Domain.Analysis;

namespace Ews.Tests;

/// <summary>
/// 定格値チェック <see cref="RatingValueChecker"/>(=Fysk02_Check_Teikakuchi/ALL/Part/GLE_Check)と、
/// 共用情報項番(61～87)まで拡張した <see cref="RatingKeyBuilder.GetDataValue(short, NumericElectricalParameters, NumericSharedInfo)"/>
/// の単体テスト。
/// </summary>
public class RatingValueCheckerTests
{
    /// <summary>入力有無チェック配列(index 0=有効フラグ, 以降 index=項番)。すべて 0(=入力なし)で作る。</summary>
    private static int[] NoInput() => new int[90];

    private static NumericSharedInfo EmptyShared() => new()
    {
        SensitivityCurrents = new double[] { 0, 0, 0 },
        PrimaryVoltageValues = new double[] { 0, 0, 0 },
        PrimaryVoltageKinds = new char[] { ' ', ' ' },
        SecondaryVoltageValues = new double[] { 0, 0, 0, 0 },
        SecondaryVoltageKinds = new char[] { ' ', ' ', ' ' },
        ControlVoltageValues = new double[] { 0, 0, 0, 0 },
        ControlVoltageKinds = new char[] { ' ', ' ', ' ' },
        ControlVoltageRangeFrom = 1.0,
        ControlVoltageRangeTo = 1.0,
    };

    [Fact]
    public void GetDataValue_共用情報の項番を返す()
    {
        var shared = new NumericSharedInfo
        {
            MainPowerSharedAcDc = 'A',
            ControlPowerSharedAcDc = 'D',
            SensitivityCurrents = new double[] { 30, 100, 200 },
            PrimaryVoltageValues = new double[] { 210, 220, 230 },
            PrimaryVoltageKinds = new char[] { 'X', 'Y' },
            SecondaryVoltageValues = new double[] { 100, 105, 110, 115 },
            SecondaryVoltageKinds = new char[] { 'a', 'b', 'c' },
            ControlVoltageValues = new double[] { 200, 201, 202, 203 },
            ControlVoltageKinds = new char[] { 'p', 'q', 'r' },
            ControlVoltageRangeFrom = 0.9,
            ControlVoltageRangeTo = 1.1,
        };
        var p = new NumericElectricalParameters();

        Assert.Equal('A', RatingKeyBuilder.GetDataValue(61, p, shared).Char);
        Assert.Equal('D', RatingKeyBuilder.GetDataValue(62, p, shared).Char);
        Assert.Equal(30, RatingKeyBuilder.GetDataValue(63, p, shared).Numeric);
        Assert.Equal(200, RatingKeyBuilder.GetDataValue(65, p, shared).Numeric);
        Assert.Equal(210, RatingKeyBuilder.GetDataValue(66, p, shared).Numeric);
        Assert.Equal('Y', RatingKeyBuilder.GetDataValue(69, p, shared).Char);
        Assert.Equal(115, RatingKeyBuilder.GetDataValue(77, p, shared).Numeric);
        Assert.Equal('r', RatingKeyBuilder.GetDataValue(83, p, shared).Char);
        Assert.Equal(0.9, RatingKeyBuilder.GetDataValue(86, p, shared).Numeric);
        Assert.Equal(1.1, RatingKeyBuilder.GetDataValue(87, p, shared).Numeric);
    }

    [Fact]
    public void GetDataValue_項番85は配列外参照で項番66と同値になる()
    {
        // 【C原典】km_s.kyomad[3](配列外)= kv1_s.kyov1d1(=項番66)。原典の配列外参照を再現。
        var shared = new NumericSharedInfo
        {
            SensitivityCurrents = new double[] { 30, 100, 200 },   // 3枠のみ
            PrimaryVoltageValues = new double[] { 415, 0, 0 },
        };
        var p = new NumericElectricalParameters();

        double item85 = RatingKeyBuilder.GetDataValue(85, p, shared).Numeric;
        double item66 = RatingKeyBuilder.GetDataValue(66, p, shared).Numeric;

        Assert.Equal(415, item85);
        Assert.Equal(item66, item85);
    }

    [Fact]
    public void CheckAll_全項目が範囲を満たせばOKを返す()
    {
        // MCB 相当: at(GE)/af(GE)/p(GE)/e(GE)/v(GE)。候補側は電気パラメータ以上。
        RatingKeyTableEntry[] table =
        {
            new(4, 0, 9, 2, 0, 0, 0, 0),    // at GE
            new(4, 0, 8, 2, 0, 0, 0, 0),    // af GE
            new(1, 0, 6, 2, 0, 0, 0, 0),    // p GE
            new(1, 0, 7, 2, 0, 0, 0, 0),    // e GE
            new(3, 0, 23, 2, 0, 0, 0, 0),   // v GE
            new(-1, -1, -1, -1, -1, -1, -1, -1),
        };
        var p = new NumericElectricalParameters { At = 30, Af = 30, P = 3, E = 1 };
        p.V2[0] = 200;
        // 候補 kteichi: at=0030 af=0030 p=3 e=1 v=200
        string tc = "0030" + "0030" + "3" + "1" + "200";
        var cmp = new RatingComparisonState();

        int ret = RatingValueChecker.Check(0, table, p, NoInput(), tc, EmptyShared(), 1, cmp);

        Assert.Equal(RatingValueChecker.Good, ret);
    }

    [Fact]
    public void CheckAll_GE比較で候補が小さいとNGを返す()
    {
        RatingKeyTableEntry[] table =
        {
            new(4, 0, 9, 2, 0, 0, 0, 0),    // at GE
            new(-1, -1, -1, -1, -1, -1, -1, -1),
        };
        var p = new NumericElectricalParameters { At = 50 };  // 要求 50
        string tc = "0030";                                   // 候補 30 < 50 → NG
        var cmp = new RatingComparisonState();

        int ret = RatingValueChecker.Check(0, table, p, NoInput(), tc, EmptyShared(), 1, cmp);

        Assert.Equal(RatingValueChecker.NoGood, ret);
    }

    [Fact]
    public void CheckPart_格納区分に応じて比較用グローバル値へ退避する()
    {
        // kakunou 1/2 → CMP_1[0]/[1], 3 → CMP_2, 4 → CMP_3。
        RatingKeyTableEntry[] table =
        {
            new(4, 0, 9, 0, 0, 1, 0, 0),    // at → CMP_1[0]
            new(4, 0, 8, 0, 0, 2, 0, 0),    // af → CMP_1[1]
            new(3, 0, 23, 0, 0, 4, 0, 0),   // v  → CMP_3
            new(-1, -1, -1, -1, -1, -1, -1, -1),
        };
        var p = new NumericElectricalParameters();
        string tc = "0100" + "0200" + "440";
        var cmp = new RatingComparisonState();

        RatingValueChecker.Check(0, table, p, NoInput(), tc, EmptyShared(), 1, cmp);

        Assert.Equal(100, cmp.AmpereTripPair[0]);
        Assert.Equal(200, cmp.AmpereTripPair[1]);
        Assert.Equal(440, cmp.Voltage);
    }

    [Fact]
    public void CheckPart_入力有ありなら比較記号は一致に置き換わる()
    {
        // sfg[0]==1 かつ sfg[kouno]==1、s_toku が -3/-1 以外 → geflg=E(一致)。
        // GE 指定だが候補>要求でも一致でないため NG になる。
        RatingKeyTableEntry[] table =
        {
            new(4, 0, 9, 2, 0, 0, 0, 0),    // at GE(だが入力有で E に変わる)
            new(-1, -1, -1, -1, -1, -1, -1, -1),
        };
        var p = new NumericElectricalParameters { At = 30 };
        int[] sfg = NoInput();
        sfg[0] = 1;
        sfg[9] = 1;    // at の入力あり
        string tcHit = "0030";   // 一致 → OK
        string tcOver = "0040";  // 候補>要求だが一致でない → NG
        var cmp = new RatingComparisonState();

        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(0, table, p, sfg, tcHit, EmptyShared(), 1, cmp));
        Assert.Equal(RatingValueChecker.NoGood,
            RatingValueChecker.Check(0, table, p, sfg, tcOver, EmptyShared(), 1, cmp));
    }

    [Fact]
    public void CheckPart_len0のときd_lenを項番として別データを取得する()
    {
        // len==0 → aac = Get_Datachi(d_len)。ここでは d_len=23(=epav2[0])。
        // 比較 GE: aac(=epav2[0]) >= aak(=epaat) を要求。
        RatingKeyTableEntry[] table =
        {
            new(0, 23, 9, 2, 0, 0, 0, 0),   // len0/d_len=23(v2) を aac, kouno=9(at) を aak, GE
            new(-1, -1, -1, -1, -1, -1, -1, -1),
        };
        var pOk = new NumericElectricalParameters { At = 100 };
        pOk.V2[0] = 200;   // 200 >= 100 → OK
        var pNg = new NumericElectricalParameters { At = 300 };
        pNg.V2[0] = 200;   // 200 < 300 → NG
        var cmp = new RatingComparisonState();

        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(0, table, pOk, NoInput(), string.Empty, EmptyShared(), 1, cmp));
        Assert.Equal(RatingValueChecker.NoGood,
            RatingValueChecker.Check(0, table, pNg, NoInput(), string.Empty, EmptyShared(), 1, cmp));
    }

    [Fact]
    public void CheckPart_fromtoで制御電圧適応範囲を乗じる()
    {
        // fromto==1 → aac *= scd.vcfrom。候補 100 × 1.1 = 110 >= 要求 105 → OK。
        RatingKeyTableEntry[] table =
        {
            new(3, 0, 29, 2, 1, 0, 0, 0),   // vc GE, fromto=1
            new(-1, -1, -1, -1, -1, -1, -1, -1),
        };
        var p = new NumericElectricalParameters { Vc = 105 };
        NumericSharedInfo shared = EmptyShared();
        shared.ControlVoltageRangeFrom = 1.1;
        string tc = "100";   // 100 × 1.1 = 110 >= 105 → OK
        var cmp = new RatingComparisonState();

        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(0, table, p, NoInput(), tc, shared, 1, cmp));
    }

    [Fact]
    public void CheckPart_ma処理_感度電流が候補側に揃えばOK()
    {
        // 項番16(ma)。epama[0..2]=30/100/0、候補側 kyomad[0..2]=100/30/0。集合として一致。
        RatingKeyTableEntry[] table =
        {
            new(3, 0, 16, 1, 0, 0, 0, 0),   // ma(check=E だが sfg なしで比較は素通り、ma処理へ)
            new(-1, -1, -1, -1, -1, -1, -1, -1),
        };
        var p = new NumericElectricalParameters();
        p.Ma[0] = 30; p.Ma[1] = 100; p.Ma[2] = 0; p.Ma[3] = 0;
        NumericSharedInfo shared = EmptyShared();
        shared.SensitivityCurrents = new double[] { 100, 30, 0 };
        string tc = "030";   // candidate ma 値(check=E: 30 == epama[0]=30 → OK)
        var cmp = new RatingComparisonState();

        int ret = RatingValueChecker.Check(0, table, p, NoInput(), tc, shared, 1, cmp);

        Assert.Equal(RatingValueChecker.Good, ret);
    }

    [Fact]
    public void CheckPart_ma処理_入力なし1回目で不一致ならREPEATを返す()
    {
        // sfg[16]!=1 かつ times==1 で不一致 → REPEAT。
        RatingKeyTableEntry[] table =
        {
            new(3, 0, 16, 0, 0, 0, 0, 0),   // ma(check=0 で比較なし、ma処理のみ)
            new(-1, -1, -1, -1, -1, -1, -1, -1),
        };
        var p = new NumericElectricalParameters();
        p.Ma[0] = 999;   // 候補側に無い値
        NumericSharedInfo shared = EmptyShared();
        shared.SensitivityCurrents = new double[] { 1, 2, 3 };
        var cmp = new RatingComparisonState();

        int ret = RatingValueChecker.Check(0, table, p, NoInput(), "000", shared, 1, cmp);

        Assert.Equal(RatingValueChecker.Repeat, ret);
    }

    [Fact]
    public void Check_特殊予約語フラグは未対応で例外を投げる()
    {
        RatingKeyTableEntry[] table = { new(-1, -1, -1, -1, -1, -1, -1, -1) };
        var p = new NumericElectricalParameters();
        var cmp = new RatingComparisonState();

        Assert.Throws<NotSupportedException>(() =>
            RatingValueChecker.Check(2, table, p, NoInput(), string.Empty, EmptyShared(), 1, cmp));
    }
}
