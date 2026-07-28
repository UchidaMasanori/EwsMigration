using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="IncomingPhaseResolver"/>(【C原典】Fysk00_ph)の単体テスト。
/// </summary>
public class IncomingPhaseResolverTests
{
    private static MainCircuitResult Record(string reservedWord, char ph2 = '0', char wr2 = '0', char kpaph = '0')
    {
        var r = new MainCircuitResult();
        r.Data.ReservedWord = reservedWord;
        r.Data.ElectricalParameterSlots[0].Ph2[0] = ph2.ToString();
        r.Data.ElectricalParameterSlots[0].Wr2[0] = wr2.ToString();
        r.Data.CircuitPhaseCount = kpaph;
        return r;
    }

    [Fact]
    public void Resolve_入線が無ければ単相1を返す()
    {
        var records = new List<MainCircuitResult>
        {
            Record("MCB"),
            Record("ELB"),
        };

        Assert.Equal('1', IncomingPhaseResolver.Resolve(records, 1));
    }

    [Fact]
    public void Resolve_入線の相数をそのまま返す()
    {
        var records = new List<MainCircuitResult>
        {
            Record("P", ph2: '3', wr2: '3'),  // 三相三線
            Record("MCB"),
        };

        Assert.Equal('3', IncomingPhaseResolver.Resolve(records, 1));
    }

    [Fact]
    public void Resolve_入線が三相四線なら自機器の回路相数を返す()
    {
        var records = new List<MainCircuitResult>
        {
            Record("P", ph2: '3', wr2: '4'),          // 三相四線
            Record("MCB", kpaph: '1'),                 // 自機器 kpaph=1
        };

        Assert.Equal('1', IncomingPhaseResolver.Resolve(records, 1));
    }

    [Fact]
    public void Resolve_遡って最も近い入線を採用する()
    {
        var records = new List<MainCircuitResult>
        {
            Record("P", ph2: '1'),        // 遠い入線(採用されない)
            Record("MCB"),
            Record("P", ph2: '3', wr2: '3'),  // 近い入線
            Record("ELB"),
        };

        Assert.Equal('3', IncomingPhaseResolver.Resolve(records, 3));
    }

    [Fact]
    public void Resolve_PBSは入線とみなさない()
    {
        var records = new List<MainCircuitResult>
        {
            Record("PBS", ph2: '3'),
            Record("MCB"),
        };

        Assert.Equal('1', IncomingPhaseResolver.Resolve(records, 1));
    }

    [Fact]
    public void Resolve_添字0では常に単相1を返す()
    {
        var records = new List<MainCircuitResult>
        {
            Record("P", ph2: '3', wr2: '3'),
        };

        Assert.Equal('1', IncomingPhaseResolver.Resolve(records, 0));
    }
}
