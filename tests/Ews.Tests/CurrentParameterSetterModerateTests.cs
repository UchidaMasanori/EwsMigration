using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CurrentParameterSetter"/> の中位セッタ群
/// (【C原典】Fyss3G_Set_TB / Set_TR / Set_RRY / Set_AM, toku/sekkei/src/Fyss3G.c)の単体テスト。
/// TB は電線サイズ検索(CnsSQsetSeek)、TR は下流抽出(Fyss35_Select_Karyu_Sub)、
/// RRY は親遡行、AM は延長目盛りタイプ判定＋定格電流１検索(CnsA1setSeek)を伴う。
/// </summary>
public class CurrentParameterSetterModerateTests
{
    /// <summary>AT/A1/A2 の未設定を表す整形前ゼロ("000000000")。</summary>
    private static readonly string RawZero9 = new('0', 9);

    /// <summary>電線サイズ設定表(許容電流 31/40、選定フラグ 0)。key を跨ぐと 5.5→8.0 に切り替わる。</summary>
    private static readonly List<WireSizeSetting> WireSizes =
    [
        new WireSizeSetting(5.5, 31.0, 0),
        new WireSizeSetting(8.0, 40.0, 0),
    ];

    /// <summary>主回路データを 1 件生成する。denryu は "%08.2f"(8桁)形式。</summary>
    private static MainCircuitResult Rec(
        string sequenceNumber = "001",
        char leadingFlag = '1',
        string energizingCurrent = "00000.00",
        string parentSequenceNumber = "000",
        string hierarchyNumber = "000",
        char systemKind = '1',
        char loadSourceKind = ' ',
        char loadUnitKind = ' ',
        string loadCapacity = "0000000",
        string loadKind = " ",
        char circuitPhaseCount = '0')
    {
        var r = new MainCircuitResult { SequenceNumber = sequenceNumber };
        MainCircuitData d = r.Data;
        d.EnergizingCurrent = energizingCurrent;
        d.ParentSequenceNumber = parentSequenceNumber;
        d.HierarchyNumber = hierarchyNumber;
        d.SystemKind = systemKind;
        d.LoadSourceKind = loadSourceKind;
        d.CircuitPhaseCount = circuitPhaseCount;
        d.AttachedParameter.LoadUnitKind = loadUnitKind;
        d.AttachedParameter.LoadCapacity = loadCapacity;
        d.AttachedParameter.LoadKind = loadKind;
        r.Work.LeadingEquipmentFlag = leadingFlag;
        return r;
    }

    // ==== Set_TB ====

    [Fact]
    public void SetTb_A2に通電電流を設定しSQを検索する()
    {
        var row = Rec(energizingCurrent: "00025.00");
        row.Data.ElectricalParameterSlots[2].Sq = "000.00";
        var records = new List<MainCircuitResult> { row };

        // prm1=1 で ep[1] 処理を打ち切り、ep[2] だけを検証する。
        CurrentParameterSetter.SetTb(1, records, 0, WireSizes, 1);

        // A2 = 通電電流値、SQ = key(25*1.12=28)以上の最初の許容電流(31)の電線サイズ 5.5。
        Assert.Equal("00025.000", row.Data.ElectricalParameterSlots[2].A2);
        Assert.Equal("005.50", row.Data.ElectricalParameterSlots[2].Sq);
    }

    [Fact]
    public void SetTb_LGTは電線サイズを設定せず終了する()
    {
        var row = Rec(energizingCurrent: "00025.00");
        row.Data.ReservedWord = "LGT";
        row.Data.ElectricalParameterSlots[2].Sq = "000.00";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetTb(1, records, 0, WireSizes, 1);

        // A2 は設定されるが SQ は未設定("000.00")のまま。
        Assert.Equal("00025.000", row.Data.ElectricalParameterSlots[2].A2);
        Assert.Equal("000.00", row.Data.ElectricalParameterSlots[2].Sq);
    }

