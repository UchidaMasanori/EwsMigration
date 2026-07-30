using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 機器検索前処理の TS/400V/耐熱ブレーカ補正(<see cref="SpecialBreakerTypeResolver"/>)の移植検証。
/// 【C原典】PropChgTsType/PropChg400vBreaker/PropChgF2Breaker(Fysk00.c)。
/// </summary>
public sealed class SpecialBreakerTypeResolverTests
{
    private static SpecialBreakerTypeResolver Build(string circuitText) =>
        new(new CircuitDescriptionArea(
            [new CircuitDescriptionLine { LineNumber = 5, CircuitText = circuitText }]));

    private static string[] Types(params string[] values)
    {
        string[] result = ["", "", "", "", "", "", ""];
        for (int i = 0; i < values.Length && i < 7; i++)
        {
            result[i] = values[i];
        }
        return result;
    }

    // ---- PropChgTsType(改訂<71>) 主回路 ----

    private static MainCircuitResult Ts(string reservedWord = "TS")
        => new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = reservedWord,
                DescriptionRow = "005",
                DescriptionColumn = "001",
            },
        };

    [Fact]
    public void 松下TSでタイプ指定なしならタイプ2をMTにする()
    {
        MainCircuitResult ts = Ts();
        string[] codes = ["D  ", "   ", "   ", "   "];
        string[] dtype = Types();
        string[] wtype = Types();

        Build("TS,").AdjustTsType(ts, codes, dtype, wtype);

        Assert.Equal("MT     ", dtype[1]);
        Assert.Equal("MT     ", wtype[1]);
        Assert.Equal("MT     ", ts.Data.DataType[1]);
    }

    [Fact]
    public void TSでもタイプ指定入力があればMTにしない()
    {
        MainCircuitResult ts = Ts();
        string[] codes = ["D  ", "   ", "   ", "   "];
        string[] dtype = Types();
        string[] wtype = Types();

        Build("TS+(YY),").AdjustTsType(ts, codes, dtype, wtype);

        Assert.Equal("", dtype[1]);
    }

    [Fact]
    public void 松下製でないTSはMTにしない()
    {
        MainCircuitResult ts = Ts();
        string[] codes = ["M  ", "   ", "   ", "   "];
        string[] dtype = Types();
        string[] wtype = Types();

        Build("TS,").AdjustTsType(ts, codes, dtype, wtype);

        Assert.Equal("", dtype[1]);
    }

    // ---- PropChgTsType(改訂<71>) 制御 ----

    [Fact]
    public void 制御松下TSでタイプ指定なしならタイプ2をMTにする()
    {
        ControlEquipmentInfo ts = new()
        {
            ReservedWord = "TS",
            DescriptionRow = "005",
            DescriptionColumn = "001",
        };
        string[] codes = ["D  ", "   ", "   ", "   "];
        string[] dtype = Types();
        string[] wtype = Types();

        Build("TS,").AdjustTsTypeControl(ts, codes, dtype, wtype);

        Assert.Equal("MT     ", ts.DataType[1]);
        Assert.Equal("MT     ", wtype[1]);
    }

    // ---- PropChg400vBreaker(改訂<115>) ----

    private static MainCircuitResult Breaker400(string reservedWord, string voltage, string frame)
    {
        MainCircuitResult breaker = new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = reservedWord,
                DescriptionRow = "005",
                DescriptionColumn = "001",
            },
        };
        breaker.Data.ElectricalParameterSlots[1].V2[0] = voltage;
        breaker.Data.ElectricalParameterSlots[1].Af = frame;
        return breaker;
    }

    [Fact]
    public void 主電源400V400AF以上のブレーカは経済型と三菱KMにする()
    {
        MainCircuitResult breaker = Breaker400("MCB", "000400.0", "00400.000");
        string[] codes = ["   ", "   ", "   ", "   "];
        string[] wtype = Types("A", "B", "C");

        bool ok = Build("MCB,").Adjust400vBreaker(breaker, 'N', codes, wtype);

        Assert.True(ok);
        Assert.Equal("ET     ", breaker.Data.DataType[0]);
        Assert.Equal("ET     ", wtype[0]);
        Assert.Equal("A", wtype[1]);
        Assert.Equal("B", wtype[2]);
        Assert.Equal("C", wtype[3]);
        Assert.Equal("M  ", codes[0]);
        Assert.Equal("KM ", codes[1]);
    }

    [Fact]
    public void 主電源受電400VブレーカはTSメーカーにする()
    {
        MainCircuitResult breaker = Breaker400("MCB", "000440.0", "00300.000");
        string[] codes = ["   ", "   ", "   ", "   "];
        string[] wtype = Types();

        Build("MCB,").Adjust400vBreaker(breaker, 'Y', codes, wtype);

        Assert.Equal("TS ", codes[0]);
        Assert.Equal("M  ", codes[1]);
        Assert.Equal("KTS", codes[2]);
    }

    [Fact]
    public void 電圧400V未満のブレーカは変更しない()
    {
        MainCircuitResult breaker = Breaker400("MCB", "000210.0", "00100.000");
        string[] codes = ["   ", "   ", "   ", "   "];
        string[] wtype = Types("A");

        Build("MCB,").Adjust400vBreaker(breaker, 'N', codes, wtype);

        Assert.Equal("", breaker.Data.DataType[0]);
        Assert.Equal("   ", codes[0]);
    }

    [Fact]
    public void 電圧400Vブレーカでタイプ指定があれば経済型にしない()
    {
        MainCircuitResult breaker = Breaker400("MCB", "000400.0", "00400.000");
        string[] codes = ["   ", "   ", "   ", "   "];
        string[] wtype = Types();

        Build("MCB+(XX),").Adjust400vBreaker(breaker, 'N', codes, wtype);

        Assert.Equal("", breaker.Data.DataType[0]);
        Assert.Equal("M  ", codes[0]);   // メーカーは変更される
    }

    [Fact]
    public void 回路記述取得失敗は取得失敗を返す()
    {
        MainCircuitResult breaker = Breaker400("MCB", "000400.0", "00400.000");
        breaker.Data.DescriptionRow = "009";   // 行不一致で取得NG
        string[] codes = ["   ", "   ", "   ", "   "];
        string[] wtype = Types();

        bool ok = Build("MCB,").Adjust400vBreaker(breaker, 'N', codes, wtype);

        Assert.False(ok);
    }

    // ---- PropChgF2Breaker(改訂<116>) ----

    private static MainCircuitResult F2(string frame, string trip)
    {
        MainCircuitResult breaker = new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = "MCB",
                DescriptionRow = "005",
                DescriptionColumn = "001",
                DataType = ["", "F2", "", "", "", "", ""],
            },
        };
        breaker.Data.ElectricalParameterSlots[0].Af = frame;
        breaker.Data.ElectricalParameterSlots[0].At = trip;
        return breaker;
    }

    [Fact]
    public void 耐熱225ATは250AT250AFで選定する()
    {
        MainCircuitResult breaker = F2("00000.000", "00225.000");
        NumericElectricalParameters[] sep = [new(), new(), new()];

        Build("MCB,").AdjustF2Breaker(breaker, sep);

        Assert.Equal(250.0, sep[1].Af);
        Assert.Equal(250.0, sep[2].Af);
        Assert.Equal(250.0, sep[1].At);
        Assert.Equal("00250.000", breaker.Data.ElectricalParameterSlots[1].Af);
        Assert.Equal("00250.000", breaker.Data.ElectricalParameterSlots[1].At);
    }

    [Fact]
    public void 耐熱でも三菱以外指定なら変更しない()
    {
        MainCircuitResult breaker = F2("00000.000", "00225.000");
        NumericElectricalParameters[] sep = [new(), new(), new()];

        Build("MCB+(MK=KM),").AdjustF2Breaker(breaker, sep);

        Assert.Equal(0.0, sep[1].Af);
    }

    [Fact]
    public void 耐熱でも225AT以外は変更しない()
    {
        MainCircuitResult breaker = F2("00000.000", "00100.000");
        NumericElectricalParameters[] sep = [new(), new(), new()];

        Build("MCB,").AdjustF2Breaker(breaker, sep);

        Assert.Equal(0.0, sep[1].Af);
    }

    [Fact]
    public void 耐熱でもフレーム容量指定ありは変更しない()
    {
        MainCircuitResult breaker = F2("00225.000", "00225.000");   // フレーム指定あり
        NumericElectricalParameters[] sep = [new(), new(), new()];

        Build("MCB,").AdjustF2Breaker(breaker, sep);

        Assert.Equal(0.0, sep[1].Af);
    }
}
