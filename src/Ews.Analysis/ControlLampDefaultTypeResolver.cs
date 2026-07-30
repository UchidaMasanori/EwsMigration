using Ews.Domain.Analysis;
using Ews.Domain.Configuration;

namespace Ews.Analysis;

/// <summary>
/// イズミ製ランプ(制御回路)のデフォルト機器タイプ・径サイズ設定。
/// 【C原典】PropChgLampType(Fysk00.c, 改訂&lt;46&gt;?, LAMP22 有効時)。
///   イズミ(IZ)指定の RL/GL/OL/BL について回路記述・地区グループから機器タイプ(RE/TR/AN/LED)と
///   径サイズを設定する。主回路版 <see cref="WlLampDefaultTypeResolver"/> の制御回路対応。
///
/// 移植済み依存を結線: FyGetFacGrp=<see cref="IFacilityAreaResolver"/> /
///   Fysk11_FYDF805_KkGet=<see cref="CircuitDescriptionArea"/> /
///   PropSetDefLampType=<see cref="LampDefaultTypeResolver"/>。
/// </summary>
public sealed class ControlLampDefaultTypeResolver
{
    private const int MinamataFacilityGroup = 4;   // 水俣工場

    private readonly IFacilityAreaResolver _facilityAreaResolver;
    private readonly IRuntimeParameterProvider _parameters;
    private readonly CircuitDescriptionArea _circuitDescriptions;

    public ControlLampDefaultTypeResolver(IFacilityAreaResolver facilityAreaResolver,
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

    /// <summary>制御ランプの既定機器タイプ・径サイズを設定する。【C原典】PropChgLampType。</summary>
    /// <param name="equipment">制御機器情報。【C原典】kk(struct kikijg)。</param>
    /// <param name="makerCodes">メーカーコード選定順位。【C原典】mcod。</param>
    /// <param name="dataTypes">機器タイプ(7×7, 破壊的に更新)。【C原典】dtype。</param>
    /// <param name="displayTypes">表示用機器タイプ(7×7, 破壊的に更新)。【C原典】wtype。</param>
    public void Resolve(ControlEquipmentInfo equipment, string[] makerCodes,
                        string[] dataTypes, string[] displayTypes)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(displayTypes);

        int facilityGroup = _facilityAreaResolver.GetFacilityGroup(_parameters.ZoneCode);

        if (!Matches(equipment.ReservedWord, "RL ", 3) &&
            !Matches(equipment.ReservedWord, "GL ", 3) &&
            !Matches(equipment.ReservedWord, "OL ", 3) &&
            !Matches(equipment.ReservedWord, "BL ", 3))
        {
            return;
        }

        string kairoar = _circuitDescriptions.GetDescriptionAt(
            equipment.DescriptionRow, equipment.DescriptionColumn);
        if (kairoar.Length == 0)
        {
            return;         // 取得 NG
        }

        string[] defType = ["", "", "", "", "", "", ""];

        if (Matches(makerCodes[0], "IZ ", 3))
        {
            if (!Contains(kairoar, "+("))       // タイプ指定なし
            {
                if (facilityGroup == MinamataFacilityGroup)
                {
                    defType[1] = "RE     ";
                }
                else
                {
                    ApplyTypeAndRadius(0, equipment, kairoar, defType);
                }
            }
            else                                // タイプ指定あり
            {
                if (facilityGroup == MinamataFacilityGroup)
                {
                    // 【C原典】"+(" 以降の ')' で切詰めた範囲に AN/SQ/WP/P1/P2 が無ければ RE 判定。
                    string inside = TruncateAtParen(kairoar);
                    if (!Contains(inside, "AN") && !Contains(inside, "SQ") &&
                        !Contains(inside, "WP") && !Contains(inside, "P1") &&
                        !Contains(inside, "P2"))
                    {
                        defType[1] = "RE     ";
                    }
                }
                else
                {
                    ApplyTypeAndRadius(1, equipment, kairoar, defType);
                }
            }

            for (int i = 0; i < 7; i++)
            {
                if (defType[i].Length > 0)
                {
                    SetTypeSlot(equipment, dataTypes, displayTypes, i, defType[i]);
                }
            }
        }

        // LED タイプのデフォルト設定(改訂<65>)。現行値 "" を渡し LED または未変更(空)を得る。
        string ledType = LampDefaultTypeResolver.ResolveDefaultType(kairoar, string.Empty);
        if (ledType.Length > 0)
        {
            SetTypeSlot(equipment, dataTypes, displayTypes, 3, ledType);
        }
    }

    /// <summary>
    /// 制御ランプのデフォルトタイプ(TR/AN)・径サイズ(22)を設定する。
    /// 【C原典】PropChgSeigyolTypeAndKei(Fysk00.c, 改訂&lt;80&gt;)。
    /// </summary>
    private static void ApplyTypeAndRadius(int typeSpecified, ControlEquipmentInfo equipment,
                                           string kairoar, string[] defType)
    {
        int radius = ExtractRadius(kairoar);
        if (radius != 0 && radius != 22)
        {
            return;
        }

        if (typeSpecified == 0)
        {
            defType[0] = "TR     ";
            if (equipment.DataType[1].Length == 0)      // 【C原典】datatype[1][0]=='\0'(未設定)
            {
                defType[1] = "AN     ";
            }
        }
        else if (!Contains(kairoar, "DI"))
        {
            // +(DI) がなければデフォルトタイプ変更。
            defType[0] = "TR     ";
        }

        equipment.ElectricalParameterSlots[0].Ksize = "022.0";
        equipment.ElectricalParameterSlots[1].Ksize = "022.0";
        equipment.ElectricalParameterSlots[2].Ksize = "022.0";
    }

    // 【C原典】strchr(kairoar,'P') の 2 文字手前を atoi した径サイズ。P 無しや先頭 2 桁未満は 0。
    private static int ExtractRadius(string circuitDescription)
    {
        int p = circuitDescription.IndexOf('P');
        if (p < 2)
        {
            return 0;
        }

        return EquipmentParameterFormatter.Stoi(circuitDescription.Substring(p - 2, 2), 2);
    }

    // 【C原典】"+(" 以降の ')' で切詰めた文字列を返す(NUL 切詰め相当)。
    private static string TruncateAtParen(string kairoar)
    {
        int plus = kairoar.IndexOf("+(", StringComparison.Ordinal);
        if (plus < 0)
        {
            return kairoar;
        }

        int close = kairoar.IndexOf(')', plus);
        return close < 0 ? kairoar : kairoar[..close];
    }

    private static void SetTypeSlot(ControlEquipmentInfo equipment, string[] dataTypes,
                                    string[] displayTypes, int index, string type)
    {
        displayTypes[index] = type;
        dataTypes[index] = type;
        equipment.DataType[index] = type;
    }

    private static bool Contains(string value, string token) =>
        value.Contains(token, StringComparison.Ordinal);

    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
