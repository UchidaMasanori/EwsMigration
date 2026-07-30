using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 機器検索前処理の WH/MC 電気値・タイプ補正(<see cref="McWhElectricalAdjuster"/>)の移植検証。
/// 【C原典】PropChgWHType/PropChgMcMaker/PropChgTAMC_epav2/PropWhmFukaDenFromChild(Fysk00.c)。
/// </summary>
public sealed class McWhElectricalAdjusterTests
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

    // ---- PropChgWHType(改訂<23>) ----

    [Fact]
    public void WHでKMタイプがあると表示タイプ2をクリアする()
    {
        MainCircuitResult wh = new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = "WH",
                DataType = ["NOTHING", "KM", "", "", "", "", ""],
                AttachedParameter = new AttachedParameters { SpFutureMountKind = ' ' },
            },
        };
        string[] display = Types("NOTHING", "KE");

        McWhElectricalAdjuster.AdjustWhType(wh, display);

        Assert.Equal("       ", display[1]);
    }

    [Fact]
    public void SP枠のWHは表示タイプ2をクリアしない()
    {
        MainCircuitResult wh = new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = "WH",
                DataType = ["NOTHING", "KM", "", "", "", "", ""],
                AttachedParameter = new AttachedParameters { SpFutureMountKind = '1' },
            },
        };
        string[] display = Types("NOTHING", "KE");

        McWhElectricalAdjuster.AdjustWhType(wh, display);

        Assert.Equal("KE", display[1]);
    }

    // ---- PropChgMcMaker(改訂<37>/<38>) ----

    private static MainCircuitResult TaMc(string pole, string a2)
    {
        MainCircuitResult mc = new()
        {
            Data = new MainCircuitData { ReservedWord = "MC" },
        };
        mc.Data.ElectricalParameterSlots[1].P = pole;
        mc.Data.ElectricalParameterSlots[1].A2 = a2;
        return mc;
    }

    [Fact]
    public void 大陸MC3P50Aは三菱に固定しSKタイプを選定する()
    {
        MainCircuitResult mc = TaMc("002", "00050.000");
        mc.Data.ElectricalParameterSlots[2].Vc = "200";
        string[] codes = ["TA ", "XX ", "   ", "   "];
        int count = 2;
        string[] dtype = Types();
        string[] wtype = Types();
        int tsu = 0;
        NumericElectricalParameters[] sep = [new(), new(), new()];

        McWhElectricalAdjuster.AdjustMcMaker(mc, codes, ref count, dtype, wtype, ref tsu, sep);

        Assert.Equal("MN ", codes[0]);
        Assert.Equal(1, count);
        Assert.Equal("SK     ", dtype[0]);
        Assert.Equal("SK     ", wtype[0]);
        Assert.Equal(1, tsu);
        Assert.Equal(3.0, sep[1].P);
        Assert.Equal("003", mc.Data.ElectricalParameterSlots[1].P);
        Assert.Equal(220.0, sep[1].V2[0]);
        Assert.Equal("000220.0", mc.Data.ElectricalParameterSlots[1].V2[0]);
        Assert.Equal(200.0, sep[1].Vc);
        Assert.Equal("200", mc.Data.ElectricalParameterSlots[1].Vc);
    }

    [Fact]
    public void 大陸MC3P20Aは三菱に固定するがSKタイプにはしない()
    {
        MainCircuitResult mc = TaMc("003", "00020.000");
        string[] codes = ["TA ", "   ", "   ", "   "];
        int count = 1;
        string[] dtype = Types();
        string[] wtype = Types();
        int tsu = 0;
        NumericElectricalParameters[] sep = [new(), new(), new()];

        McWhElectricalAdjuster.AdjustMcMaker(mc, codes, ref count, dtype, wtype, ref tsu, sep);

        Assert.Equal("MN ", codes[0]);
        Assert.Equal("", dtype[0]);
        Assert.Equal(0, tsu);
    }

    [Fact]
    public void TA製でないMCは変更しない()
    {
        MainCircuitResult mc = TaMc("002", "00050.000");
        string[] codes = ["M  ", "   ", "   ", "   "];
        int count = 1;
        string[] dtype = Types();
        string[] wtype = Types();
        int tsu = 0;
        NumericElectricalParameters[] sep = [new(), new(), new()];

        McWhElectricalAdjuster.AdjustMcMaker(mc, codes, ref count, dtype, wtype, ref tsu, sep);

        Assert.Equal("M  ", codes[0]);
        Assert.Equal("", dtype[0]);
    }

    // ---- PropChgTAMC_epav2(改訂<37>) ----

    [Fact]
    public void 大陸MCは定格電圧210を220に強制設定する()
    {
        MainCircuitResult mc = new() { Data = new MainCircuitData { ReservedWord = "MC" } };
        mc.Data.ElectricalParameterSlots[2].V2[0] = "000210.0";   // 210V (<=400)
        mc.Data.ElectricalParameterSlots[2].Vc = "200";
        string[] codes = ["TA ", "   ", "   ", "   "];
        NumericElectricalParameters[] sep = [new(), new(), new()];

        McWhElectricalAdjuster.AdjustTaMcVoltage(mc, codes, sep);

        Assert.Equal("000220.0", mc.Data.ElectricalParameterSlots[2].V2[0]);
        Assert.Equal(220.0, sep[1].V2[0]);
        Assert.Equal(220.0, sep[2].V2[0]);
        Assert.Equal(200.0, sep[1].Vc);
    }

    [Fact]
    public void 大陸MCは定格電圧420を440に強制設定する()
    {
        MainCircuitResult mc = new() { Data = new MainCircuitData { ReservedWord = "MC" } };
        mc.Data.ElectricalParameterSlots[2].V2[0] = "000420.0";   // 420V (>400)
        string[] codes = ["TA ", "   ", "   ", "   "];
        NumericElectricalParameters[] sep = [new(), new(), new()];

        McWhElectricalAdjuster.AdjustTaMcVoltage(mc, codes, sep);

        Assert.Equal("000440.0", mc.Data.ElectricalParameterSlots[2].V2[0]);
        Assert.Equal(440.0, sep[1].V2[0]);
    }

    // ---- PropWhmFukaDenFromChild(改訂<78>/<155>) ----

    private static MainCircuitResult Whm(string datano)
    {
        MainCircuitResult whm = new()
        {
            SequenceNumber = datano,
            Data = new MainCircuitData { ReservedWord = "WH", LineTypeCode = "SM" },
        };
        whm.Data.ElectricalParameterSlots[0].Ph2 = ["1", "0"];
        whm.Data.ElectricalParameterSlots[0].Wr2 = ["2", "0"];
        return whm;
    }

    private static MainCircuitResult Child(string oyatno, string loadVoltage)
    {
        MainCircuitResult child = new()
        {
            Data = new MainCircuitData
            {
                ParentSequenceNumber = oyatno,
                AttachedParameter = new AttachedParameters(),
            },
        };
        child.Data.AttachedParameter.LoadVoltage[0] = loadVoltage;
        return child;
    }

    [Fact]
    public void WHM1P2Wは子のLV200から定格電圧を200に設定する()
    {
        MainCircuitResult whm = Whm("005");
        MainCircuitResult child = Child("005", "200");
        NumericElectricalParameters[] sep = [new(), new(), new()];

        McWhElectricalAdjuster.AdjustWhmFukaDenFromChild(whm, [whm, child], sep);

        Assert.Equal(200.0, sep[1].V2[0]);
        Assert.Equal(200.0, sep[2].V2[0]);
        Assert.Equal("000200.0", whm.Data.ElectricalParameterSlots[1].V2[0]);
        Assert.Equal("000200.0", whm.Data.ElectricalParameterSlots[2].V2[0]);
    }

    [Fact]
    public void 子のLVが200でないWHMは変更しない()
    {
        MainCircuitResult whm = Whm("005");
        MainCircuitResult child = Child("005", "100");
        NumericElectricalParameters[] sep = [new(), new(), new()];

        McWhElectricalAdjuster.AdjustWhmFukaDenFromChild(whm, [whm, child], sep);

        Assert.Equal(0.0, sep[1].V2[0]);
    }

    [Fact]
    public void WHMに入力電圧があると子を参照しない()
    {
        MainCircuitResult whm = Whm("005");
        whm.Data.ElectricalParameterSlots[0].V2[0] = "000210.0";   // 入力あり
        MainCircuitResult child = Child("005", "200");
        NumericElectricalParameters[] sep = [new(), new(), new()];

        McWhElectricalAdjuster.AdjustWhmFukaDenFromChild(whm, [whm, child], sep);

        Assert.Equal(0.0, sep[1].V2[0]);
    }
}