    [Fact]
    public void SetTb_改訂9_動力三相の負荷容量帯でdenryuを補正する()
    {
        // fpalwkbn='W' かつ kpaph='3'、fpalw2=6000(5500<x<=11000)→ denryu=30.1。
        var row = Rec(
            energizingCurrent: "00010.00",
            loadUnitKind: 'W',
            loadCapacity: "0006000",
            circuitPhaseCount: '3');
        row.Data.ElectricalParameterSlots[2].Sq = "000.00";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetTb(1, records, 0, WireSizes, 1);

        // key=30.1*1.12=33.7 → 許容電流 31 は不足、40 が該当 → 電線サイズ 8.0。
        Assert.Equal("00010.000", row.Data.ElectricalParameterSlots[2].A2);
        Assert.Equal("008.00", row.Data.ElectricalParameterSlots[2].Sq);
    }

    [Fact]
    public void SetTb_改訂7_26台のdenryuは30_1に補正される()
    {
        // denryu=26.8(26.669-26.876)→ 30.1 に補正。
        var row = Rec(energizingCurrent: "00026.80");
        row.Data.ElectricalParameterSlots[2].Sq = "000.00";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetTb(1, records, 0, WireSizes, 1);

        // 補正なしなら key=30.0→5.5 だが、補正後 key=33.7→8.0 になる。
        Assert.Equal("008.00", row.Data.ElectricalParameterSlots[2].Sq);
    }

    [Fact]
    public void SetTb_ahassei1でep2のSQにep1のSQを複写する()
    {
        var row = Rec(energizingCurrent: "00025.00", loadSourceKind: '1');
        // ep[2] は非未設定にして ep[2] の検索をスキップさせ、複写を観測する。
        row.Data.ElectricalParameterSlots[2].Sq = "999.99";
        row.Data.ElectricalParameterSlots[1].Sq = "000.00";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetTb(0, records, 0, WireSizes, 1);

        // ep[1].SQ=5.5、ahassei='1' で ep[2].SQ ← ep[1].SQ。
        Assert.Equal("005.50", row.Data.ElectricalParameterSlots[1].Sq);
        Assert.Equal("005.50", row.Data.ElectricalParameterSlots[2].Sq);
    }

    [Fact]
    public void SetTb_prm1が0以外はep1処理をしない()
    {
        var row = Rec(energizingCurrent: "00025.00", loadSourceKind: '1');
        row.Data.ElectricalParameterSlots[1].Sq = "000.00";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetTb(1, records, 0, WireSizes, 1);

        // prm1!=0 で ep[1].SQ は未設定のまま。
        Assert.Equal("000.00", row.Data.ElectricalParameterSlots[1].Sq);
    }

    // ==== Set_TR ====

    [Fact]
    public void SetTr_タイプ0未設定でVA500以下はROを設定する()
    {
        var row = Rec();
        // ep[0].VA を整形ゼロにして ep[2].VA(=300)を読ませる。
        row.Data.ElectricalParameterSlots[0].Va = "0000000.00";
        row.Data.ElectricalParameterSlots[2].Va = "0000300.00";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetTr(records, 0, 1);

        Assert.Equal("RO     ", row.Data.DataType[0]);
    }

    [Fact]
    public void SetTr_タイプ0_ep0非ゼロならep1のVAを読む原典バグ()
    {
        var row = Rec();
        // ep[0]=999(>500) だが原典は ep[1]=400(<=500)を読むため RO になる。
        row.Data.ElectricalParameterSlots[0].Va = "0000999.00";
        row.Data.ElectricalParameterSlots[1].Va = "0000400.00";
        // ep[2]=600(>500)。誤って ep[2] を読めば RO にならない。
        row.Data.ElectricalParameterSlots[2].Va = "0000600.00";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetTr(records, 0, 1);

        Assert.Equal("RO     ", row.Data.DataType[0]);
    }

