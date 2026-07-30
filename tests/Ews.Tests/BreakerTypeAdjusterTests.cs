using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 機器検索前処理のブレーカ系タイプ調整(<see cref="BreakerTypeAdjuster"/>)の移植検証。
/// 【C原典】PropChgMcbType/PropChgOyaMcbType/PropChgPluginType/PropChgM10AfBreaker/
///          PropChgLaClass1Type(Fysk00.c)。
/// </summary>
public sealed class BreakerTypeAdjusterTests
{
    private static string[] Types(params string[] values)
    {
        string[] result = ["", "", "", "", "", "", ""];
        for (int i = 0; i < values.Length && i < 7; i++)
        {
            result[i] = values[i];
        }
        return result;
    }

    // ---- PropChgMcbType(改訂<2>) ----

    private static MainCircuitResult Power(string datano, char ph, char wr, char bn)
    {
        MainCircuitResult power = new()
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ReservedWord = "P",
                CircuitPhaseCount = ph,
                CircuitWireType = wr,
            },
        };
        power.Data.ElectricalParameterSlots[0].Bn = bn;
        return power;
    }

    private static MainCircuitResult Mcb(string datano, string oyatno, string lineTypeCode)
        => new()
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ReservedWord = "MCB",
                LineTypeCode = lineTypeCode,
                ParentSequenceNumber = oyatno,
            },
        };

    [Fact]
    public void 分岐MCBは単相2線電源で協約型に変更する()
    {
        MainCircuitResult power = Power("001", '1', '2', '0');
        MainCircuitResult mcb = Mcb("002", "001", "B");
        string[] display = Types();

        BreakerTypeAdjuster.AdjustBranchMcbType(mcb, [power, mcb], Types(), display);

        Assert.Equal("KY     ", display[0]);
        Assert.Equal("KM     ", display[1]);
    }

    [Fact]
    public void 分岐MCBは制御盤電源では変更しない()
    {
        MainCircuitResult power = Power("001", '1', '2', '5');   // epabn=='5' 制御盤
        MainCircuitResult mcb = Mcb("002", "001", "B");
        string[] display = Types();

        BreakerTypeAdjuster.AdjustBranchMcbType(mcb, [power, mcb], Types(), display);

        Assert.Equal("", display[0]);
    }

    [Fact]
    public void 分岐MCBはdtype確定済なら変更しない()
    {
        MainCircuitResult power = Power("001", '1', '2', '0');
        MainCircuitResult mcb = Mcb("002", "001", "B");
        string[] display = Types();

        BreakerTypeAdjuster.AdjustBranchMcbType(mcb, [power, mcb], Types("KE"), display);

        Assert.Equal("", display[0]);
    }

    [Fact]
    public void 分岐MCBは三相電源では変更しない()
    {
        MainCircuitResult power = Power("001", '3', '4', '0');
        MainCircuitResult mcb = Mcb("002", "001", "B");
        string[] display = Types();

        BreakerTypeAdjuster.AdjustBranchMcbType(mcb, [power, mcb], Types(), display);

        Assert.Equal("", display[0]);
    }

    // ---- PropChgOyaMcbType(改訂<48>) ----

    private static MainCircuitResult ParentBreaker(string datano)
        => new()
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ReservedWord = "MCB",
                CircuitPhaseCount = '1',
                CircuitWireType = '3',
                CircuitClass = 'M',
                IncomingNumber = "001",
            },
        };

    private static MainCircuitResult ChildBreaker(string oyatno, string pole)
    {
        MainCircuitResult child = new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = "MCB",
                ParentSequenceNumber = oyatno,
                IncomingNumber = "001",
            },
        };
        child.Data.ElectricalParameterSlots[0].P = pole;
        return child;
    }

    [Fact]
    public void 主幹MCBは3P子分岐がある1P3Wで経済型に変更する()
    {
        MainCircuitResult parent = ParentBreaker("001");
        MainCircuitResult child = ChildBreaker("001", "003");
        string[] display = Types();

        BreakerTypeAdjuster.AdjustParentMcbType(parent, [parent, child], Types(), display);

        Assert.Equal("ET     ", display[0]);
        Assert.Equal("KY     ", display[1]);
    }

    [Fact]
    public void 主幹MCBは既に経済型なら変更しない()
    {
        MainCircuitResult parent = ParentBreaker("001");
        MainCircuitResult child = ChildBreaker("001", "003");
        string[] display = Types("ET");

        BreakerTypeAdjuster.AdjustParentMcbType(parent, [parent, child], Types(), display);

        Assert.Equal("ET", display[0]);   // 変更されない(元のまま)
        Assert.Equal("", display[1]);
    }

    [Fact]
    public void 主幹MCBは子が3Pでないと変更しない()
    {
        MainCircuitResult parent = ParentBreaker("001");
        MainCircuitResult child = ChildBreaker("001", "002");   // 2P
        string[] display = Types();

        BreakerTypeAdjuster.AdjustParentMcbType(parent, [parent, child], Types(), display);

        Assert.Equal("", display[0]);
    }

    // ---- PropChgPluginType(改訂<34>) ----

    [Fact]
    public void プラグインCHは接続相NOTHINGをRNに変更する()
    {
        MainCircuitResult breaker = new()
        {
            Data = new MainCircuitData
            {
                DataType = ["CH", "", "", "NOTHING", "", "", ""],
            },
        };
        breaker.Data.ElectricalParameterSlots[0].E = "1";
        string[] display = Types();

        BreakerTypeAdjuster.AdjustPluginType(breaker, display);

        Assert.Equal("RN     ", display[3]);
    }

    [Fact]
    public void プラグインでも1Eでないと変更しない()
    {
        MainCircuitResult breaker = new()
        {
            Data = new MainCircuitData
            {
                DataType = ["CH", "", "", "NOTHING", "", "", ""],
            },
        };
        breaker.Data.ElectricalParameterSlots[0].E = "0";
        string[] display = Types();

        BreakerTypeAdjuster.AdjustPluginType(breaker, display);

        Assert.Equal("", display[3]);
    }

    // ---- PropChgM10AfBreaker(改訂<118>) ----

    [Fact]
    public void 三菱3PのELB10AFは50AFに変更する()
    {
        MainCircuitResult elb = new()
        {
            Data = new MainCircuitData { ReservedWord = "ELB", CircuitPoleCount = '3' },
        };
        NumericElectricalParameters[] sep = [new(), new(), new()];
        sep[1].Af = 10.0;

        BreakerTypeAdjuster.AdjustM10AfBreaker(elb, "M  ", sep);

        Assert.Equal(50.0, sep[1].Af);
        Assert.Equal(50.0, sep[2].Af);
    }

    [Fact]
    public void ELBでも三菱協約以外は変更しない()
    {
        MainCircuitResult elb = new()
        {
            Data = new MainCircuitData { ReservedWord = "ELB", CircuitPoleCount = '3' },
        };
        NumericElectricalParameters[] sep = [new(), new(), new()];
        sep[1].Af = 10.0;

        BreakerTypeAdjuster.AdjustM10AfBreaker(elb, "TA ", sep);

        Assert.Equal(10.0, sep[1].Af);
    }

    // ---- PropChgLaClass1Type(改訂<143>) ----

    [Fact]
    public void LAのCLASS1はタイプ2をRSに設定する()
    {
        MainCircuitResult la = new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = "LA",
                DataType = ["1C", "", "NOTHING", "", "", "", ""],
            },
        };

        BreakerTypeAdjuster.AdjustLaClass1Type(la);

        Assert.Equal("RS     ", la.Data.DataType[2]);
    }

    [Fact]
    public void LA以外はタイプ2を変更しない()
    {
        MainCircuitResult other = new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = "MCB",
                DataType = ["1C", "", "NOTHING", "", "", "", ""],
            },
        };

        BreakerTypeAdjuster.AdjustLaClass1Type(other);

        Assert.Equal("NOTHING", other.Data.DataType[2]);
    }
}
