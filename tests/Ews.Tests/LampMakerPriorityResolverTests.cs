using Ews.Analysis;
using Ews.Data.Seeding;
using Ews.Domain.Analysis;
using Ews.Domain.Configuration;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// ランプ類の優先メーカー変更(PropChgWlLampMaker / PropChgSeigyoLampMaker / PropCnsLampRead)の移植検証。
///
/// 【C原典】Fysk00.c(toku/sekkei/src, 改訂&lt;88&gt;/&lt;142&gt;)。メーカー未指定ランプについて地区・予約語で
/// メーカーコード順位を設定し、sel_LAMP.cns の一致行で上書きする。
/// </summary>
public sealed class LampMakerPriorityResolverTests
{
    private static LampMakerPriorityResolver Build(int facilityGroup, IReadOnlyList<LampMakerEntry> table)
    {
        const string zone = "Z";
        var facility = new InMemoryFacilityAreaResolver([new FacilityAreaEntry(zone, facilityGroup)]);
        var parameters = new InMemoryRuntimeParameterProvider(
            new Dictionary<string, string?> { [RuntimeParameterNames.ZoneCode] = zone });
        return new LampMakerPriorityResolver(facility, parameters, table);
    }

    private static MainCircuitResult Lamp(string reservedWord = "WL", string makerCode = "") =>
        new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = reservedWord,
                AttachedParameter = new AttachedParameters { MakerCode = makerCode },
            },
        };

    private static ControlEquipmentInfo ControlLamp(string reservedWord = "RL", string makerCode = "") =>
        new() { ReservedWord = reservedWord, MakerCode = makerCode };

    // ---- sel_LAMP.cns ローダー ----

    [Fact]
    public void Loader_コメント行を除き工場コード予約語メーカーを取込む()
    {
        const string cns =
            "/* comment */\n" +
            "1,WL ,MAN,MA ,IZ ,   ,\n" +
            "1,RL ,MAN,MA ,IZ ,   ,\n" +
            "/*----< End >----*/\n";

        IReadOnlyList<LampMakerEntry> table = LampMakerTableLoader.Parse(cns);

        Assert.Equal(2, table.Count);
        Assert.Equal(1, table[0].FacilityGroup);
        Assert.Equal("WL ", table[0].ReservedWord);
        Assert.Equal("MAN", table[0].MakerCodes[0]);
        Assert.Equal("MA ", table[0].MakerCodes[1]);
        Assert.Equal("IZ ", table[0].MakerCodes[2]);
        Assert.Equal("   ", table[0].MakerCodes[3]);
    }

    // ---- 主回路 PropChgWlLampMaker ----

    [Fact]
    public void 主回路_メーカー指定ありは変更しない()
    {
        MainCircuitResult lamp = Lamp("WL", makerCode: "M  ");
        string[] makerCodes = ["K  ", "   ", "   ", "   "];

        Build(5, []).Resolve(lamp, makerCodes);

        Assert.Equal("K  ", makerCodes[0]);
    }

    [Fact]
    public void 主回路_水俣以外のWLはイズミ優先にする()
    {
        MainCircuitResult lamp = Lamp("WL");
        string[] makerCodes = ["   ", "   ", "   ", "   "];

        Build(5, []).Resolve(lamp, makerCodes);

        Assert.Equal("IZ ", makerCodes[0]);
        Assert.Equal("MAN", makerCodes[1]);
        Assert.Equal("MA ", makerCodes[2]);
    }

    [Fact]
    public void 主回路_水俣のWLはマルヤス優先にする()
    {
        MainCircuitResult lamp = Lamp("WL");
        string[] makerCodes = ["   ", "   ", "   ", "   "];

        Build(4, []).Resolve(lamp, makerCodes);

        Assert.Equal("MAN", makerCodes[0]);
        Assert.Equal("MA ", makerCodes[1]);
        Assert.Equal("IZ ", makerCodes[2]);
    }

    [Fact]
    public void 主回路_selLAMP一致行があれば上書きする()
    {
        MainCircuitResult lamp = Lamp("WL");
        string[] makerCodes = ["   ", "   ", "   ", "   "];
        IReadOnlyList<LampMakerEntry> table = [new LampMakerEntry(1, "WL ", ["MAN", "MA ", "IZ ", "   "])];

        Build(1, table).Resolve(lamp, makerCodes);

        Assert.Equal("MAN", makerCodes[0]);   // sel_LAMP.cns の順位で上書き
        Assert.Equal("MA ", makerCodes[1]);
        Assert.Equal("IZ ", makerCodes[2]);
    }

    // ---- 制御回路 PropChgSeigyoLampMaker ----

    [Fact]
    public void 制御_水俣以外のRLはイズミ優先にする()
    {
        ControlEquipmentInfo lamp = ControlLamp("RL");
        string[] makerCodes = ["   ", "   ", "   ", "   "];

        Build(5, []).ResolveControl(lamp, makerCodes);

        Assert.Equal("IZ ", makerCodes[0]);
        Assert.Equal("MAN", makerCodes[1]);
    }

    [Fact]
    public void 制御_水俣のRLはマルヤス優先にする()
    {
        ControlEquipmentInfo lamp = ControlLamp("GL");
        string[] makerCodes = ["   ", "   ", "   ", "   "];

        Build(4, []).ResolveControl(lamp, makerCodes);

        Assert.Equal("MAN", makerCodes[0]);
        Assert.Equal("MA ", makerCodes[1]);
        Assert.Equal("IZ ", makerCodes[2]);
    }

    [Fact]
    public void 制御_メーカー指定ありは変更しない()
    {
        ControlEquipmentInfo lamp = ControlLamp("RL", makerCode: "M  ");
        string[] makerCodes = ["K  ", "   ", "   ", "   "];

        Build(5, []).ResolveControl(lamp, makerCodes);

        Assert.Equal("K  ", makerCodes[0]);
    }
}
