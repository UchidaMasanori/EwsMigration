using Ews.Data.Seeding;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="InverterMcConstantTableLoader"/>(=Fysk01_ReadCnstINV_MC)の移植テスト。
/// </summary>
public sealed class InverterMcConstantTableLoaderTests
{
    private const string Sample =
        "/****************************************************************************/\r\n" +
        "/* <Title> inv003.cns */\r\n" +
        "/* パラメーター| kw |MC品名 */\r\n" +
        "ST     ,00.75,S-T10,\r\n" +
        "ST     ,03.70,S-T21,\r\n" +
        "ET     ,00.10,S-T10,\r\n";

    [Fact]
    public void コメント行を飛ばしデータ行のみ解析する()
    {
        IReadOnlyList<InverterMcConstant> table = InverterMcConstantTableLoader.Parse(Sample);
        Assert.Equal(3, table.Count);
    }

    [Fact]
    public void タイプ_kw_品名を解析する()
    {
        IReadOnlyList<InverterMcConstant> table = InverterMcConstantTableLoader.Parse(Sample);
        InverterMcConstant first = table[0];
        Assert.StartsWith("ST", first.Type);
        Assert.Equal(0.75, first.RatedKw);
        Assert.Equal("S-T10", first.ProductName);
    }

    [Fact]
    public void タイプは7桁で末尾空白を保持する()
    {
        IReadOnlyList<InverterMcConstant> table = InverterMcConstantTableLoader.Parse(Sample);
        Assert.Equal("ST     ", table[0].Type);
        Assert.Equal(7, table[0].Type.Length);
    }

    [Fact]
    public void 先頭ゼロのkwをatof相当で解釈する()
    {
        IReadOnlyList<InverterMcConstant> table = InverterMcConstantTableLoader.Parse(Sample);
        Assert.Equal(3.70, table[1].RatedKw);
    }

    [Fact]
    public void 第2フィールド欠落でkwは0()
    {
        IReadOnlyList<InverterMcConstant> table = InverterMcConstantTableLoader.Parse("ST\r\n");
        Assert.Single(table);
        Assert.Equal(0.0, table[0].RatedKw);
    }

    [Fact]
    public void 第3フィールド欠落で品名は空()
    {
        IReadOnlyList<InverterMcConstant> table = InverterMcConstantTableLoader.Parse("ST     ,01.50\r\n");
        Assert.Single(table);
        Assert.Equal(string.Empty, table[0].ProductName);
    }

    [Fact]
    public void 空行で読込を終了する()
    {
        const string content = "ST     ,00.75,S-T10,\r\n\r\nET     ,00.10,S-T10,\r\n";
        IReadOnlyList<InverterMcConstant> table = InverterMcConstantTableLoader.Parse(content);
        Assert.Single(table);
    }

    [Fact]
    public void リアクトル有無でファイル名を切り替える()
    {
        Assert.Equal("inv003b.cns", InverterMcConstantTableLoader.ResolveFileName(hasReactor: true));
        Assert.Equal("inv003a.cns", InverterMcConstantTableLoader.ResolveFileName(hasReactor: false));
    }
}
