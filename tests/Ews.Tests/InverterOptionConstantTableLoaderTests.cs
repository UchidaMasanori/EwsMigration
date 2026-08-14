using Ews.Data.Seeding;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="InverterOptionConstantTableLoader"/>(=Fysk01_ReadCnstINV_OP)の移植テスト。
/// </summary>
public sealed class InverterOptionConstantTableLoaderTests
{
    private const string Sample =
        "/****************************************************************************/\r\n" +
        "/* <Title> invAC.cns */\r\n" +
        "AC     ,00.75,OPT-AC0075,\r\n" +
        "AC     ,03.70,OPT-AC0370,\r\n" +
        "AC     ,07.50,OPT-AC0750,\r\n";

    [Fact]
    public void コメント行を飛ばしデータ行のみ解析する()
    {
        IReadOnlyList<InverterOptionConstant> table = InverterOptionConstantTableLoader.Parse(Sample);
        Assert.Equal(3, table.Count);
    }

    [Fact]
    public void タイプ_kw_品名を解析する()
    {
        IReadOnlyList<InverterOptionConstant> table = InverterOptionConstantTableLoader.Parse(Sample);
        InverterOptionConstant second = table[1];
        Assert.Equal("AC     ", second.Type);
        Assert.Equal(3.70, second.RatedKw);
        Assert.Equal("OPT-AC0370", second.ProductName);
    }

    [Fact]
    public void 先頭ゼロのkwをatof相当で解釈する()
    {
        IReadOnlyList<InverterOptionConstant> table = InverterOptionConstantTableLoader.Parse(Sample);
        Assert.Equal(0.75, table[0].RatedKw);
        Assert.Equal(7.50, table[2].RatedKw);
    }

    [Fact]
    public void 第2フィールド欠落でkwは0()
    {
        IReadOnlyList<InverterOptionConstant> table =
            InverterOptionConstantTableLoader.Parse("AC     \r\n");
        Assert.Single(table);
        Assert.Equal(0.0, table[0].RatedKw);
    }

    [Fact]
    public void 第3フィールド欠落で品名は空()
    {
        IReadOnlyList<InverterOptionConstant> table =
            InverterOptionConstantTableLoader.Parse("AC     ,01.50\r\n");
        Assert.Single(table);
        Assert.Equal(string.Empty, table[0].ProductName);
    }

    [Fact]
    public void 空行で読込を終了する()
    {
        const string content =
            "AC     ,00.75,OPT-AC0075,\r\n\r\nAC     ,03.70,OPT-AC0370,\r\n";
        IReadOnlyList<InverterOptionConstant> table = InverterOptionConstantTableLoader.Parse(content);
        Assert.Single(table);
    }
}
