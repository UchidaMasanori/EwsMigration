using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="PlugInBreakerConnector"/>(【C原典】Fyss3R.c のプラグインブレーカ結線処理)の単体テスト。
/// プラグインタイプ照合(FyHcPlugInJdgType)と電源・分岐グルーピング(PropGrouping)を検証する。
/// </summary>
public class PlugInBreakerConnectorTests
{
    /// <summary>主回路レコードを 1 件生成する。</summary>
    private static MainCircuitResult Rec(
        string reservedWord = "",
        string dataType0 = "",
        char phase = '1',
        char wire = '3')
    {
        var r = new MainCircuitResult { SequenceNumber = "001" };
        r.Data.ReservedWord = reservedWord;
        r.Data.DataType[0] = dataType0;
        r.Data.CircuitPhaseCount = phase;
        r.Data.CircuitWireType = wire;
        return r;
    }

    // ---- IsPlugInType ----

    [Theory]
    [InlineData("CTP")]
    [InlineData("CH")]
    [InlineData("CHP")]
    [InlineData("KP")]
    [InlineData("CH     ")]  // 末尾空白は除去して照合
    public void 有効なプラグインタイプは真(string type0)
    {
        Assert.True(PlugInBreakerConnector.IsPlugInType([type0, "", "", "", "", "", ""]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("CV")]   // 改訂<2>で無効化
    [InlineData("FL")]   // 改訂<2>で無効化
    [InlineData("CSP")]  // 改訂<2>で無効化
    [InlineData("MCB")]
    [InlineData(" CH")]  // 先頭空白は除去しないため不一致
    public void 無効なタイプは偽(string type0)
    {
        Assert.False(PlugInBreakerConnector.IsPlugInType([type0, "", "", "", "", "", ""]));
    }

    [Fact]
    public void 空配列は偽()
    {
        Assert.False(PlugInBreakerConnector.IsPlugInType([]));
    }

    // ---- GroupBySource ----

    [Fact]
    public void 電源後の連続同一タイプは1グループ()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(reservedWord: "P", phase: '1', wire: '3'),  // 電源 単相3線=13
            Rec(dataType0: "CH"),
            Rec(dataType0: "CHP"),  // 先頭 'C' で同一タイプ
        };

        (IReadOnlyList<PlugInGroup> groups, int count) = PlugInBreakerConnector.GroupBySource(records);

        Assert.Equal(1, count);
        Assert.Equal(13, groups[0].SourcePhaseWire);
        Assert.Equal('C', groups[0].Type);
        Assert.Equal(1, groups[0].StartIndex);
        Assert.Equal(2, groups[0].EndIndex);
    }

