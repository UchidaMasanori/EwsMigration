using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 末端回路の通電電流値算出オーケストレータ(<see cref="AccumulationAreaSetter.SetTerminalCircuitCurrent"/>)の
/// 移植検証。【C原典】Fyss36_MattanKairo_Iset(toku/sekkei/src/Fyss36.c)。
/// 積算エリアセット等の下請けは <see cref="AccumulationAreaSetterTests"/> で個別検証済み。ここでは
/// 4 ループの分岐(実行条件/スキップ条件)・ＳＣ積算のデリゲート境界・エラー通知を検証する。
/// </summary>
public sealed class AccumulationAreaSetterOrchestratorTests
{
    private static MainCircuitResult Row(
        string datano,
        string oyatno = "000",
        string kno = "000",
        char kpaph = '0',
        char kpawr = '0',
        char kpap = '0',
        string kaisono = "000",
        string heino = "000",
        char ksyubetu = '1',
        char ahassei = ' ',
        char kiryoso = ' ',
        char mattan = ' ',
        char kaetyp = ' ',
        char tokkbn = ' ',
        char sentflg = ' ',
        string fpac = "  ",
        string fpaln1 = "",
        string denryu = "00000.00",
        string yoyaku = "")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ParentSequenceNumber = oyatno,
                SystemNumber = kno,
                CircuitPhaseCount = kpaph,
                CircuitWireType = kpawr,
                CircuitPoleCount = kpap,
                HierarchyNumber = kaisono,
                ParallelNumber = heino,
                SystemKind = ksyubetu,
                LoadSourceKind = ahassei,
                CircuitElement = kiryoso,
                TerminalKind = mattan,
                SwitchType = kaetyp,
                SpecialReservedWordKind = tokkbn,
                EnergizingCurrent = denryu,
                ReservedWord = yoyaku,
            },
        };
        r.Data.AttachedParameter.ControlPowerNumber = fpac;
        r.Data.AttachedParameter.LoadName[1] = fpaln1;
        r.Work.LeadingEquipmentFlag = sentflg;
        return r;
    }

    // ── ループ1: 負荷発生元の積算エリアセット ───────────────────────────────

    [Fact]
    public void 負荷発生元で制御電源番号が空なら積算エリアがセットされる()
    {
        MainCircuitResult p = Row("001", kno: "001", yoyaku: "P", kpaph: '3');
        MainCircuitResult parent = Row("002", oyatno: "001", kno: "001", kpaph: '3', kpawr: '3');
        MainCircuitResult load = Row("003", oyatno: "002", kno: "001", kpaph: '3', kpawr: '3',
            ahassei: '1', denryu: "00010.00");
        load.Data.AttachedParameter.LoadKind = "M ";
        load.Data.AttachedParameter.LoadCapacity = "0007500";

        AccumulationAreaSetter.SetTerminalCircuitCurrent([p, parent, load]);

        Assert.Equal(10.0, load.Work.AccumulationSlots[0].A);
        Assert.Equal(7500.0, load.Work.AccumulationSlots[0].M);
    }

    [Fact]
    public void 負荷発生元でも制御電源番号が非空なら積算エリアをセットしない()
    {
        MainCircuitResult p = Row("001", kno: "001", yoyaku: "P", kpaph: '3');
        MainCircuitResult parent = Row("002", oyatno: "001", kno: "001", kpaph: '3', kpawr: '3');
        MainCircuitResult load = Row("003", oyatno: "002", kno: "001", kpaph: '3', kpawr: '3',
            ahassei: '1', denryu: "00010.00", fpac: "01");
        load.Data.AttachedParameter.LoadKind = "M ";
        load.Data.AttachedParameter.LoadCapacity = "0007500";

        AccumulationAreaSetter.SetTerminalCircuitCurrent([p, parent, load]);

        Assert.Equal(0.0, load.Work.AccumulationSlots[0].A);
    }

    // ── ループ3: 通電電流値の積算(Fyss37_I_Set_Sub 呼出) ────────────────────

    [Fact]
    public void 先頭機器で積算が失敗するとエラー通知される()
    {
        // 系統種別≠'1' → 下流抽出が null → IntegrateCurrent が false(C の RETCD_NG)。
        MainCircuitResult r = Row("001", kiryoso: '1', sentflg: '1', ksyubetu: '2');

        var errors = new List<string>();
        AccumulationAreaSetter.SetTerminalCircuitCurrent([r], reportError: errors.Add);

        Assert.Single(errors);
        Assert.Equal("Fyss36_MattanKairo_Iset()", errors[0]);
    }

    [Theory]
    [InlineData('1')]
    [InlineData('2')]
    public void 先頭機器でも切換タイプ1か2なら積算をスキップする(char kaetyp)
    {
        MainCircuitResult r = Row("001", kiryoso: '1', sentflg: '1', ksyubetu: '2', kaetyp: kaetyp);

        var errors = new List<string>();
        AccumulationAreaSetter.SetTerminalCircuitCurrent([r], reportError: errors.Add);

        Assert.Empty(errors);
    }

    [Fact]
    public void 先頭機器フラグが立たない機器は積算しない()
    {
        MainCircuitResult r = Row("001", kiryoso: '1', sentflg: ' ', ksyubetu: '2');

        var errors = new List<string>();
        AccumulationAreaSetter.SetTerminalCircuitCurrent([r], reportError: errors.Add);

        Assert.Empty(errors);
    }

    // ── ループ4: ＳＣ(系統)積算(Fyss3A デリゲート境界) ─────────────────────

    [Fact]
    public void 系統SCで負荷名0KWなら自身の通電電流値で積算する()
    {
        MainCircuitResult r = Row("001", fpaln1: "0KW", denryu: "00012.50");

        var calls = new List<(int Index, int Flag, double Current)>();
        AccumulationAreaSetter.SetTerminalCircuitCurrent(
            [r],
            checkSystemReservedWord: _ => (0, 2),
            accumulateSystemCurrent: (idx, flag, cur) => calls.Add((idx, flag, cur)));

        Assert.Single(calls);
        Assert.Equal((0, 2, 12.5), calls[0]);
    }

    [Fact]
    public void 系統SCで負荷名0KW以外は同一階層並列の直前末端で積算する()
    {
        MainCircuitResult prev = Row("001", kaisono: "005", heino: "002", mattan: '1', denryu: "00007.00");
        MainCircuitResult sc = Row("002", kaisono: "005", heino: "002", fpaln1: "P");

        var calls = new List<(int Index, int Flag, double Current)>();
        AccumulationAreaSetter.SetTerminalCircuitCurrent(
            [prev, sc],
            checkSystemReservedWord: i => i == 1 ? (0, 2) : (0, 0),
            accumulateSystemCurrent: (idx, flag, cur) => calls.Add((idx, flag, cur)));

        Assert.Single(calls);
        Assert.Equal((0, 2, 7.0), calls[0]);
    }

    [Fact]
    public void 系統SCで直前が同一階層並列でなければ積算しない()
    {
        MainCircuitResult prev = Row("001", kaisono: "005", heino: "002", mattan: '1', denryu: "00007.00");
        MainCircuitResult sc = Row("002", kaisono: "009", heino: "002", fpaln1: "P");

        var calls = new List<(int, int, double)>();
        AccumulationAreaSetter.SetTerminalCircuitCurrent(
            [prev, sc],
            checkSystemReservedWord: i => i == 1 ? (0, 2) : (0, 0),
            accumulateSystemCurrent: (idx, flag, cur) => calls.Add((idx, flag, cur)));

        Assert.Empty(calls);
    }

    [Fact]
    public void 系統チェックの戻り値が非0なら積算対象外()
    {
        MainCircuitResult r = Row("001", fpaln1: "0KW", denryu: "00012.50");

        var calls = new List<(int, int, double)>();
        AccumulationAreaSetter.SetTerminalCircuitCurrent(
            [r],
            checkSystemReservedWord: _ => (1, 2),
            accumulateSystemCurrent: (idx, flag, cur) => calls.Add((idx, flag, cur)));

        Assert.Empty(calls);
    }

    [Fact]
    public void 系統フラグが2以外なら積算対象外()
    {
        MainCircuitResult r = Row("001", fpaln1: "0KW", denryu: "00012.50");

        var calls = new List<(int, int, double)>();
        AccumulationAreaSetter.SetTerminalCircuitCurrent(
            [r],
            checkSystemReservedWord: _ => (0, 1),
            accumulateSystemCurrent: (idx, flag, cur) => calls.Add((idx, flag, cur)));

        Assert.Empty(calls);
    }

    [Fact]
    public void 先頭要素の負荷名0KW以外は直前参照ガードで積算しない()
    {
        // i==0 で 0KW 以外 → C の maina[-1] 参照を回避し積算しない。
        MainCircuitResult sc = Row("001", kaisono: "005", heino: "002", fpaln1: "P");

        var calls = new List<(int, int, double)>();
        AccumulationAreaSetter.SetTerminalCircuitCurrent(
            [sc],
            checkSystemReservedWord: _ => (0, 2),
            accumulateSystemCurrent: (idx, flag, cur) => calls.Add((idx, flag, cur)));

        Assert.Empty(calls);
    }

    [Fact]
    public void ＳＣデリゲートが未指定なら系統積算ループは実行されない()
    {
        MainCircuitResult r = Row("001", fpaln1: "0KW", denryu: "00012.50");

        // checkSystemReservedWord/accumulateSystemCurrent 未指定でも例外なく完了する。
        AccumulationAreaSetter.SetTerminalCircuitCurrent([r]);
    }
}