    [Fact]
    public void SetTr_VA未設定なら下流のM負荷を積算してVAを算出する()
    {
        // 自身の fpalw2=1000 を M 種の下流ごとに積算する(原典どおり自身を参照)。
        var self = Rec(sequenceNumber: "001", loadCapacity: "0001000");
        self.Data.DataType[0] = "TR     ";
        self.Data.ElectricalParameterSlots[2].Va = "0000000.00";
        var child1 = Rec(sequenceNumber: "002", parentSequenceNumber: "001", loadSourceKind: '1', loadKind: "M ");
        var child2 = Rec(sequenceNumber: "003", parentSequenceNumber: "001", loadSourceKind: '1', loadKind: "M ");
        var records = new List<MainCircuitResult> { self, child1, child2 };

        CurrentParameterSetter.SetTr(records, 0, 1);

        // lw2 = 1000 + 1000 = 2000、VA = 2000 * 1.5 = 3000。
        Assert.Equal("   3000.00", self.Data.ElectricalParameterSlots[2].Va);
    }

    [Fact]
    public void SetTr_下流にM以外の負荷発生があれば積算を打ち切る()
    {
        var self = Rec(sequenceNumber: "001", loadCapacity: "0001000");
        self.Data.DataType[0] = "TR     ";
        self.Data.ElectricalParameterSlots[2].Va = "0000000.00";
        var child1 = Rec(sequenceNumber: "002", parentSequenceNumber: "001", loadSourceKind: '1', loadKind: "M ");
        var child2 = Rec(sequenceNumber: "003", parentSequenceNumber: "001", loadSourceKind: '1', loadKind: "H ");
        var records = new List<MainCircuitResult> { self, child1, child2 };

        CurrentParameterSetter.SetTr(records, 0, 1);

        // child2(H)で打ち切り → lw2 = 1000、VA = 1500。
        Assert.Equal("   1500.00", self.Data.ElectricalParameterSlots[2].Va);
    }

    [Fact]
    public void SetTr_下流なしはVAゼロで上書きされる()
    {
        var self = Rec(sequenceNumber: "001", loadCapacity: "0001000");
        self.Data.DataType[0] = "TR     ";
        self.Data.ElectricalParameterSlots[2].Va = "0000000.00";
        var records = new List<MainCircuitResult> { self };

        CurrentParameterSetter.SetTr(records, 0, 1);

        // lw2=0 → VA=0.0 が "%10.2f" で整形される。
        Assert.Equal("      0.00", self.Data.ElectricalParameterSlots[2].Va);
    }

    // ==== Set_RRY ====

