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
}
