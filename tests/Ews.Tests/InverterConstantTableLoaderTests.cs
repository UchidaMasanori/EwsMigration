using Ews.Data.Seeding;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="InverterConstantTableLoader"/>(=Fysk01_ReadCnstINV001)の移植テスト。
/// </summary>
public sealed class InverterConstantTableLoaderTests
{
    private const string Sample =
        "/****************************************************************************/\r\n" +
        "/* <Title> inv001.cns */\r\n" +
        "INV1   T2     T3     T4     T5     T6     T7     ,05.50,\r\n" +
        "INV1   T2     T3     T4     T5     T6     T7     ,07.50,\r\n";

    [Fact]
    public void コメント行を飛ばしデータ行のみ解析する()
    {
        IReadOnlyList<InverterConstant> table = InverterConstantTableLoader.ParseInv001(Sample);
        Assert.Equal(2, table.Count);
    }

    [Fact]
    public void タイプ49バイトを7スロット7桁へ展開する()
    {
        IReadOnlyList<InverterConstant> table = InverterConstantTableLoader.ParseInv001(Sample);
        InverterConstant first = table[0];
        Assert.Equal(7, first.Types.Count);
        Assert.All(first.Types, slot => Assert.Equal(7, slot.Length));
        Assert.Equal("INV1   ", first.Types[0]);
        Assert.Equal("T7     ", first.Types[6]);
    }

    [Fact]
    public void 先頭ゼロのkwをatof相当で解釈する()
    {
        IReadOnlyList<InverterConstant> table = InverterConstantTableLoader.ParseInv001(Sample);
        Assert.Equal(5.50, table[0].RatedKw);
        Assert.Equal(7.50, table[1].RatedKw);
    }

    [Fact]
    public void 第2フィールド欠落でkwは0()
    {
        IReadOnlyList<InverterConstant> table =
            InverterConstantTableLoader.ParseInv001("INV1   \r\n");
        Assert.Single(table);
        Assert.Equal(0.0, table[0].RatedKw);
    }

    [Fact]
    public void タイプが49バイト未満なら空白で埋める()
    {
        IReadOnlyList<InverterConstant> table =
            InverterConstantTableLoader.ParseInv001("AB,03.70,\r\n");
        Assert.Single(table);
        Assert.Equal("AB     ", table[0].Types[0]);
        Assert.Equal("       ", table[0].Types[6]);
    }

    [Fact]
    public void 空行で読込を終了する()
    {
        const string content =
            "INV1   T2     T3     T4     T5     T6     T7     ,05.50,\r\n" +
            "\r\n" +
            "INV1   T2     T3     T4     T5     T6     T7     ,07.50,\r\n";
        IReadOnlyList<InverterConstant> table = InverterConstantTableLoader.ParseInv001(content);
        Assert.Single(table);
    }
}
