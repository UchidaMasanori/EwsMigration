using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CurrentParameterDispatcher"/>(【C原典】Fyss3G_Denryuu_Parm_Set,
/// toku/sekkei/src/Fyss3G.c)の単体テスト。
/// レコードのフィルタ(系統種別/回路要素 kubun/負荷容量決定テーブル)と、
/// パラメータ設定タイプ(prm_tp)から個別セッタへの振り分けを検証する。
/// </summary>
public class CurrentParameterDispatcherTests
{
    private static readonly List<WireSizeSetting> NoWire = [];
    private static readonly List<RatedCurrent2Setting> NoRated2 = [];
    private static readonly List<RatedCurrent1Setting> NoRated1 = [];

    /// <summary>ElectricalParameters 既定の AT/AF/A1/A2("000000000")。</summary>
    private const string RawZero9 = "000000000";

    /// <summary>主回路データを 1 件生成する。denryu は "%08.2f"(8桁)形式。</summary>
    private static MainCircuitResult Rec(
        string reservedWord,
        string energizingCurrent = "00025.00",
        char systemKind = '1',
        char circuitElement = '1',
        char leadingFlag = '1')
    {
        var r = new MainCircuitResult { SequenceNumber = "001" };
        MainCircuitData d = r.Data;
        d.ReservedWord = reservedWord;
        d.EnergizingCurrent = energizingCurrent;
        d.SystemKind = systemKind;
        d.CircuitElement = circuitElement;
        r.Work.LeadingEquipmentFlag = leadingFlag;
        return r;
    }

    /// <summary>予約語 → パラメータ設定タイプ(prm_tp)の 1 件テーブル。</summary>
    private static List<ParameterSettingType> Table(string reservedWord, int settingType)
        => [new ParameterSettingType(reservedWord, 0, settingType, new int[10])];

    /// <summary>ep[0] を全て整形ゼロにして Check_fyrt800 の prm1=1(設定不要)を成立させる。</summary>
    private static void MakeInputUnset(MainCircuitResult row)
    {
        ElectricalParameters ep0 = row.Data.ElectricalParameterSlots[0];
        ep0.At = "00000.000";
        ep0.Af = "00000.000";
        ep0.A1 = "00000.000";
        ep0.A2 = "00000.000";
        ep0.W1 = "0000000.00";
    }

    private static void Dispatch(
        List<MainCircuitResult> records,
        List<ParameterSettingType> parameterSettingTable,
        char kubun = 'M',
        string manufacturingSpecKind = "",
        int inputFlag = 1)
        => CurrentParameterDispatcher.DispatchCurrentParameters(
            manufacturingSpecKind,
            records.Count,
            records,
            inputFlag,
            kubun,
            parameterSettingTable,
            NoWire,
            NoRated2,
            NoRated1,
            string.Empty);

    // ==== 振り分け(正常系) ====

    [Fact]
    public void Dispatch_MCB予約語はSetMcbへ振り分けep2ATを設定する()
    {
        var row = Rec("MCB");
        var records = new List<MainCircuitResult> { row };

        Dispatch(records, Table("MCB", 1));

        // SetMcb が ep[2].AT/AF に通電電流値を設定する。
        Assert.Equal("00025.000", row.Data.ElectricalParameterSlots[2].At);
        Assert.Equal("00025.000", row.Data.ElectricalParameterSlots[2].Af);
    }

