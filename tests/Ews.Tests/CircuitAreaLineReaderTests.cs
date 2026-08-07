using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Circuits;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CircuitAreaLineReader"/>(【C原典】Fysk11.c の Fysk11_FYDF805_GyoGet)の単体テスト。
/// </summary>
public sealed class CircuitAreaLineReaderTests
{
    private static CircuitDescriptionLine Line(int lineNumber, string circuitText, char command = ' ')
        => new() { LineNumber = lineNumber, CircuitText = circuitText, Command = command };

    [Fact]
    public void 行番号一致の回路内容を返す()
    {
        var lines = new List<CircuitDescriptionLine>
        {
            Line(4, "MCB3P100A"),
            Line(5, "ELB3P30A"),
        };
        Assert.Equal("ELB3P30A", CircuitAreaLineReader.GetCircuitAreaText("005", "010", lines));
    }

    [Fact]
    public void 一致行が無ければ空文字を返す()
    {
        var lines = new List<CircuitDescriptionLine> { Line(4, "MCB3P100A") };
        Assert.Equal("", CircuitAreaLineReader.GetCircuitAreaText("009", "010", lines));
    }

    [Fact]
    public void 削除行はスキップする()
    {
        var lines = new List<CircuitDescriptionLine>
        {
            Line(5, "DELETED", command: 'D'),
            Line(5, "MG"),
        };
        Assert.Equal("MG", CircuitAreaLineReader.GetCircuitAreaText("005", "010", lines));
    }

    [Fact]
    public void 削除行のみ一致なら空文字を返す()
    {
        var lines = new List<CircuitDescriptionLine> { Line(5, "DELETED", command: 'D') };
        Assert.Equal("", CircuitAreaLineReader.GetCircuitAreaText("005", "010", lines));
    }

    [Fact]
    public void 複数一致では最初のレコードを返す()
    {
        var lines = new List<CircuitDescriptionLine>
        {
            Line(5, "FIRST"),
            Line(5, "SECOND"),
        };
        Assert.Equal("FIRST", CircuitAreaLineReader.GetCircuitAreaText("005", "010", lines));
    }

    [Fact]
    public void 桁が記述エリア長以上なら行を繰り上げる()
    {
        // 桁250 → over=250/200=1 → 行5+1=6 を検索。
        var lines = new List<CircuitDescriptionLine>
        {
            Line(5, "ROW5"),
            Line(6, "ROW6"),
        };
        Assert.Equal("ROW6", CircuitAreaLineReader.GetCircuitAreaText("005", "250", lines));
    }

    [Fact]
    public void 桁がちょうど記述エリア長なら1行繰り上げる()
    {
        // 桁200 → over=200/200=1 → 行5+1=6。
        var lines = new List<CircuitDescriptionLine>
        {
            Line(5, "ROW5"),
            Line(6, "ROW6"),
        };
        Assert.Equal("ROW6", CircuitAreaLineReader.GetCircuitAreaText("005", "200", lines));
    }

    [Fact]
    public void 桁が記述エリア長の2倍なら2行繰り上げる()
    {
        // 桁400 → over=400/200=2 → 行5+2=7。
        var lines = new List<CircuitDescriptionLine>
        {
            Line(5, "ROW5"),
            Line(7, "ROW7"),
        };
        Assert.Equal("ROW7", CircuitAreaLineReader.GetCircuitAreaText("005", "400", lines));
    }

    [Fact]
    public void 桁が記述エリア長未満なら繰り上げない()
    {
        var lines = new List<CircuitDescriptionLine>
        {
            Line(5, "ROW5"),
            Line(6, "ROW6"),
        };
        Assert.Equal("ROW5", CircuitAreaLineReader.GetCircuitAreaText("005", "199", lines));
    }

    [Fact]
    public void 空リストは空文字を返す()
    {
        Assert.Equal("", CircuitAreaLineReader.GetCircuitAreaText("005", "010", new List<CircuitDescriptionLine>()));
    }
}
