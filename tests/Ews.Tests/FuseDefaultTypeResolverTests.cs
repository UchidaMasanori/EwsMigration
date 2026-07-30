using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Ews.Domain.Configuration;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// ヒューズのデフォルト機器タイプ設定(PropChgFuseType_SY)の移植検証。
///
/// 【C原典】PropChgFuseType_SY(Fysk00.c:6350)。回路内容記述・地区グループ・ヒューズ個数・
/// 品番情報・後続ランプ径から機器タイプを "GT"、メーカーを FT へ調整し、子 WL の電圧を変更する。
/// 移植済み依存(IFacilityAreaResolver/CircuitDescriptionArea/IPartNumberInfoRepository/
/// MakerCodePriorityAdjuster/WlCircuitVoltageAdjuster)を結線した統合関数。
/// </summary>
public sealed class FuseDefaultTypeResolverTests
{
    private const string ZoneHead = "01212";     // 本社地区(グループ5)想定
    private const string ZoneSapporo = "74000";  // 札幌工場(グループ1)

    private sealed class StubPartNumberRepository(PartNumberInfo? info) : IPartNumberInfoRepository
    {
        public PartNumberInfo? Find(string requestDetailNumber) => info;
    }

    private static FuseDefaultTypeResolver Build(int facilityGroup, string zoneCode,
        string circuitText, PartNumberInfo? partNumber = null)
    {
        var facility = new InMemoryFacilityAreaResolver([new FacilityAreaEntry(zoneCode, facilityGroup)]);
        var parameters = new InMemoryRuntimeParameterProvider(
            new Dictionary<string, string?> { [RuntimeParameterNames.ZoneCode] = zoneCode });
        var area = new CircuitDescriptionArea(
            [new CircuitDescriptionLine { LineNumber = 5, CircuitText = circuitText }]);
        return new FuseDefaultTypeResolver(facility, parameters, area,
            new StubPartNumberRepository(partNumber));
    }

    private static MainCircuitResult Fuse(string gyocd, char qty, string dataType0 = "", string voltage = "210") =>
        new()
        {
            SequenceNumber = "010",
            Data = new MainCircuitData
            {
                ReservedWord = "F",
                LineTypeCode = gyocd,
                DescriptionRow = "005",
                DescriptionColumn = "001",
                DataType = [dataType0, "", "", "", "", "", ""],
                CircuitVoltage = [voltage, "000", "000"],
                ElectricalParameterSlots = [new ElectricalParameters { Qty = qty }, new(), new()],
            },
        };

