using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 機器検索前処理のメーカーコード選定順位上書き(<see cref="EquipmentMakerOverrideAdjuster"/>)の移植検証。
/// 【C原典】PropChgRtrMaker/PropChgRmcbMaker/PropChgNL63Maker/PropChgWHMaker/
///          PropChgINVBPMaker/PropChgGPNMaker(Fysk00.c)。
/// </summary>
public sealed class EquipmentMakerOverrideAdjusterTests
{
    private static string[] Codes(params string[] values)
    {
        string[] result = ["   ", "   ", "   ", "   "];
        for (int i = 0; i < values.Length && i < 4; i++)
        {
            result[i] = values[i];
        }
        return result;
    }

    private static MainCircuitResult Rec(string reservedWord, string makerCode = "   ")
        => new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = reservedWord,
                AttachedParameter = new AttachedParameters { MakerCode = makerCode },
            },
        };

    // ---- PropChgRtrMaker(改訂<27>) ----

    [Fact]
    public void フル2線メーカー未指定RTRは松下Dに固定する()
    {
        MainCircuitResult rtr = Rec("RTR");
        string[] codes = Codes("AA", "BB", "CC");
        int count = 3;

        EquipmentMakerOverrideAdjuster.AdjustRtrMaker(0, rtr, codes, ref count);

        Assert.Equal("D  ", codes[0]);
        Assert.Equal("   ", codes[1]);
        Assert.Equal(1, count);
    }

    [Fact]
    public void フル2線でないRTRは変更しない()
    {
        MainCircuitResult rtr = Rec("RTR");
        string[] codes = Codes("AA");
        int count = 1;

        EquipmentMakerOverrideAdjuster.AdjustRtrMaker(-1, rtr, codes, ref count);

        Assert.Equal("AA", codes[0]);
    }

    [Fact]
    public void メーカー指定ありRTRは変更しない()
    {
        MainCircuitResult rtr = Rec("RTR", "M  ");
        string[] codes = Codes("AA");
        int count = 1;

        EquipmentMakerOverrideAdjuster.AdjustRtrMaker(0, rtr, codes, ref count);

        Assert.Equal("AA", codes[0]);
    }

    // ---- PropChgRmcbMaker(改訂<60>) ----

    [Fact]
    public void コンポ仕様メーカー未指定RMCBは松下Dに固定する()
    {
        MainCircuitResult rmcb = Rec("RMCB");
        string[] codes = Codes("AA", "BB");
        int count = 2;

        EquipmentMakerOverrideAdjuster.AdjustRmcbMaker(1, rmcb, codes, ref count);

        Assert.Equal("D  ", codes[0]);
        Assert.Equal(1, count);
    }

    [Fact]
    public void 特注仕様RMCBは変更しない()
    {
        MainCircuitResult rmcb = Rec("RMCB");
        string[] codes = Codes("AA");
        int count = 1;

        EquipmentMakerOverrideAdjuster.AdjustRmcbMaker(0, rmcb, codes, ref count);

        Assert.Equal("AA", codes[0]);
    }

    // ---- PropChgNL63Maker(改訂<139>/<150>) ----

    private static MainCircuitResult Nl63(string maker)
    {
        MainCircuitResult mcb = new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = "MCB",
                DataType = ["KM", "TL", "", "", "", "", ""],
                AttachedParameter = new AttachedParameters { MakerCode = maker },
            },
        };
        return mcb;
    }

    [Fact]
    public void NL63対象MCBは先頭にKKYを挿入し件数を増やす()
    {
        MainCircuitResult mcb = Nl63("   ");
        string[] codes = Codes("KN", "KY");
        int count = 2;

        EquipmentMakerOverrideAdjuster.AdjustNl63Maker(mcb, codes, ref count);

        Assert.Equal("KKY", codes[0]);
        Assert.Equal("KN ", codes[1]);
        Assert.Equal("KY ", codes[2]);
        Assert.Equal(3, count);
    }

    [Fact]
    public void 協約KN指定のNL63は変更しない()
    {
        MainCircuitResult mcb = Nl63("KN ");
        string[] codes = Codes("KN");
        int count = 1;

        EquipmentMakerOverrideAdjuster.AdjustNl63Maker(mcb, codes, ref count);

        Assert.Equal("KN", codes[0]);
        Assert.Equal(1, count);
    }

    // ---- PropChgWHMaker(改訂<144>) ----

    private static MainCircuitResult Wh(char ph, char wr, string voltage)
        => new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = "WH",
                CircuitPhaseCount = ph,
                CircuitWireType = wr,
                CircuitVoltage = [voltage, "000", "000"],
            },
        };

    [Fact]
    public void QrespoPlusの1P2W210WHはメーカーをMSMNMONに変更する()
    {
        MainCircuitResult wh = Wh('1', '2', "210");
        string[] codes = Codes();
        int count = 0;

        EquipmentMakerOverrideAdjuster.AdjustWhMaker("33335", wh, codes, ref count);

        Assert.Equal("MS ", codes[0]);
        Assert.Equal("MN ", codes[1]);
        Assert.Equal("M  ", codes[2]);
        Assert.Equal("ON ", codes[3]);
        Assert.Equal(4, count);
    }

    [Fact]
    public void QrespoPlus以外のWHは変更しない()
    {
        MainCircuitResult wh = Wh('1', '2', "210");
        string[] codes = Codes("AA");
        int count = 1;

        EquipmentMakerOverrideAdjuster.AdjustWhMaker("00000", wh, codes, ref count);

        Assert.Equal("AA", codes[0]);
    }

    // ---- PropChgINVBPMaker(改訂<148>) ----

    private static MainCircuitResult Invbp(string reservedWord, string loadCapacity = "0000000")
        => new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = reservedWord,
                SpecialReservedWordKind = '7',
                AttachedParameter = new AttachedParameters { LoadCapacity = loadCapacity },
            },
        };

    [Fact]
    public void INVBPのMCは三菱MNに固定する()
    {
        MainCircuitResult mc = Invbp("MC");
        string[] codes = Codes("AA", "BB");
        int count = 2;

        EquipmentMakerOverrideAdjuster.AdjustInvbpMaker(mc, codes, ref count);

        Assert.Equal("MN ", codes[0]);
        Assert.Equal("   ", codes[1]);
        Assert.Equal(1, count);
    }

    [Fact]
    public void INVBPのTHRは負荷容量22超30以下で三菱大形MSにする()
    {
        MainCircuitResult thr = Invbp("THR", "0025000");   // 25kW
        string[] codes = Codes();
        int count = 0;

        EquipmentMakerOverrideAdjuster.AdjustInvbpMaker(thr, codes, ref count);

        Assert.Equal("MS ", codes[0]);
        Assert.Equal(1, count);
    }

    [Fact]
    public void INVBPのTHRは範囲外で三菱MNにする()
    {
        MainCircuitResult thr = Invbp("THR", "0015000");   // 15kW
        string[] codes = Codes();
        int count = 0;

        EquipmentMakerOverrideAdjuster.AdjustInvbpMaker(thr, codes, ref count);

        Assert.Equal("MN ", codes[0]);
    }

    [Fact]
    public void INVBPでない機器は変更しない()
    {
        MainCircuitResult mc = new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = "MC",
                SpecialReservedWordKind = ' ',   // INVBP でない
            },
        };
        string[] codes = Codes("AA");
        int count = 1;

        EquipmentMakerOverrideAdjuster.AdjustInvbpMaker(mc, codes, ref count);

        Assert.Equal("AA", codes[0]);
    }

    // ---- PropChgGPNMaker(改訂<141>, 制御) ----

    private static ControlEquipmentInfo Gpn(string reservedWord, string makerCode = "")
        => new() { ReservedWord = reservedWord, MakerCode = makerCode };

    [Fact]
    public void GPNはOMの直前にOMNを挿入する()
    {
        ControlEquipmentInfo gpn = Gpn("GPN");
        string[] codes = Codes("AA", "OM", "BB");
        int count = 3;

        EquipmentMakerOverrideAdjuster.AdjustGpnMaker(gpn, codes, ref count);

        Assert.Equal("AA ", codes[0]);
        Assert.Equal("OMN", codes[1]);
        Assert.Equal("OM ", codes[2]);
        Assert.Equal("BB ", codes[3]);
        Assert.Equal(4, count);
    }

    [Fact]
    public void メーカー指定ありGPNは変更しない()
    {
        ControlEquipmentInfo gpn = Gpn("GPN", "OM ");
        string[] codes = Codes("OM");
        int count = 1;

        EquipmentMakerOverrideAdjuster.AdjustGpnMaker(gpn, codes, ref count);

        Assert.Equal("OM", codes[0]);
        Assert.Equal(1, count);
    }

    [Fact]
    public void OMを含まないGPNは変更しない()
    {
        ControlEquipmentInfo gpn = Gpn("GPN");
        string[] codes = Codes("AA", "BB");
        int count = 2;

        EquipmentMakerOverrideAdjuster.AdjustGpnMaker(gpn, codes, ref count);

        Assert.Equal("AA", codes[0]);
        Assert.Equal(2, count);
    }
}
