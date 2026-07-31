using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 積算エリアセット(<see cref="AccumulationAreaSetter"/>)の移植検証。
/// 【C原典】Fyss36_Set_Seki/Get_Pdno/Get_Are1/Get_Are2(toku/sekkei/src/Fyss36.c)。
/// </summary>
public sealed class AccumulationAreaSetterTests
{
    private static MainCircuitResult Row(
        string datano,
        string oyatno = "000",
        string goyano = "000",
        string kno = "000",
        char kpaph = '0',
        char kpawr = '0',
        char kpap = '0',
        string heino = "000",
        char ahassei = ' ',
        string denryu = "",
        string fpalw1 = "",
        string fpalw2 = "",
        string yoyaku = "")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ParentSequenceNumber = oyatno,
                GroupParentSequenceNumber = goyano,
                SystemNumber = kno,
                CircuitPhaseCount = kpaph,
                CircuitWireType = kpawr,
                CircuitPoleCount = kpap,
                ParallelNumber = heino,
                LoadSourceKind = ahassei,
                EnergizingCurrent = denryu,
                ReservedWord = yoyaku,
            },
        };
        r.Data.AttachedParameter.LoadKind = fpalw1;
        r.Data.AttachedParameter.LoadCapacity = fpalw2;
        return r;
    }

    [Fact]
    public void 単相2線の電動機は相Xの機器種別Bに通電電流値をセットする()
    {
        MainCircuitResult p = Row("001", kno: "001", yoyaku: "P", kpaph: '1');
        MainCircuitResult parent = Row("002", oyatno: "001", kno: "001", kpaph: '1', kpawr: '2');
        MainCircuitResult load = Row("003", oyatno: "002", kno: "001", kpaph: '1', kpawr: '2', kpap: '1',
            ahassei: '1', denryu: "00005.00", fpalw1: "M ", fpalw2: "0037000");

        AccumulationAreaSetter.SetLoadSourceAccumulation([p, parent, load], 3);

        Assert.Equal(5.0, load.Work.AccumulationSlots[3].B);
        Assert.Equal(0.0, load.Work.AccumulationSlots[3].A);
        Assert.Equal(0.0, load.Work.AccumulationSlots[0].B);
    }

    [Fact]
    public void 三相の電動機は相RSTのA_M_Sに電流と負荷容量をセットする()
    {
        MainCircuitResult p = Row("001", kno: "001", yoyaku: "P", kpaph: '3');
        MainCircuitResult parent = Row("002", oyatno: "001", kno: "001", kpaph: '3', kpawr: '3');
        MainCircuitResult load = Row("003", oyatno: "002", kno: "001", kpaph: '3', kpawr: '3',
            ahassei: '1', denryu: "00010.00", fpalw1: "M ", fpalw2: "0007500");

        AccumulationAreaSetter.SetLoadSourceAccumulation([p, parent, load], 3);

        foreach (int slot in new[] { 0, 1, 2 })
        {
            Assert.Equal(10.0, load.Work.AccumulationSlots[slot].A);
            Assert.Equal(7500.0, load.Work.AccumulationSlots[slot].M);
            Assert.Equal(7500.0, load.Work.AccumulationSlots[slot].S);
        }
    }

    [Fact]
    public void 直流親の機器は相XYに機器種別で電流をセットする()
    {
        // グループ親が DC(kpaph='0' kpawr='0') → 相 X,Y。非電動機 → 機器種別 C。
        MainCircuitResult p = Row("001", kno: "001", yoyaku: "P", kpaph: '1');
        MainCircuitResult parent = Row("002", oyatno: "001", kno: "001", kpaph: '0', kpawr: '0');
        MainCircuitResult load = Row("003", oyatno: "002", kno: "001", kpaph: '1', kpawr: '2', kpap: '1',
            ahassei: '1', denryu: "00003.00", fpalw1: "H ", fpalw2: "0001000");

        AccumulationAreaSetter.SetLoadSourceAccumulation([p, parent, load], 3);

        Assert.Equal(3.0, load.Work.AccumulationSlots[3].C);
        Assert.Equal(3.0, load.Work.AccumulationSlots[4].C);
    }

    [Fact]
    public void 負荷容量が無い単相は機器種別Eに電流をセットする()
    {
        MainCircuitResult p = Row("001", kno: "001", yoyaku: "P", kpaph: '1');
        MainCircuitResult parent = Row("002", oyatno: "001", kno: "001", kpaph: '1', kpawr: '2');
        MainCircuitResult load = Row("003", oyatno: "002", kno: "001", kpaph: '1', kpawr: '2', kpap: '1',
            ahassei: '1', denryu: "00005.00"); // fpalw2 空 → 負荷容量 0

        AccumulationAreaSetter.SetLoadSourceAccumulation([p, parent, load], 3);

        Assert.Equal(5.0, load.Work.AccumulationSlots[3].E);
    }

    [Fact]
    public void グループ親データ追番が親データ追番より優先される()
    {
        // oyatno は存在しない "099"、goyano="002" が使われることを確認(三相 → R,S,T 積算)。
        MainCircuitResult p = Row("001", kno: "001", yoyaku: "P", kpaph: '3');
        MainCircuitResult groupParent = Row("002", oyatno: "001", kno: "001", kpaph: '3', kpawr: '3');
        MainCircuitResult load = Row("003", oyatno: "099", goyano: "002", kno: "001", kpaph: '3', kpawr: '3',
            ahassei: '1', denryu: "00010.00", fpalw1: "M ", fpalw2: "0007500");

        AccumulationAreaSetter.SetLoadSourceAccumulation([p, groupParent, load], 3);

        Assert.Equal(10.0, load.Work.AccumulationSlots[0].A);
    }

    [Theory]
    [InlineData("001", 3)] // 並列追番が奇数 → X
    [InlineData("002", 4)] // 並列追番が偶数 → Y
    public void 単相3線親配下の並列追番でXかYが切り替わる(string heino, int expectedSlot)
    {
        MainCircuitResult p = Row("001", kno: "001", yoyaku: "P", kpaph: '1');
        MainCircuitResult parent = Row("002", oyatno: "001", kno: "001", kpaph: '1', kpawr: '3');
        MainCircuitResult load = Row("003", oyatno: "002", kno: "001", kpaph: '1', kpawr: '2', kpap: '1',
            heino: heino, ahassei: '1', denryu: "00005.00", fpalw1: "M ", fpalw2: "0037000");

        AccumulationAreaSetter.SetLoadSourceAccumulation([p, parent, load], 3);

        Assert.Equal(5.0, load.Work.AccumulationSlots[expectedSlot].B);
    }

    [Fact]
    public void 存在しないデータ追番は何もしない()
    {
        MainCircuitResult load = Row("001", kno: "001", kpaph: '1');

        AccumulationAreaSetter.SetLoadSourceAccumulation([load], 99);

        Assert.Equal(0.0, load.Work.AccumulationSlots[0].A);
    }

    [Fact]
    public void 空リストでも例外にならない()
    {
        AccumulationAreaSetter.SetLoadSourceAccumulation([], 1);
    }
}
