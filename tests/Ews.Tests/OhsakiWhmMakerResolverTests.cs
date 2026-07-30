using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 三菱製WH優先営業所チェック(<see cref="MitsubishiWhmPriorityChecker"/>)の移植検証。
/// 【C原典】PropChkHibknNum(Fysk00.c:6130)。
/// </summary>
public sealed class MitsubishiWhmPriorityCheckerTests
{
    private static MitsubishiWhmPriorityChecker Build(
        IReadOnlyList<NonPropertyOfficeEntry> table, IReadOnlyList<string> priority)
        => new(table, priority);

    [Fact]
    public void 営業所コードが優先非物件コードに紐づくとPriority()
    {
        var checker = Build(
            [new NonPropertyOfficeEntry("CE", ["TE", "AK"])],
            ["CE"]);

        Assert.Equal(MitsubishiWhmPriority.Priority, checker.Check("TE"));
        Assert.Equal(MitsubishiWhmPriority.Priority, checker.Check("AK"));
    }

    [Fact]
    public void 非物件コードが優先一覧になければNotPriority()
    {
        var checker = Build(
            [new NonPropertyOfficeEntry("CE", ["TE"])],
            []);

        Assert.Equal(MitsubishiWhmPriority.NotPriority, checker.Check("TE"));
    }

    [Fact]
    public void 営業所コードが見つからなければError()
    {
        var checker = Build(
            [new NonPropertyOfficeEntry("CE", ["TE"])],
            ["CE"]);

        Assert.Equal(MitsubishiWhmPriority.Error, checker.Check("ZZ"));
    }

    [Fact]
    public void 先頭2桁で照合する()
    {
        var checker = Build(
            [new NonPropertyOfficeEntry("CE", ["TE"])],
            ["CE"]);

        Assert.Equal(MitsubishiWhmPriority.Priority, checker.Check("TE999"));
    }
}

/// <summary>
/// 大崎製WHの新品番選定(<see cref="OhsakiWhmMakerResolver"/>)の移植検証。
/// 【C原典】PropSelNewONWhm(Fysk00.c:2892)。
/// </summary>
public sealed class OhsakiWhmMakerResolverTests
{
    // 非優先営業所を返すチェッカ(非物件コード CE は優先一覧になし)。
    private static OhsakiWhmMakerResolver NotPriorityResolver() =>
        new(new MitsubishiWhmPriorityChecker([new NonPropertyOfficeEntry("CE", ["TE"])], []));

    // 優先営業所を返すチェッカ。
    private static OhsakiWhmMakerResolver PriorityResolver() =>
        new(new MitsubishiWhmPriorityChecker([new NonPropertyOfficeEntry("CE", ["TE"])], ["CE"]));

    private static MainCircuitResult Wh(char hz = '5', char pole = '3')
    {
        MainCircuitResult wh = new()
        {
            Data = new MainCircuitData
            {
                ReservedWord = "WH",
                DataType = ["", "", "", "", "", "", ""],
                CircuitPoleCount = pole,
                AttachedParameter = new AttachedParameters(),
            },
        };
        wh.Data.ElectricalParameterSlots[2].Hz = hz == '5' ? "50" : "60";
        return wh;
    }

    private static string[] Codes() => ["   ", "   ", "   ", "   "];
    private static string[] Types() => ["", "", "", "", "", "", ""];

    [Fact]
    public void 非優先営業所の50HzWHは大崎製ONOに変更する()
    {
        MainCircuitResult wh = Wh();
        string[] codes = Codes();
        string[] wtype = Types();

        NotPriorityResolver().Resolve(wh, codes, wtype, "TE");

        Assert.Equal("ON ", codes[0]);
        Assert.Equal("O  ", codes[1]);
    }

    [Fact]
    public void 条件を満たすと表示タイプをKEにする()
    {
        MainCircuitResult wh = Wh();
        wh.Data.DataType[2] = "KM";
        wh.Data.DataType[3] = "NOTHING";
        wh.Data.ElectricalParameterSlots[0].A1 = "00000.000";
        string[] codes = Codes();
        string[] wtype = Types();

        NotPriorityResolver().Resolve(wh, codes, wtype, "TE");

        Assert.Equal("KE     ", wtype[0]);
    }

    [Fact]
    public void 優先営業所のWHは変更しない()
    {
        MainCircuitResult wh = Wh();
        string[] codes = Codes();
        string[] wtype = Types();

        PriorityResolver().Resolve(wh, codes, wtype, "TE");

        Assert.Equal("   ", codes[0]);
    }

    [Fact]
    public void 検定タイプBNのWHは変更しない()
    {
        MainCircuitResult wh = Wh();
        wh.Data.DataType[1] = "BN";
        string[] codes = Codes();
        string[] wtype = Types();

        NotPriorityResolver().Resolve(wh, codes, wtype, "TE");

        Assert.Equal("   ", codes[0]);
    }

    [Fact]
    public void 周波数60HzのWHは変更しない()
    {
        MainCircuitResult wh = Wh('6');
        string[] codes = Codes();
        string[] wtype = Types();

        NotPriorityResolver().Resolve(wh, codes, wtype, "TE");

        Assert.Equal("   ", codes[0]);
    }

    [Fact]
    public void メーカー指定ありのWHは変更しない()
    {
        MainCircuitResult wh = Wh();
        wh.Data.AttachedParameter.MakerCode = "M  ";
        string[] codes = Codes();
        string[] wtype = Types();

        NotPriorityResolver().Resolve(wh, codes, wtype, "TE");

        Assert.Equal("   ", codes[0]);
    }
}