    [Fact]
    public void Dispatch_TS予約語はSetTsでA2を15Aに設定する()
    {
        var row = Rec("TS");
        var records = new List<MainCircuitResult> { row };

        Dispatch(records, Table("TS", 38));

        Assert.Equal("00015.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Theory]
    [InlineData("TSU", 77)]
    [InlineData("SSWU", 78)]
    public void Dispatch_TSU系もSetTsへ振り分ける(string reservedWord, int settingType)
    {
        var row = Rec(reservedWord);
        var records = new List<MainCircuitResult> { row };

        Dispatch(records, Table(reservedWord, settingType));

        Assert.Equal("00015.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Theory]
    [InlineData("PBSU", 79)]
    [InlineData("COSU", 80)]
    [InlineData("2COSU", 81)]
    [InlineData("OLU", 82)]
    public void Dispatch_SU系はSetSuでA2を1_5Aに設定する(string reservedWord, int settingType)
    {
        var row = Rec(reservedWord);
        var records = new List<MainCircuitResult> { row };

        Dispatch(records, Table(reservedWord, settingType));

        Assert.Equal("00001.500", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Theory]
    [InlineData("CON", 23)]
    [InlineData("ZCT", 25)]
    public void Dispatch_CONとZCTはSetConでA2に通電電流を設定する(string reservedWord, int settingType)
    {
        var row = Rec(reservedWord);
        var records = new List<MainCircuitResult> { row };

        Dispatch(records, Table(reservedWord, settingType));

        Assert.Equal("00025.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void Dispatch_SSWはSetSswでA2に通電電流を設定する()
    {
        var row = Rec("SSW");
        var records = new List<MainCircuitResult> { row };

        Dispatch(records, Table("SSW", 43));

        Assert.Equal("00025.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    // ==== フィルタ ====

    [Fact]
    public void Dispatch_系統種別が1以外はスキップする()
    {
        var row = Rec("MCB", systemKind: '2');
        var records = new List<MainCircuitResult> { row };

        Dispatch(records, Table("MCB", 1));

        // 未処理のため ep[2].AT は既定のまま。
        Assert.Equal(RawZero9, row.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void Dispatch_kubunMは回路要素1以外をスキップする()
    {
        var row = Rec("MCB", circuitElement: '2');
        var records = new List<MainCircuitResult> { row };

        Dispatch(records, Table("MCB", 1), kubun: 'M');

        Assert.Equal(RawZero9, row.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void Dispatch_kubunM以外は回路要素1をスキップする()
    {
        var row = Rec("MCB", circuitElement: '1');
        var records = new List<MainCircuitResult> { row };

        // kubun='K'(計器回路)は回路要素 '1'(主回路)を処理しない。
        Dispatch(records, Table("MCB", 1), kubun: 'K');

        Assert.Equal(RawZero9, row.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void Dispatch_kubunM以外は回路要素1以外を処理する()
    {
        var row = Rec("SSW", circuitElement: '2');
        var records = new List<MainCircuitResult> { row };

        Dispatch(records, Table("SSW", 43), kubun: 'K');

        // 計器回路として処理され A2 が設定される。
        Assert.Equal("00025.000", row.Data.ElectricalParameterSlots[2].A2);
    }

    // ==== C 原典の忠実再現 ====

    [Fact]
    public void Dispatch_MGはSetMgでep2ATを設定する()
    {
        var row = Rec("MG");
        var records = new List<MainCircuitResult> { row };

        Dispatch(records, Table("MG", 12));

        Assert.Equal("00025.000", row.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void Dispatch_MGFRはMG系caseに含まれず既定でno_op()
    {
        // 【C原典】switch の MG 系 case は MG/MGSD/MGFRSD のみ。MGFR(72)は含まれない。
        var row = Rec("MGFR");
        var records = new List<MainCircuitResult> { row };

        Dispatch(records, Table("MGFR", 72));

        // 既定(no-op)のため ep[2].AT は変化しない。
        Assert.Equal(RawZero9, row.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void Dispatch_パラメータ設定タイプ未該当はno_op()
    {
        var row = Rec("MCB");
        var records = new List<MainCircuitResult> { row };

        // 空テーブル → SeekParameterSettingType が null → prm_tp=0 → 既定 no-op。
        Dispatch(records, []);

        Assert.Equal(RawZero9, row.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void Dispatch_DCPWは空関数で何も変更しない()
    {
        // 【C原典】Fyss3G_Set_DCPW は空関数。
        var row = Rec("DCPW");
        var records = new List<MainCircuitResult> { row };

        Dispatch(records, Table("DCPW", 42));

        Assert.Equal(RawZero9, row.Data.ElectricalParameterSlots[2].At);
        Assert.Equal(RawZero9, row.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void Dispatch_CKSはprm1の値をSetCksへ渡す()
    {
        // 【C原典】Fyss3G_Set_CKS( prm1, ... )。定義側引数名は prm2 だが実引数は prm1。
        // ep[0] を全て未入力にすると Check_fyrt800 は prm1=1 を返し、CKS は A2 を設定しない。
        var setRow = Rec("CKS");
        var unsetRow = Rec("CKS");
        MakeInputUnset(unsetRow);

        var setRecords = new List<MainCircuitResult> { setRow };
        var unsetRecords = new List<MainCircuitResult> { unsetRow };

        Dispatch(setRecords, Table("CKS", 49));
        Dispatch(unsetRecords, Table("CKS", 49));

        // prm1=0(入力あり扱い)→ 設定される。
        Assert.Equal("00025.000", setRow.Data.ElectricalParameterSlots[2].A2);
        // prm1=1(未入力)→ 設定されない(prm1 の値が渡っている証拠)。
        Assert.Equal(RawZero9, unsetRow.Data.ElectricalParameterSlots[2].A2);
    }

    // ==== 複数レコード ====

    [Fact]
    public void Dispatch_複数レコードをそれぞれ振り分ける()
    {
        var mcb = Rec("MCB");
        var skipped = Rec("MCB", systemKind: '2');
        var ts = Rec("TS");
        var records = new List<MainCircuitResult> { mcb, skipped, ts };

        var table = new List<ParameterSettingType>
        {
            new("MCB", 0, 1, new int[10]),
            new("TS", 0, 38, new int[10]),
        };

        CurrentParameterDispatcher.DispatchCurrentParameters(
            string.Empty, records.Count, records, 1, 'M', table, NoWire, NoRated2, NoRated1, string.Empty);

        Assert.Equal("00025.000", mcb.Data.ElectricalParameterSlots[2].At);
        Assert.Equal(RawZero9, skipped.Data.ElectricalParameterSlots[2].At);
        Assert.Equal("00015.000", ts.Data.ElectricalParameterSlots[2].A2);
    }
}
