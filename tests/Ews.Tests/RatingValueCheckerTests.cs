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
    public void Check_未知のフラグは例外を投げる()
    {
        RatingKeyTableEntry[] table = { new(-1, -1, -1, -1, -1, -1, -1, -1) };
        var p = new NumericElectricalParameters();
        var cmp = new RatingComparisonState();

        Assert.Throws<NotSupportedException>(() =>
            RatingValueChecker.Check(99, table, p, NoInput(), string.Empty, EmptyShared(), 1, cmp));
    }

    // ---- 特殊予約語(flag 1～13)----

    /// <summary>入力有無ありの sfg(index0=有効, 指定項番=1)。</summary>
    private static int[] Input(params int[] itemNos)
    {
        var sfg = new int[90];
        sfg[0] = 1;
        foreach (int no in itemNos)
        {
            sfg[no] = 1;
        }

        return sfg;
    }

    private static RatingKeyTableEntry Trivial(short itemNo = 1)
        => new(0, 0, itemNo, 0, 0, 0, 0, 0);

    [Fact]
    public void Check_SC_容量が候補以上ならOK候補未満ならNG()
    {
        // flag1 SC: 先頭3項目(通常)→ 4項目目でコンデンサ容量(項番14 Kvar)を大小判定。
        RatingKeyTableEntry[] table =
        {
            Trivial(1), Trivial(2), Trivial(3),
            new(3, 0, 14, 0, 0, 0, 0, 0),  // kvar(aak>TOL 経路)
            new(3, 0, 15, 0, 0, 0, 0, 0),  // uf(aak<=TOL 経路の候補)
        };
        var p = new NumericElectricalParameters { Kvar = 100 };
        var cmp = new RatingComparisonState();

        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(1, table, p, NoInput(), "150", EmptyShared(), 1, cmp));
        Assert.Equal(RatingValueChecker.NoGood,
            RatingValueChecker.Check(1, table, p, NoInput(), "050", EmptyShared(), 1, cmp));
    }

    [Fact]
    public void Check_WH_一次側なし経路で二次側を大小判定()
    {
        // flag2 WH: 先頭5項目(通常)→ 6項目目(項番9 At)で fg 決定 → 7項目目(項番10 A1)。
        RatingKeyTableEntry[] table =
        {
            Trivial(1), Trivial(2), Trivial(3), Trivial(4), Trivial(5),
            new(3, 0, 9, 0, 0, 0, 0, 0),   // At(fg 決定)
            new(3, 0, 10, 0, 0, 0, 0, 0),  // A1
        };
        var pNoCt = new NumericElectricalParameters { At = 0, A1 = 200 };  // fg=1
        var cmp = new RatingComparisonState();

        // fg=1・ch=0: 二次側 aac>=aak なら OK。
        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(2, table, pNoCt, NoInput(), "000250", EmptyShared(), 1, cmp));
        Assert.Equal(RatingValueChecker.NoGood,
            RatingValueChecker.Check(2, table, pNoCt, NoInput(), "000150", EmptyShared(), 1, cmp));
    }

    [Fact]
    public void Check_VM_区分Aは完全一致_区分A以外は比較スキップ()
    {
        // flag3 VM: 項番27(epav2kbn)が 'A' のときのみ 1項目目を判定(dangling-else)。
        RatingKeyTableEntry[] table =
        {
            new(3, 0, 9, 0, 0, 0, 0, 0),   // At
            Trivial(1),                     // 2項目目(通常)
        };
        var cmp = new RatingComparisonState();

        var pA = new NumericElectricalParameters { V2Kbn = 'A', At = 100 };
        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(3, table, pA, NoInput(), "100", EmptyShared(), 1, cmp));
        Assert.Equal(RatingValueChecker.NoGood,
            RatingValueChecker.Check(3, table, pA, NoInput(), "050", EmptyShared(), 1, cmp));

        // 区分が 'A' 以外: dangling-else により 1項目目の比較は一切行われず OK。
        var pBlank = new NumericElectricalParameters { V2Kbn = ' ', At = 100 };
        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(3, table, pBlank, NoInput(), "050", EmptyShared(), 1, cmp));
    }

    [Fact]
    public void Check_AM_CT無しは二次側を大小判定()
    {
        // flag4 AM: 1項目目(項番9 At)で CT有無(fg)決定 → 2項目目(項番10 A1)。
        RatingKeyTableEntry[] table =
        {
            new(3, 0, 9, 0, 0, 0, 0, 0),
            new(3, 0, 10, 0, 0, 0, 0, 0),
        };
        var p = new NumericElectricalParameters { At = 0, A1 = 200 };  // fg=1(CT無し)
        var cmp = new RatingComparisonState();

        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(4, table, p, NoInput(), "000250", EmptyShared(), 1, cmp));
        Assert.Equal(RatingValueChecker.NoGood,
            RatingValueChecker.Check(4, table, p, NoInput(), "000150", EmptyShared(), 1, cmp));
    }

    [Fact]
    public void Check_CR_接点計算不要のとき後半項目も判定する()
    {
        // flag6 CR: 前半4項目→ stn==-1 のとき後半3項目。5項目目(項番9 At)を E で不一致に。
        RatingKeyTableEntry[] table =
        {
            Trivial(1), Trivial(2), Trivial(3), Trivial(4),
            new(3, 0, 9, 1, 0, 0, 0, 0),   // At の E チェック(不一致で NG)
            Trivial(10), Trivial(11),
        };
        var p = new NumericElectricalParameters { At = 100 };
        var cmp = new RatingComparisonState();

        // stn=-1: 後半も判定 → 候補 050 は At 100 と不一致で NG。
        Assert.Equal(RatingValueChecker.NoGood,
            RatingValueChecker.Check(6, table, p, NoInput(), "050", EmptyShared(), 1, cmp, -1));
        // stn=0: 前半のみ判定 → OK。
        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(6, table, p, NoInput(), "050", EmptyShared(), 1, cmp, 0));
    }

    [Fact]
    public void Check_TM_先頭の時間単位判定は結果に影響しない()
    {
        // flag7 TM: 先頭(項番9 At の E)は判定するが戻り値を無視。候補が At と不一致でも OK。
        RatingKeyTableEntry[] table =
        {
            new(3, 0, 9, 1, 0, 0, 0, 0),   // 時間単位(戻り値無視)
            Trivial(1), Trivial(2),         // chkflg==0→2 で比較スキップ
            Trivial(3), Trivial(4), Trivial(5), Trivial(6),
        };
        var p = new NumericElectricalParameters { At = 100 };
        var cmp = new RatingComparisonState();

        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(7, table, p, NoInput(), "050", EmptyShared(), 1, cmp, 0));
    }

    [Fact]
    public void Check_BZ_区分で判定項目を切り替える()
    {
        // flag9 BZ: 項番27(区分)が 'A' なら項目[3]、それ以外は項目[4]。
        RatingKeyTableEntry[] table =
        {
            Trivial(1), Trivial(2),
            new(0, 0, 27, 0, 0, 0, 0, 0),  // 区分読取用(kouno=27)
            Trivial(10),                    // 'A' 経路(通常 OK)
            new(3, 0, 9, 1, 0, 0, 0, 0),   // 非'A' 経路(At の E)
        };
        var cmp = new RatingComparisonState();

        var pA = new NumericElectricalParameters { V2Kbn = 'A', At = 100 };
        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(9, table, pA, NoInput(), "050", EmptyShared(), 1, cmp));

        var pBlank = new NumericElectricalParameters { V2Kbn = ' ', At = 100 };
        Assert.Equal(RatingValueChecker.NoGood,
            RatingValueChecker.Check(9, table, pBlank, NoInput(), "050", EmptyShared(), 1, cmp));
    }

    [Fact]
    public void Check_MV_区分で判定項目を切り替える()
    {
        // flag11 MV: 1項目目(通常)→ 項番27(区分)が 'A' なら項目[2]、それ以外は項目[3]。
        RatingKeyTableEntry[] table =
        {
            Trivial(1),
            new(0, 0, 27, 0, 0, 0, 0, 0),  // 区分読取用
            Trivial(10),                    // 'A' 経路
            new(3, 0, 9, 1, 0, 0, 0, 0),   // 非'A' 経路(At の E)
        };
        var cmp = new RatingComparisonState();

        var pA = new NumericElectricalParameters { V2Kbn = 'A', At = 100 };
        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(11, table, pA, NoInput(), "050", EmptyShared(), 1, cmp));

        var pBlank = new NumericElectricalParameters { V2Kbn = ' ', At = 100 };
        Assert.Equal(RatingValueChecker.NoGood,
            RatingValueChecker.Check(11, table, pBlank, NoInput(), "050", EmptyShared(), 1, cmp));
    }

    [Fact]
    public void Check_TR_全項目がゼロなら通過する()
    {
        // flag5 TR: 先頭12項目→ 項番[12]の Key Value がゼロなら 13～15 をスキップ→ 項目[16]。
        var entries = new RatingKeyTableEntry[17];
        for (int i = 0; i < 17; i++)
        {
            entries[i] = Trivial(1);
        }

        var p = new NumericElectricalParameters();
        var cmp = new RatingComparisonState();

        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(5, entries, p, NoInput(), string.Empty, EmptyShared(), 1, cmp));
    }

    [Fact]
    public void Check_THSW_基本経路を通過する()
    {
        // flag13 THSW: 先頭(戻り値無視)→ 項目1～2(chkflg 2 で比較スキップ)→ 項目3～4。
        RatingKeyTableEntry[] table =
        {
            new(3, 0, 9, 1, 0, 0, 0, 0),   // 先頭(戻り値無視)
            Trivial(1), Trivial(2),
            Trivial(3), Trivial(4),
        };
        var p = new NumericElectricalParameters { At = 100 };
        var cmp = new RatingComparisonState();

        Assert.Equal(RatingValueChecker.Good,
            RatingValueChecker.Check(13, table, p, NoInput(), "050", EmptyShared(), 1, cmp));
    }
}
