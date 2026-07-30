using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// ヒューズのデフォルト機器タイプ設定(簡易版 PropChgFuseType_SY2)の移植検証。
///
/// 【C原典】PropChgFuseType_SY2(Fysk00.c:6959)。タイプ指定 "+(" 無し・特注/ブロック(cpf=0)で
/// 機器タイプ GT、メーカー指定 "MK=" 無しでメーカー FT。地区/品番/後続ランプ非依存の簡易版。
/// </summary>
public sealed class SimpleFuseDefaultTypeResolverTests
{
    private static SimpleFuseDefaultTypeResolver Build(string circuitText) =>
        new(new CircuitDescriptionArea(
            [new CircuitDescriptionLine { LineNumber = 5, CircuitText = circuitText }]));

    private static MainCircuitResult Fuse(string reservedWord = "F", string dataType0 = "") =>
        new()
        {
            SequenceNumber = "010",
            Data = new MainCircuitData
            {
                ReservedWord = reservedWord,
                DescriptionRow = "005",
                DescriptionColumn = "001",
                DataType = [dataType0, "", "", "", "", "", ""],
            },
        };

    private static string[] Types() => ["", "", "", "", "", "", ""];

    [Fact]
    public void 予約語がFでなければ何もしない()
    {
        MainCircuitResult mcb = Fuse("MCB");
        string[] makerCodes = ["K  ", "   ", "   ", "   "];
        string[] dataTypes = Types();

        Build("F,").Resolve(mcb, makerCodes, dataTypes, Types(), 0);

        Assert.Equal("K  ", makerCodes[0]);
        Assert.Equal("", dataTypes[0]);
    }

    [Fact]
    public void 特注でタイプ指定なしならGTとFTにする()
    {
        MainCircuitResult fuse = Fuse();
        string[] makerCodes = ["K  ", "   ", "   ", "   "];
        string[] dataTypes = Types();
        string[] displayTypes = Types();

        Build("F,").Resolve(fuse, makerCodes, dataTypes, displayTypes, 0);

        Assert.Equal("GT     ", dataTypes[0]);
        Assert.Equal("GT     ", displayTypes[0]);
        Assert.Equal("GT     ", fuse.Data.DataType[0]);
        Assert.Equal("FT ", makerCodes[0]);
    }

    [Fact]
    public void コンポ盤はGTにしない()
    {
        MainCircuitResult fuse = Fuse();
        string[] makerCodes = ["K  ", "   ", "   ", "   "];
        string[] dataTypes = Types();

        Build("F,").Resolve(fuse, makerCodes, dataTypes, Types(), 1);

        Assert.Equal("", dataTypes[0]);
        Assert.Equal("K  ", makerCodes[0]);
    }

    [Fact]
    public void タイプ指定ありは変更しない()
    {
        MainCircuitResult fuse = Fuse();
        string[] makerCodes = ["K  ", "   ", "   ", "   "];
        string[] dataTypes = Types();

        Build("F+(GT),").Resolve(fuse, makerCodes, dataTypes, Types(), 0);

        Assert.Equal("", dataTypes[0]);
        Assert.Equal("K  ", makerCodes[0]);
    }

    [Fact]
    public void メーカー指定MKありはFTにしない()
    {
        MainCircuitResult fuse = Fuse();
        string[] makerCodes = ["K  ", "   ", "   ", "   "];
        string[] dataTypes = Types();

        Build("FMK=M,").Resolve(fuse, makerCodes, dataTypes, Types(), 0);

        Assert.Equal("GT     ", dataTypes[0]);   // タイプは GT
        Assert.Equal("K  ", makerCodes[0]);      // メーカーは MK= 指定ありのため据置
    }

    [Fact]
    public void メーカーがK以外なら何もしない()
    {
        MainCircuitResult fuse = Fuse();
        string[] makerCodes = ["M  ", "   ", "   ", "   "];
        string[] dataTypes = Types();

        Build("F,").Resolve(fuse, makerCodes, dataTypes, Types(), 0);

        Assert.Equal("", dataTypes[0]);
        Assert.Equal("M  ", makerCodes[0]);
    }
}
