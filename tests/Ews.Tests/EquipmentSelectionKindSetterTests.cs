using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 機器選定区分セット(<see cref="EquipmentSelectionKindSetter"/>)の移植検証。
/// 【C原典】Fyss33_KikiSentei_Set 一式(toku/sekkei/src/Fyss33.c)。
/// </summary>
public sealed class EquipmentSelectionKindSetterTests
{
    private static readonly Func<string, char> NoMotor = _ => ' ';

    private static MainCircuitResult Row(
        string datano,
        string oyatno = "000",
        char ksyubetu = '1',
        char kiryoso = '1',
        char ahassei = ' ',
        string fpac = "",
        string fpalw1 = "",
        string fpalw2 = "",
        string denryu = "",
        string yoyaku = "",
        char mattan = ' ',
        string gyocd = "",
        string gyoglno = "000")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                SystemKind = ksyubetu,
                CircuitElement = kiryoso,
                LoadSourceKind = ahassei,
                ParentSequenceNumber = oyatno,
                ReservedWord = yoyaku,
                TerminalKind = mattan,
                LineTypeCode = gyocd,
                LineTypeGroupNumber = gyoglno,
                EnergizingCurrent = denryu,
            },
        };
        r.Data.AttachedParameter.ControlPowerNumber = fpac;
        r.Data.AttachedParameter.LoadKind = fpalw1;
        r.Data.AttachedParameter.LoadCapacity = fpalw2;
        return r;
    }

    [Fact]
    public void 負荷発生元が2件未満なら全主回路機器が選定対象1になる()
    {
        MainCircuitResult a = Row("001", ahassei: '1');   // 負荷発生元 → fcnt=1
        MainCircuitResult b = Row("002", oyatno: "001");  // 非発生元

        EquipmentSelectionKindSetter.SetEquipmentSelectionKind([a, b], NoMotor);

        Assert.Equal('1', a.Work.EquipmentSelectionKind);
        Assert.Equal('1', b.Work.EquipmentSelectionKind);
    }

    [Fact]
    public void 制御電源番号ありは負荷発生元として数えない()
    {
        // 2件とも ahassei='1' だが片方は fpac 非空 → fcnt=1<2 → 全て '1'。
        MainCircuitResult a = Row("001", ahassei: '1', fpac: "01");
        MainCircuitResult b = Row("002", oyatno: "001", ahassei: '1');

        EquipmentSelectionKindSetter.SetEquipmentSelectionKind([a, b], NoMotor);

        Assert.Equal('1', a.Work.EquipmentSelectionKind);
        Assert.Equal('1', b.Work.EquipmentSelectionKind);
    }

    [Fact]
    public void 独立した負荷発生元が複数下流にある機器は選定区分2になる()
    {
        // MCB(001) 配下に独立した負荷発生元 MC1(002)/MC2(003) が2件。
        MainCircuitResult mcb = Row("001", yoyaku: "MCB");
        MainCircuitResult mc1 = Row("002", oyatno: "001", ahassei: '1');
        MainCircuitResult mc2 = Row("003", oyatno: "001", ahassei: '1');

        EquipmentSelectionKindSetter.SetEquipmentSelectionKind([mcb, mc1, mc2], NoMotor);

        Assert.Equal('2', mcb.Work.EquipmentSelectionKind);
        Assert.Equal('1', mc1.Work.EquipmentSelectionKind);
        Assert.Equal('1', mc2.Work.EquipmentSelectionKind);
    }

    [Fact]
    public void 下流負荷発生元が直列で実質1件なら選定区分1になる()
    {
        // MC2(003) は MC1(002) の下流 → 重複排除で独立発生元は1件 → MCB は '1'。
        MainCircuitResult mcb = Row("001", yoyaku: "MCB");
        MainCircuitResult mc1 = Row("002", oyatno: "001", ahassei: '1');
        MainCircuitResult mc2 = Row("003", oyatno: "002", ahassei: '1');

        EquipmentSelectionKindSetter.SetEquipmentSelectionKind([mcb, mc1, mc2], NoMotor);

        Assert.Equal('1', mcb.Work.EquipmentSelectionKind);
    }

    [Fact]
    public void 選定区分3は発生しない_C原典のデッドコード忠実()
    {
        // Shori2 第1ループが == 誤りで '3' を種付けしないため、'3' は決して現れない。
        MainCircuitResult mcb = Row("001", yoyaku: "MCB");
        MainCircuitResult mc1 = Row("002", oyatno: "001", ahassei: '1');
        MainCircuitResult mc2 = Row("003", oyatno: "001", ahassei: '1');

        EquipmentSelectionKindSetter.SetEquipmentSelectionKind([mcb, mc1, mc2], NoMotor);

        Assert.All(new[] { mcb, mc1, mc2 }, r => Assert.NotEqual('3', r.Work.EquipmentSelectionKind));
    }

    [Fact]
    public void 負荷容量のある末端から上流へ負荷情報を伝播し始動区分1を設定する()
    {
        MainCircuitResult p = Row("001");
        MainCircuitResult mcb = Row("002", oyatno: "001");
        MainCircuitResult mc = Row("003", oyatno: "002", mattan: '1',
            fpalw1: "M ", fpalw2: "0037000", denryu: "00015.20", yoyaku: "MC");

        EquipmentSelectionKindSetter.SetEquipmentSelectionKind([p, mcb, mc], NoMotor);

        Assert.Equal('1', mc.Work.StartCircuitKind);
        Assert.Equal('1', mcb.Work.StartCircuitKind);
        Assert.Equal('1', p.Work.StartCircuitKind);
        // 負荷情報が上流へ伝播。
        Assert.Equal("0037000", mcb.Data.AttachedParameter.LoadCapacity);
        Assert.Equal("M ", p.Data.AttachedParameter.LoadKind);
        Assert.Equal("00015.20", p.Data.EnergizingCurrent);
    }

    [Fact]
    public void スターデルタ系の末端は始動区分2を設定する()
    {
        MainCircuitResult mgsd = Row("001", mattan: '1', fpalw2: "0037000", yoyaku: "MGSD");

        EquipmentSelectionKindSetter.SetEquipmentSelectionKind([mgsd], NoMotor);

        Assert.Equal('2', mgsd.Work.StartCircuitKind);
    }

    [Fact]
    public void 選定区分2の電動機大分類は下流電動機の負荷種類をコピーする()
    {
        // MCB(001) が '2' になり、下流に電動機負荷発生元 MC1(同一行種) → MCB へ "M " コピー。
        MainCircuitResult mcb = Row("001", yoyaku: "MCB", gyocd: "X", gyoglno: "001");
        MainCircuitResult mc1 = Row("002", oyatno: "001", ahassei: '1', fpalw1: "M ", gyocd: "X", gyoglno: "001");
        MainCircuitResult mc2 = Row("003", oyatno: "001", ahassei: '1', fpalw1: "M ", gyocd: "X", gyoglno: "001");

        EquipmentSelectionKindSetter.SetEquipmentSelectionKind([mcb, mc1, mc2], rw => rw == "MCB" ? '1' : ' ');

        Assert.Equal('2', mcb.Work.EquipmentSelectionKind);
        Assert.Equal("M ", mcb.Data.AttachedParameter.LoadKind);
    }

    [Fact]
    public void 電動機大分類でなければ負荷種類はコピーしない()
    {
        MainCircuitResult mcb = Row("001", yoyaku: "MCB", gyocd: "X", gyoglno: "001");
        MainCircuitResult mc1 = Row("002", oyatno: "001", ahassei: '1', fpalw1: "M ", gyocd: "X", gyoglno: "001");
        MainCircuitResult mc2 = Row("003", oyatno: "001", ahassei: '1', fpalw1: "M ", gyocd: "X", gyoglno: "001");

        EquipmentSelectionKindSetter.SetEquipmentSelectionKind([mcb, mc1, mc2], NoMotor);

        Assert.Equal('2', mcb.Work.EquipmentSelectionKind);
        Assert.Equal("", mcb.Data.AttachedParameter.LoadKind);
    }

    [Fact]
    public void 空リストでも例外にならない()
    {
        EquipmentSelectionKindSetter.SetEquipmentSelectionKind([], NoMotor);
    }
}