    [Fact]
    public void SetRry_LACSLは16Aを設定して終了する()
    {
        var row = Rec();
        row.Data.DataType[1] = "LA";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetRry(records, 0, 1);

        Assert.Equal("00016.000", row.Data.ElectricalParameterSlots[1].A2);
        // ep[2].A2 は変化しない。
        Assert.Equal(RawZero9, row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetRry_親を遡り同一階層のATをA2に採る()
    {
        var parent = Rec(sequenceNumber: "001", parentSequenceNumber: "000", hierarchyNumber: "002");
        parent.Data.ElectricalParameterSlots[0].At = "00050.000";
        var self = Rec(sequenceNumber: "002", parentSequenceNumber: "001", hierarchyNumber: "002");
        var records = new List<MainCircuitResult> { parent, self };

        CurrentParameterSetter.SetRry(records, 1, 1);

        Assert.Equal("00050.000", self.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetRry_親のATが未設定なら通電電流をA2に設定する()
    {
        var parent = Rec(sequenceNumber: "001", parentSequenceNumber: "000", hierarchyNumber: "002");
        parent.Data.ElectricalParameterSlots[0].At = "00000.000";
        var self = Rec(sequenceNumber: "002", parentSequenceNumber: "001", hierarchyNumber: "002", energizingCurrent: "00033.00");
        self.Data.ElectricalParameterSlots[2].A2 = "00000.000";
        var records = new List<MainCircuitResult> { parent, self };

        CurrentParameterSetter.SetRry(records, 1, 1);

        Assert.Equal("00033.000", self.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetRry_階層不一致の親はスキップして通電電流を採る()
    {
        var parent = Rec(sequenceNumber: "001", parentSequenceNumber: "000", hierarchyNumber: "005");
        parent.Data.ElectricalParameterSlots[0].At = "00050.000";
        var self = Rec(sequenceNumber: "002", parentSequenceNumber: "001", hierarchyNumber: "002", energizingCurrent: "00033.00");
        self.Data.ElectricalParameterSlots[2].A2 = "00000.000";
        var records = new List<MainCircuitResult> { parent, self };

        CurrentParameterSetter.SetRry(records, 1, 1);

        Assert.Equal("00033.000", self.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetRry_先頭機器フラグ不一致なら何もしない()
    {
        var row = Rec(leadingFlag: ' ', energizingCurrent: "00033.00");
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetRry(records, 0, 1);

        Assert.Equal(RawZero9, row.Data.ElectricalParameterSlots[1].A2);
        Assert.Equal(RawZero9, row.Data.ElectricalParameterSlots[2].A2);
    }

    // ==== Set_AM ====

    /// <summary>定格電流１設定表(10/20/30A)。SeekRatedCurrent1 は key より大きい最初の値を返す。</summary>
    private static readonly List<RatedCurrent1Setting> RatedCurrents =
    [
        new RatedCurrent1Setting(10.0),
        new RatedCurrent1Setting(20.0),
        new RatedCurrent1Setting(30.0),
    ];

    [Fact]
    public void SetAm_タイプ0が未設定なら通常目盛りNBKを設定する()
    {
        var row = Rec(energizingCurrent: "00050.00");
        row.Data.CircuitElement = '1';
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetAm(1, records, 0, 1, RatedCurrents, 1, 1, "");

        Assert.Equal("NBK    ", row.Data.DataType[0]);
    }

    [Fact]
    public void SetAm_BOMSかつ電動機なら3倍目盛り3BKを設定する()
    {
        var row = Rec(energizingCurrent: "00050.00", loadKind: "M ");
        row.Data.LineTypeCode = "B  ";
        row.Data.LineTypeGroupNumber = "001";
        row.Data.CircuitElement = '1';
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetAm(1, records, 0, 1, RatedCurrents, 1, 1, "");

        Assert.Equal("3BK    ", row.Data.DataType[0]);
    }

    [Fact]
    public void SetAm_タイプ6が未設定でAC区分ならACを設定する()
    {
        var row = Rec(energizingCurrent: "00050.00");
        row.Data.CircuitElement = '1';
        row.Data.CircuitVoltageKind = 'A';
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetAm(1, records, 0, 1, RatedCurrents, 1, 1, "");

        Assert.Equal("AC     ", row.Data.DataType[6]);
    }

    [Fact]
    public void SetAm_タイプ6が未設定でDC区分ならDCを設定する()
    {
        var row = Rec(energizingCurrent: "00050.00");
        row.Data.CircuitElement = '1';
        row.Data.CircuitVoltageKind = 'D';
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetAm(1, records, 0, 1, RatedCurrents, 1, 1, "");

        Assert.Equal("DC     ", row.Data.DataType[6]);
    }

    [Fact]
    public void SetAm_主回路はep2A1を初期化しA2に1_2倍値を設定する()
    {
        var row = Rec(energizingCurrent: "00050.00");
        row.Data.CircuitElement = '1';
        var records = new List<MainCircuitResult> { row };

        // denryu=50 -> a1=60.0(境界補正なし)。
        CurrentParameterSetter.SetAm(1, records, 0, 1, RatedCurrents, 1, 1, "");

        Assert.Equal("00000.000", row.Data.ElectricalParameterSlots[2].A1);
        Assert.Equal("00060.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetAm_河村標準の境界で強制値に補正する()
    {
        var row = Rec(energizingCurrent: "00008.00");
        row.Data.CircuitElement = '1';
        var records = new List<MainCircuitResult> { row };

        // denryu=8 -> a1=9.6 は (7.7520,10.00) に入り 10.01 へ補正。
        CurrentParameterSetter.SetAm(1, records, 0, 1, RatedCurrents, 1, 1, "");

        Assert.Equal("00010.010", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetAm_公共建築仕様の境界で強制値に補正する()
    {
        var row = Rec(energizingCurrent: "00008.50");
        row.Data.CircuitElement = '1';
        var records = new List<MainCircuitResult> { row };

        // denryu=8.5 -> a1=10.2 は (10.000,11.148) に入り 10.00 へ補正(seisakusiyou=0)。
        CurrentParameterSetter.SetAm(1, records, 0, 1, RatedCurrents, 1, 0, "");

        Assert.Equal("00010.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetAm_計器用回路CT付きはA1をA1SET検索しA2を5固定する()
    {
        var row = Rec(energizingCurrent: "00015.00");
        row.Data.CircuitElement = '2';
        var records = new List<MainCircuitResult> { row };

        // denryu=15 -> 1.2倍=18 -> A1SET 検索で 18 より大きい最初の 20。
        CurrentParameterSetter.SetAm(1, records, 0, 1, RatedCurrents, 1, 1, "");

        Assert.Equal("00020.000", row.Data.ElectricalParameterSlots[2].A1);
        Assert.Equal("00005.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void SetAm_prm1が1ならep1処理へ進まず負荷発生複写もしない()
    {
        var row = Rec(energizingCurrent: "00050.00", loadSourceKind: '1');
        row.Data.CircuitElement = '1';
        row.Data.ElectricalParameterSlots[1].A1 = "00012.340";
        var records = new List<MainCircuitResult> { row };

        // prm1=1: ep[2].A1 は主回路初期化のまま、ep[1].A1 の複写は行われない。
        CurrentParameterSetter.SetAm(1, records, 0, 1, RatedCurrents, 1, 1, "");

        Assert.Equal("00000.000", row.Data.ElectricalParameterSlots[2].A1);
    }

    [Fact]
    public void SetAm_prm1が0かつ負荷発生ならep2A1にep1A1を複写する()
    {
        var row = Rec(energizingCurrent: "00050.00", loadSourceKind: '1');
        row.Data.CircuitElement = '1';
        row.Data.ElectricalParameterSlots[1].A1 = "00012.340";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetAm(0, records, 0, 1, RatedCurrents, 1, 1, "");

        Assert.Equal("00012.340", row.Data.ElectricalParameterSlots[2].A1);
    }

    [Fact]
    public void SetAm_CT付きでep1A1が非ゼロなら定格電流を再検索する()
    {
        var row = Rec(energizingCurrent: "00015.00");
        row.Data.CircuitElement = '2';
        row.Data.ElectricalParameterSlots[1].A1 = "00010.000";
        var records = new List<MainCircuitResult> { row };

        // ep1 側は denryu(1.2倍なし)=15 で再検索 -> 20。
        CurrentParameterSetter.SetAm(0, records, 0, 1, RatedCurrents, 1, 1, "");

        Assert.Equal("00020.000", row.Data.ElectricalParameterSlots[1].A1);
    }

    [Fact]
    public void SetAm_改訂5_ヒータで同系統に三相があれば3BKを設定する()
    {
        var row = Rec(energizingCurrent: "00050.00", loadKind: "H ", circuitPhaseCount: '3');
        row.Data.CircuitElement = '1';
        row.Data.SystemNumber = "005";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetAm(1, records, 0, 1, RatedCurrents, 1, 1, "");

        Assert.Equal("3BK    ", row.Data.DataType[0]);
    }

    [Fact]
    public void SetAm_改訂11_特定ゾーンでタイプ2がAS以外なら3BKを設定する()
    {
        var row = Rec(energizingCurrent: "00050.00");
        row.Data.CircuitElement = '1';
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetAm(1, records, 0, 1, RatedCurrents, 1, 1, "78007");

        Assert.Equal("3BK    ", row.Data.DataType[0]);
    }

    [Fact]
    public void SetAm_改訂11_特定ゾーンでもタイプ2がASなら通常目盛りのまま()
    {
        var row = Rec(energizingCurrent: "00050.00");
        row.Data.CircuitElement = '1';
        row.Data.DataType[2] = "AS     ";
        var records = new List<MainCircuitResult> { row };

        CurrentParameterSetter.SetAm(1, records, 0, 1, RatedCurrents, 1, 1, "78007");

        Assert.Equal("NBK    ", row.Data.DataType[0]);
    }
}