    [Fact]
    public void タイプが変われば新グループ()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(reservedWord: "P", phase: '3', wire: '3'),  // 三相3線=33
            Rec(dataType0: "CH"),   // 'C'
            Rec(dataType0: "KP"),   // 'K' → 新グループ
        };

        (IReadOnlyList<PlugInGroup> groups, int count) = PlugInBreakerConnector.GroupBySource(records);

        Assert.Equal(2, count);
        Assert.Equal('C', groups[0].Type);
        Assert.Equal(1, groups[0].StartIndex);
        Assert.Equal(1, groups[0].EndIndex);
        Assert.Equal('K', groups[1].Type);
        Assert.Equal(2, groups[1].StartIndex);
        Assert.Equal(2, groups[1].EndIndex);
        Assert.Equal(33, groups[1].SourcePhaseWire);
    }

    [Fact]
    public void プラグインを含まないグループは境界を進めない_改訂2()
    {
        // 電源1(プラグインあり) → 電源2(プラグインなし) → 電源3(プラグインあり)。
        // 改訂<2>: プラグインを含む電源1・電源3のみがグループ化され count=2。
        var records = new List<MainCircuitResult>
        {
            Rec(reservedWord: "P", phase: '1', wire: '3'),
            Rec(dataType0: "CH"),
            Rec(reservedWord: "P", phase: '1', wire: '3'),
            Rec(dataType0: "MCB"),  // プラグインでない
            Rec(reservedWord: "P", phase: '3', wire: '3'),
            Rec(dataType0: "KP"),
        };

        (IReadOnlyList<PlugInGroup> groups, int count) = PlugInBreakerConnector.GroupBySource(records);

        Assert.Equal(2, count);
        Assert.Equal('C', groups[0].Type);
        Assert.Equal(1, groups[0].StartIndex);
        Assert.Equal(13, groups[0].SourcePhaseWire);
        Assert.Equal('K', groups[1].Type);
        Assert.Equal(5, groups[1].StartIndex);
        Assert.Equal(33, groups[1].SourcePhaseWire);
    }

    [Fact]
    public void 電源が先行しないプラグインはスキップされる()
    {
        // "P " が先行しない場合、C の grp[-1] 書込(未定義動作)を回避しスキップ。
        var records = new List<MainCircuitResult>
        {
            Rec(dataType0: "CH"),
            Rec(dataType0: "KP"),
        };

        (IReadOnlyList<PlugInGroup> _, int count) = PlugInBreakerConnector.GroupBySource(records);

        Assert.Equal(0, count);
    }

    [Fact]
    public void 予約語PBは電源とみなさない()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(reservedWord: "PB"),  // "P " ではない
            Rec(dataType0: "CH"),
        };

        (IReadOnlyList<PlugInGroup> _, int count) = PlugInBreakerConnector.GroupBySource(records);

        Assert.Equal(0, count);
    }

    [Fact]
    public void 空入力はグループ0件()
    {
        (IReadOnlyList<PlugInGroup> _, int count) = PlugInBreakerConnector.GroupBySource([]);

        Assert.Equal(0, count);
    }

    // ---- SetConnection (PropSetSouFor1sou/3sou/Kes_Set) ----

    /// <summary>プラグインブレーカ 1 件を生成する(結線処理テスト用)。</summary>
    private static MainCircuitResult Plug(
        string dataType0 = "CH",
        string dataType1 = "",
        string dataType2 = "",
        string dataType3 = "",
        string kpav0 = "000",
        string epap = "000")
    {
        var r = new MainCircuitResult { SequenceNumber = "002" };
        r.Data.DataType[0] = dataType0;
        r.Data.DataType[1] = dataType1;
        r.Data.DataType[2] = dataType2;
        r.Data.DataType[3] = dataType3;
        r.Data.CircuitVoltage[0] = kpav0;
        r.Data.ElectricalParameterSlots[0].P = epap;
        return r;
    }

    [Fact]
    public void 単相_NOTHING指定無_接続相タイプ未入力はXN_YNを交互()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(reservedWord: "P", phase: '1', wire: '3'),           // 電源 単相3線=13
            Plug(dataType3: "NOTHING", kpav0: "105"),
            Plug(dataType3: "NOTHING", kpav0: "105"),
        };

        PlugInBreakerConnector.SetConnection(records, _ => false); // nothing=2

        Assert.Equal("XN  ", records[1].Data.UsedPhase);
        Assert.Equal("RN     ", records[1].Data.DataType[3]);
        Assert.Equal("YN  ", records[2].Data.UsedPhase);
        Assert.Equal("TN     ", records[2].Data.DataType[3]);
    }

    [Fact]
    public void 単相_CV付きでNOTHING指定有はRT相結線210_XY()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(reservedWord: "P", phase: '1', wire: '3'),
            Plug(dataType1: "CV ", dataType3: "NOTHING", kpav0: "105"),
        };

        PlugInBreakerConnector.SetConnection(records, _ => true); // nothing=1

        Assert.Equal("210", records[1].Data.CircuitVoltage[0]);
        Assert.Equal("XY  ", records[1].Data.UsedPhase);
    }

    [Fact]
    public void 単相_接続相タイプ入力済RNはXN_TNはYN()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(reservedWord: "P", phase: '1', wire: '3'),
            Plug(dataType3: "RN ", kpav0: "105"),
            Plug(dataType3: "TN ", kpav0: "105"),
        };

        PlugInBreakerConnector.SetConnection(records, _ => false);

        Assert.Equal("XN  ", records[1].Data.UsedPhase);
        Assert.Equal("YN  ", records[2].Data.UsedPhase);
    }

    [Fact]
    public void 三相_NOTHING指定無は順にRS_ST_RTと機器タイプをセット()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(reservedWord: "P", phase: '3', wire: '3'),          // 三相3線=33
            Plug(dataType3: "NOTHING", kpav0: "210"),
            Plug(dataType3: "NOTHING", kpav0: "210"),
            Plug(dataType3: "NOTHING", kpav0: "210"),
        };

        PlugInBreakerConnector.SetConnection(records, _ => false); // nothing=2

        Assert.Equal("RS  ", records[1].Data.UsedPhase);
        Assert.Equal("RN     ", records[1].Data.DataType[3]);
        Assert.Equal("ST  ", records[2].Data.UsedPhase);
        Assert.Equal("TN     ", records[2].Data.DataType[3]);
        Assert.Equal("RT  ", records[3].Data.UsedPhase);
        Assert.Equal("NOTHING", records[3].Data.DataType[3]);
    }

    [Fact]
    public void 三相_アラームなしCHPタイプはRT()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(reservedWord: "P", phase: '3', wire: '3'),
            Plug(dataType0: "CHP ", dataType2: "NOTHING", dataType3: "NOTHING", kpav0: "210"),
        };

        PlugInBreakerConnector.SetConnection(records, _ => false); // nothing=2

        Assert.Equal("RT  ", records[1].Data.UsedPhase);
    }

    [Fact]
    public void 三相_接続相タイプRNはRS_TNはST()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(reservedWord: "P", phase: '3', wire: '3'),
            Plug(dataType3: "RN ", kpav0: "210"),
            Plug(dataType3: "TN ", kpav0: "210"),
        };

        PlugInBreakerConnector.SetConnection(records, _ => false);

        Assert.Equal("RS  ", records[1].Data.UsedPhase);
        Assert.Equal("ST  ", records[2].Data.UsedPhase);
    }

    [Fact]
    public void 三相_kpav210でないか極数003は処理しない()
    {
        var records = new List<MainCircuitResult>
        {
            Rec(reservedWord: "P", phase: '3', wire: '3'),
            Plug(dataType3: "NOTHING", kpav0: "105"),               // kpav!=210 → スキップ
            Plug(dataType3: "NOTHING", kpav0: "210", epap: "003"),  // epap==003 → スキップ
        };

        PlugInBreakerConnector.SetConnection(records, _ => false);

        Assert.Equal(string.Empty, records[1].Data.UsedPhase);
        Assert.Equal(string.Empty, records[2].Data.UsedPhase);
    }

    [Fact]
    public void SetConnectionのrecordsがnullなら例外()
    {
        Assert.Throws<System.ArgumentNullException>(
            () => PlugInBreakerConnector.SetConnection(null!, _ => false));
    }

    [Fact]
    public void SetConnectionのデリゲートがnullなら例外()
    {
        Assert.Throws<System.ArgumentNullException>(
            () => PlugInBreakerConnector.SetConnection([], null!));
    }

    // ---- CheckMainBreaker (Fyss3R_TokuPlugIn_MainChk) ----

    /// <summary>プラグイン子機器 1 件を生成する(主幹チェックテスト用)。</summary>
    private static MainCircuitResult Child(string parent = "010", string dataType0 = "CH")
    {
        var r = new MainCircuitResult { SequenceNumber = "001" };
        r.Data.DataType[0] = dataType0;
        r.Data.ParentSequenceNumber = parent;
        return r;
    }

    /// <summary>親機器(主幹)1 件を生成する(主幹チェックテスト用)。</summary>
    private static MainCircuitResult ParentRec(
        string sequence = "010",
        char phase = '3',
        string epaat = "00000.000",
        string epap = "003",
        string reservedWord = "MCB",
        string makerCode = "M  ",
        string gyo = "005",
        string keta = "007")
    {
        var r = new MainCircuitResult { SequenceNumber = sequence };
        r.Data.ReservedWord = reservedWord;
        r.Data.CircuitPhaseCount = phase;
        r.Data.ElectricalParameterSlots[1].At = epaat;
        r.Data.ElectricalParameterSlots[0].P = epap;
        r.Data.AttachedParameter.MakerCode = makerCode;
        r.Data.DescriptionRow = gyo;
        r.Data.DescriptionColumn = keta;
        return r;
    }

    private static Func<string, MainCircuitResult?> Finder(params MainCircuitResult[] parents)
    {
        var map = new Dictionary<string, MainCircuitResult>();
        foreach (MainCircuitResult p in parents)
        {
            map[p.SequenceNumber] = p;
        }
        return key => map.TryGetValue(key, out MainCircuitResult? v) ? v : null;
    }

    [Fact]
    public void 主幹チェック_全条件OKはエラーなし()
    {
        var records = new List<MainCircuitResult> { Child(parent: "010") };
        MainCircuitResult parent = ParentRec(sequence: "010");

        CircuitParseError? err = PlugInBreakerConnector.CheckMainBreaker(records, Finder(parent));

        Assert.Null(err);
    }

    [Fact]
    public void 主幹チェック_三相トリップ400AT超過はNG()
    {
        var records = new List<MainCircuitResult> { Child(parent: "010") };
        MainCircuitResult parent = ParentRec(sequence: "010", phase: '3', epaat: "00400.001");

        CircuitParseError? err = PlugInBreakerConnector.CheckMainBreaker(records, Finder(parent));

        Assert.NotNull(err);
        Assert.Equal("FY-957E", err!.ErrorCode);
        Assert.Equal("FYMEE80", err.MessageId);
        Assert.Equal(5, err.LineNumber);
        Assert.Equal(7, err.Column);
    }

    [Fact]
    public void 主幹チェック_単相トリップ250AT超過はNG()
    {
        var records = new List<MainCircuitResult> { Child(parent: "010") };
        MainCircuitResult parent = ParentRec(sequence: "010", phase: '1', epaat: "00250.001");

        CircuitParseError? err = PlugInBreakerConnector.CheckMainBreaker(records, Finder(parent));

        Assert.NotNull(err);
    }

    [Fact]
    public void 主幹チェック_極数3P以外はNG()
    {
        var records = new List<MainCircuitResult> { Child(parent: "010") };
        MainCircuitResult parent = ParentRec(sequence: "010", epap: "002");

        CircuitParseError? err = PlugInBreakerConnector.CheckMainBreaker(records, Finder(parent));

        Assert.NotNull(err);
    }

    [Fact]
    public void 主幹チェック_ELB_MCBで三菱以外はNG()
    {
        var records = new List<MainCircuitResult> { Child(parent: "010") };
        MainCircuitResult parent = ParentRec(sequence: "010", reservedWord: "ELB", makerCode: "K  ");

        CircuitParseError? err = PlugInBreakerConnector.CheckMainBreaker(records, Finder(parent));

        Assert.NotNull(err);
    }

    [Fact]
    public void 主幹チェック_ELB_MCBで三菱MNはOK()
    {
        var records = new List<MainCircuitResult> { Child(parent: "010") };
        MainCircuitResult parent = ParentRec(sequence: "010", reservedWord: "MCB", makerCode: "MN ");

        CircuitParseError? err = PlugInBreakerConnector.CheckMainBreaker(records, Finder(parent));

        Assert.Null(err);
    }

    [Fact]
    public void 主幹チェック_CTPと非プラグインと親なしはスキップ()
    {
        var records = new List<MainCircuitResult>
        {
            Child(parent: "010", dataType0: "CTP"),  // 改訂<3> CTP スキップ
            Child(parent: "011", dataType0: "MCB"),  // 非プラグイン スキップ
            Child(parent: "999", dataType0: "CH"),   // 親なし スキップ
        };
        // 親 010 が NG 条件でも上記はスキップされるため検索されない。
        MainCircuitResult ng = ParentRec(sequence: "010", epap: "002");

        CircuitParseError? err = PlugInBreakerConnector.CheckMainBreaker(records, Finder(ng));

        Assert.Null(err);
    }

    [Fact]
    public void CheckMainBreakerのrecordsがnullなら例外()
    {
        Assert.Throws<System.ArgumentNullException>(
            () => PlugInBreakerConnector.CheckMainBreaker(null!, _ => null));
    }

    [Fact]
    public void CheckMainBreakerのfindParentがnullなら例外()
    {
        Assert.Throws<System.ArgumentNullException>(
            () => PlugInBreakerConnector.CheckMainBreaker([], null!));
    }
}