    private static MainCircuitResult WlChild(string voltage = "105") =>
        new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = "WL",
                ParentSequenceNumber = "010",
                CircuitVoltage = [voltage, "000", "000"],
            },
        };

    [Fact]
    public void 予約語がFでなければ何もしない()
    {
        FuseDefaultTypeResolver resolver = Build(5, ZoneHead, "F,WL,");
        MainCircuitResult mcb = Fuse("PM ", '1');
        mcb.Data.ReservedWord = "MCB";
        string[] makerCodes = ["K  ", "   ", "   ", "   "];

        resolver.Resolve(mcb, makerCodes, Types(), Types(), [mcb], 0, "2607AL01");

        Assert.Equal("K  ", makerCodes[0]);
    }

    [Fact]
    public void PM行以外はメーカー調整して終了する()
    {
        FuseDefaultTypeResolver resolver = Build(5, ZoneHead, "F,WL,");
        MainCircuitResult fuse = Fuse("M  ", '1');
        string[] makerCodes = ["K  ", "F  ", "M  ", "   "];

        resolver.Resolve(fuse, makerCodes, Types(), Types(), [fuse], 0, "2607AL01");

        // FT/F/K/OT に設定後、保存値[K,F,M]に含まれる F,K のみ前詰めで残る。
        Assert.Equal("F  ", makerCodes[0]);
        Assert.Equal("K  ", makerCodes[1]);
        Assert.Equal("   ", makerCodes[2]);
    }

    [Fact]
    public void ヒューズ2個以上はGTのときメーカーをFTにする()
    {
        FuseDefaultTypeResolver resolver = Build(5, ZoneHead, "F,WL,");
        MainCircuitResult fuse = Fuse("PM ", '2', dataType0: "GT     ");
        string[] makerCodes = ["K  ", "   ", "   ", "   "];

        resolver.Resolve(fuse, makerCodes, GtTypes(), GtTypes(), [fuse], 0, "2607AL01");

        Assert.Equal("FT ", makerCodes[0]);
    }

    [Fact]
    public void F以降の記述が無くGTならメーカーをFTにする()
    {
        FuseDefaultTypeResolver resolver = Build(5, ZoneHead, "F");   // F 以降の記述なし
        MainCircuitResult fuse = Fuse("PM ", '1', dataType0: "GT     ");
        string[] makerCodes = ["K  ", "   ", "   ", "   "];

        resolver.Resolve(fuse, makerCodes, GtTypes(), GtTypes(), [fuse], 0, "2607AL01");

        Assert.Equal("FT ", makerCodes[0]);
    }

    [Fact]
    public void 特注WLランプはGTタイプとFTメーカーに変更し子WL電圧を複写する()
    {
        FuseDefaultTypeResolver resolver = Build(5, ZoneHead, "F,WL,");
        MainCircuitResult fuse = Fuse("PM ", '1', voltage: "210");
        MainCircuitResult wl = WlChild();
        string[] makerCodes = ["K  ", "   ", "   ", "   "];
        string[] dataTypes = Types();
        string[] displayTypes = Types();

        resolver.Resolve(fuse, makerCodes, dataTypes, displayTypes, [fuse, wl], 0, "2607AL01");

        Assert.Equal("GT     ", dataTypes[0]);
        Assert.Equal("GT     ", displayTypes[0]);
        Assert.Equal("GT     ", fuse.Data.DataType[0]);
        Assert.Equal("FT ", makerCodes[0]);
        Assert.Equal("210", wl.Data.CircuitVoltage[0]);   // FT(非河村)なので F の電圧を複写
    }

    [Fact]
    public void 札幌工場かつ特注はGTにして子WL電圧を変更し終了する()
    {
        FuseDefaultTypeResolver resolver = Build(1, ZoneSapporo, "F,WL,");
        MainCircuitResult fuse = Fuse("PM ", '1', voltage: "210");
        MainCircuitResult wl = WlChild();
        string[] makerCodes = ["K  ", "   ", "   ", "   "];
        string[] dataTypes = Types();

        resolver.Resolve(fuse, makerCodes, dataTypes, Types(), [fuse, wl], 0, "2607AL01");

        Assert.Equal("GT     ", dataTypes[0]);
        Assert.Equal("FT ", makerCodes[0]);
        Assert.Equal("210", wl.Data.CircuitVoltage[0]);
    }

    [Fact]
    public void コンポ盤でWLユニット品番ならGTにする()
    {
        var partNumber = new PartNumberInfo { InputPartNumber = "GWL-GM1-GQ20" };
        FuseDefaultTypeResolver resolver = Build(5, ZoneHead, "F,WL,", partNumber);
        MainCircuitResult fuse = Fuse("PM ", '1');
        MainCircuitResult wl = WlChild();
        string[] makerCodes = ["K  ", "   ", "   ", "   "];
        string[] dataTypes = Types();

        resolver.Resolve(fuse, makerCodes, dataTypes, Types(), [fuse, wl], 1, "2607AL01");

        Assert.Equal("GT     ", dataTypes[0]);
        Assert.Equal("FT ", makerCodes[0]);
    }

    private static string[] Types() => ["", "", "", "", "", "", ""];
    private static string[] GtTypes() => ["GT     ", "", "", "", "", "", ""];
}
