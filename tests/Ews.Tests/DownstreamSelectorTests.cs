using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="DownstreamSelector"/>(【C原典】Fyss35_Select_Karyu_Sub)の単体テスト。
/// </summary>
public class DownstreamSelectorTests
{
    private static MainCircuitResult Record(string datano, string oyatno, char systemKind = '1')
    {
        var r = new MainCircuitResult { SequenceNumber = datano };
        r.Data.ParentSequenceNumber = oyatno;
        r.Data.SystemKind = systemKind;
        return r;
    }

    [Fact]
    public void SelectDownstream_指定機器の直後に連なる下流を抽出する()
    {
        // 指定機器 datano=001(oyatno=000)。直後に oyatno>000 の下流が連なる。
        var records = new List<MainCircuitResult>
        {
            Record("001", "000"),   // 指定機器(sijino=1)
            Record("002", "001"),   // 下流
            Record("003", "002"),   // 下流
            Record("004", "000"),   // 兄弟(打ち切り)
        };

        var result = DownstreamSelector.SelectDownstream(records, 1);

        Assert.NotNull(result);
        Assert.Equal(new[] { 2, 3 }, result);
    }

    [Fact]
    public void SelectDownstream_兄弟が現れた時点で打ち切る()
    {
        var records = new List<MainCircuitResult>
        {
            Record("010", "005"),   // 指定機器(oyatno=005)
            Record("011", "010"),   // 下流(010>005)
            Record("012", "005"),   // 同じ親=兄弟(005は005より大きくない → 打ち切り)
            Record("013", "011"),   // 打ち切り後なので拾わない
        };

        var result = DownstreamSelector.SelectDownstream(records, 1);

        Assert.NotNull(result);
        Assert.Equal(new[] { 11 }, result);
    }

    [Fact]
    public void SelectDownstream_下流が無ければ空リスト()
    {
        var records = new List<MainCircuitResult>
        {
            Record("001", "000"),
            Record("002", "000"),   // 兄弟(下流でない)
        };

        var result = DownstreamSelector.SelectDownstream(records, 1);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void SelectDownstream_系統種別が1以外はnull()
    {
        var records = new List<MainCircuitResult>
        {
            Record("001", "000", systemKind: '2'),  // SP系統
            Record("002", "001"),
        };

        Assert.Null(DownstreamSelector.SelectDownstream(records, 1));
    }

    [Fact]
    public void SelectDownstream_範囲外の指定番号はnull()
    {
        var records = new List<MainCircuitResult>
        {
            Record("001", "000"),
        };

        Assert.Null(DownstreamSelector.SelectDownstream(records, 2));  // sijino > Pmainc
        Assert.Null(DownstreamSelector.SelectDownstream(records, 0));  // sijino < 1
    }

    [Fact]
    public void SelectDownstream_末尾機器指定は空リスト()
    {
        var records = new List<MainCircuitResult>
        {
            Record("001", "000"),
            Record("002", "001"),   // 末尾を指定
        };

        var result = DownstreamSelector.SelectDownstream(records, 2);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
