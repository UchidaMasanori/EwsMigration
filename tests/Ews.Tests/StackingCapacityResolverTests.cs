using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="StackingCapacityResolver"/>(【C原典】Fysk00_Set_VA_W)の単体テスト。
/// </summary>
public class StackingCapacityResolverTests
{
    private static EquipmentMaster Equip(string acVa, string dcW)
        => new() { RatedCapacityAcVa = acVa, RatedCapacityDcW = dcW };

    private static MainCircuitResult Rec(string reserved, char element = '2', char voltageKind = 'A', string va = "0000000000")
    {
        var r = new MainCircuitResult();
        r.Data.ReservedWord = reserved;
        r.Data.CircuitElement = element;
        r.Data.CircuitVoltageKind = voltageKind;
        r.Data.ElectricalParameterSlots[1].Va = va;
        return r;
    }

    [Fact]
    public void Resolve_フラグ0のAC機器は定格容量VA_teiva0を使う()
    {
        EquipmentMaster equip = Equip(acVa: "1500", dcW: "800");
        MainCircuitResult record = Rec("WH", voltageKind: 'A');

        StackingCapacityResolver.Resolve(equip, record, 1);

        Assert.Equal(1500.0, record.Work.RatedCapacity);
    }

    [Fact]
    public void Resolve_フラグ0のDC機器は定格容量W_teiwを使う()
    {
        EquipmentMaster equip = Equip(acVa: "1500", dcW: "800");
        MainCircuitResult record = Rec("WH", voltageKind: 'D');

        StackingCapacityResolver.Resolve(equip, record, 1);

        Assert.Equal(800.0, record.Work.RatedCapacity);
    }

    [Fact]
    public void Resolve_フラグ1は電圧区分によらず定格容量Wを使う()
    {
        EquipmentMaster equip = Equip(acVa: "1500", dcW: "800");
        MainCircuitResult record = Rec("WL", voltageKind: 'A');   // フラグ1

        StackingCapacityResolver.Resolve(equip, record, 1);

        Assert.Equal(800.0, record.Work.RatedCapacity);
    }

    [Fact]
    public void Resolve_フラグ2は負荷容量ep_epno_VAを使う()
    {
        EquipmentMaster equip = Equip(acVa: "1500", dcW: "800");
        MainCircuitResult record = Rec("VT", va: "0000002000");   // フラグ2, ep[1].VA=2000

        StackingCapacityResolver.Resolve(equip, record, 1);

        Assert.Equal(2000.0, record.Work.RatedCapacity);
    }

    [Fact]
    public void Resolve_未定義予約語は空文字の既定でフラグ0扱い()
    {
        EquipmentMaster equip = Equip(acVa: "1500", dcW: "800");
        MainCircuitResult record = Rec("XYZ", voltageKind: 'A');   // どの名前付きにも該当せず → 既定(フラグ0 AC)

        StackingCapacityResolver.Resolve(equip, record, 1);

        Assert.Equal(1500.0, record.Work.RatedCapacity);
    }

    [Fact]
    public void Resolve_回路要素が1の機器は積み上げ対象外でゼロ()
    {
        EquipmentMaster equip = Equip(acVa: "1500", dcW: "800");
        MainCircuitResult record = Rec("WH", element: '1', voltageKind: 'A');

        StackingCapacityResolver.Resolve(equip, record, 1);

        Assert.Equal(0.0, record.Work.RatedCapacity);
    }
}
