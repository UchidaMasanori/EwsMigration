using Ews.Domain.Analysis;
using Ews.Domain.Configuration;

namespace Ews.Analysis;

/// <summary>
/// マルヤス製ランプ指定時のデフォルト径サイズ設定。
/// 【C原典】PropChgMALampType(主回路)/PropChgMALampTypeC(制御回路)(Fysk00.c, 改訂&lt;117&gt;/&lt;146&gt;)。
///   マルヤス(MA/MAN)指定のランプで径入力(WL○○P)が無い場合、径サイズを設定する
///   (札幌工場=22mm, それ以外=25mm)。
///
/// 移植済み依存を結線: FyGetFacGrp=<see cref="IFacilityAreaResolver"/> /
///   Fysk11_FYDF805_KkGet=<see cref="CircuitDescriptionArea"/>。
/// </summary>
public sealed class MaruyasuLampRadiusResolver
{
    private const int SapporoFacilityGroup = 1;   // 札幌工場はデフォルト 22P

    private readonly IFacilityAreaResolver _facilityAreaResolver;
    private readonly IRuntimeParameterProvider _parameters;
    private readonly CircuitDescriptionArea _circuitDescriptions;

    public MaruyasuLampRadiusResolver(IFacilityAreaResolver facilityAreaResolver,
                                      IRuntimeParameterProvider parameters,
                                      CircuitDescriptionArea circuitDescriptions)
    {
        ArgumentNullException.ThrowIfNull(facilityAreaResolver);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(circuitDescriptions);
        _facilityAreaResolver = facilityAreaResolver;
        _parameters = parameters;
        _circuitDescriptions = circuitDescriptions;
    }

    /// <summary>
    /// 主回路ランプ(WL/RL/GL/OL/BL)の径サイズを設定する。【C原典】PropChgMALampType。
    /// </summary>
    /// <param name="lamp">主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位。【C原典】mcod。</param>
    /// <param name="sep">電気パラメータ数値版(3, 破壊的に更新)。【C原典】sep。</param>
    public void Resolve(MainCircuitResult lamp, string[] makerCodes, NumericElectricalParameters[] sep)
    {
        ArgumentNullException.ThrowIfNull(lamp);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(sep);

        int facilityGroup = _facilityAreaResolver.GetFacilityGroup(_parameters.ZoneCode);

        if (!IsLamp(lamp.Data.ReservedWord, includeWl: true))
        {
            return;
        }

        if (!IsMaruyasu(makerCodes[0]))
        {
            return;
        }

        string kairoar = _circuitDescriptions.GetDescriptionAt(
            lamp.Data.DescriptionRow, lamp.Data.DescriptionColumn);
        if (kairoar.Length == 0)
        {
            return;         // 取得 NG
        }

        if (kairoar.Contains('P'))
        {
            return;         // 径入力あり
        }

        double radius = facilityGroup == SapporoFacilityGroup ? 22.0 : 25.0;
        string radiusText = facilityGroup == SapporoFacilityGroup ? "022.0" : "025.0";

        sep[0].Ksize = radius;
        sep[2].Ksize = radius;
        lamp.Data.ElectricalParameterSlots[0].Ksize = radiusText;
        lamp.Data.ElectricalParameterSlots[2].Ksize = radiusText;
    }

    /// <summary>
    /// 制御ランプ(RL/GL/OL/BL)の径サイズを設定する。【C原典】PropChgMALampTypeC。
    /// </summary>
    /// <param name="equipment">制御機器情報。【C原典】kk(struct kikijg)。</param>
    /// <param name="makerCodes">メーカーコード選定順位。【C原典】mcod。</param>
    public void ResolveControl(ControlEquipmentInfo equipment, string[] makerCodes)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(makerCodes);

        int facilityGroup = _facilityAreaResolver.GetFacilityGroup(_parameters.ZoneCode);

        if (!IsLamp(equipment.ReservedWord, includeWl: false))
        {
            return;
        }

        // 【C原典】制御版は回路記述取得を メーカー判定より先に行う。
        string kairoar = _circuitDescriptions.GetDescriptionAt(
            equipment.DescriptionRow, equipment.DescriptionColumn);
        if (kairoar.Length == 0)
        {
            return;         // 取得 NG
        }

        if (!IsMaruyasu(makerCodes[0]))
        {
            return;
        }

        if (kairoar.Contains('P'))
        {
            return;         // 径入力あり
        }

        string radiusText = facilityGroup == SapporoFacilityGroup ? "022.0" : "025.0";
        equipment.ElectricalParameterSlots[0].Ksize = radiusText;
        equipment.ElectricalParameterSlots[1].Ksize = radiusText;
        equipment.ElectricalParameterSlots[2].Ksize = radiusText;
    }

    // 【C原典】主回路は WL/RL/GL/OL/BL(先頭2文字)、制御は RL/GL/OL/BL(3文字)。
    private static bool IsLamp(string reservedWord, bool includeWl)
    {
        int width = includeWl ? 2 : 3;
        return (includeWl && Matches(reservedWord, "WL", 2)) ||
               Matches(reservedWord, "RL", width) ||
               Matches(reservedWord, "GL", width) ||
               Matches(reservedWord, "OL", width) ||
               Matches(reservedWord, "BL", width);
    }

    private static bool IsMaruyasu(string makerCode) =>
        Matches(makerCode, "MA ", 3) || Matches(makerCode, "MAN", 3);

    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
