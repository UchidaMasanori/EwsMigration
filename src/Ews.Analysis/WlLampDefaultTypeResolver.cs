using Ews.Domain.Analysis;
using Ews.Domain.Configuration;
using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// イズミ製 WL ランプ(主回路)のデフォルト機器タイプ・径サイズ設定。
/// 【C原典】PropChgWlLampType(Fysk00.c:5404, 改訂&lt;46&gt;?, LAMP22 有効時)。
///   マルヤス製が廃番→イズミ製に変わった処置。イズミ(IZ)指定または水俣工場(fac_grp=4)の
///   マルヤス(MAN)指定の WL について、回路記述・地区グループ・後続/前段記述・ヒューズ個数・
///   品番(PEKOB)から機器タイプ(RE/TR/WP/LED)・径サイズ・電圧を設定する。
///
/// 移植済み依存を結線: FyGetFacGrp=<see cref="IFacilityAreaResolver"/> /
///   Fysk11_FYDF805_KkGet/_Mae=<see cref="CircuitDescriptionArea"/> /
///   PropSetDefLampType=<see cref="LampDefaultTypeResolver"/> /
///   PropChkHbnPEKOB=<see cref="IPartNumberInfoRepository"/>。
/// </summary>
public sealed class WlLampDefaultTypeResolver
{
    private const int MinamataFacilityGroup = 4;   // 水俣工場

    private readonly IFacilityAreaResolver _facilityAreaResolver;
    private readonly IRuntimeParameterProvider _parameters;
    private readonly CircuitDescriptionArea _circuitDescriptions;
    private readonly IPartNumberInfoRepository _partNumberRepository;

    public WlLampDefaultTypeResolver(IFacilityAreaResolver facilityAreaResolver,
                                     IRuntimeParameterProvider parameters,
                                     CircuitDescriptionArea circuitDescriptions,
                                     IPartNumberInfoRepository partNumberRepository)
    {
        ArgumentNullException.ThrowIfNull(facilityAreaResolver);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(circuitDescriptions);
        ArgumentNullException.ThrowIfNull(partNumberRepository);
        _facilityAreaResolver = facilityAreaResolver;
        _parameters = parameters;
        _circuitDescriptions = circuitDescriptions;
        _partNumberRepository = partNumberRepository;
    }

    /// <summary>WL ランプの既定機器タイプ・径サイズを設定する。【C原典】PropChgWlLampType。</summary>
    /// <param name="lamp">WL の主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位。【C原典】mcod。</param>
    /// <param name="dataTypes">機器タイプ(7×7, 破壊的に更新)。【C原典】dtype。</param>
    /// <param name="displayTypes">表示用機器タイプ(7×7, 破壊的に更新)。【C原典】wtype。</param>
    /// <param name="sep">電気パラメータ数値版(3, 破壊的に更新)。【C原典】sep。</param>
    /// <param name="records">主回路レコード列。【C原典】f800。</param>
    /// <param name="requestDetailNumber">依頼明細番号(PEKOB 品番判定キー)。【C原典】bknm 由来の iraimei。</param>
    public void Resolve(MainCircuitResult lamp, string[] makerCodes, string[] dataTypes,
                        string[] displayTypes, NumericElectricalParameters[] sep,
                        IReadOnlyList<MainCircuitResult> records, string requestDetailNumber)
    {
        ArgumentNullException.ThrowIfNull(lamp);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(displayTypes);
        ArgumentNullException.ThrowIfNull(sep);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(requestDetailNumber);

        int facilityGroup = _facilityAreaResolver.GetFacilityGroup(_parameters.ZoneCode);

        if (!Matches(lamp.Data.ReservedWord, "WL ", 3))
        {
            return;
        }

        string kairoar = _circuitDescriptions.GetDescriptionAt(
            lamp.Data.DescriptionRow, lamp.Data.DescriptionColumn);
        if (kairoar.Length == 0)
        {
            return;         // 取得 NG
        }

        string[] defType = ["", "", "", "", "", "", ""];

        if (Matches(makerCodes[0], "IZ ", 3) ||
            (facilityGroup == MinamataFacilityGroup && Matches(makerCodes[0], "MAN", 3)))
        {
            // PM 行・B 行のみ処理。
            if (!Matches(lamp.Data.LineTypeCode, "PM ", 3) &&
                !Matches(lamp.Data.LineTypeCode, "B  ", 3))
            {
                return;
            }

            if (!Contains(kairoar, "+("))       // タイプ指定入力なし
            {
                if (facilityGroup == MinamataFacilityGroup)     // 水俣工場
                {
                    if (Matches(lamp.Data.DataType[1], "AN ", 3))
                    {
                        defType[1] = "RE     ";
                    }
                }
                else
                {
                    ApplyTypeAndRadius(0, lamp, records, kairoar, defType, sep, requestDetailNumber);
                }
            }
            else                                // タイプ指定あり
            {
                if (facilityGroup == MinamataFacilityGroup)     // 水俣工場
                {
                    // 【C原典】"+(" 以降の ')' で切詰めた範囲に AN/SQ/P1/P2 が無ければタイプ据置判定。
                    string inside = TruncateAtParen(kairoar);
                    if (!Contains(inside, "AN") && !Contains(inside, "SQ") &&
                        !Contains(inside, "P1") && !Contains(inside, "P2"))
                    {
                        if (Matches(lamp.Data.DataType[1], "AN ", 3))
                        {
                            defType[1] = "RE     ";
                        }
                    }
                }
                else
                {
                    ApplyTypeAndRadius(1, lamp, records, kairoar, defType, sep, requestDetailNumber);
                }
            }

            if (defType[0].Length > 0)
            {
                SetTypeSlot(lamp, dataTypes, displayTypes, 0, defType[0]);
            }
            if (defType[4].Length > 0)
            {
                SetTypeSlot(lamp, dataTypes, displayTypes, 4, defType[4]);
            }
            if (defType[1].Length > 0)
            {
                SetTypeSlot(lamp, dataTypes, displayTypes, 1, defType[1]);
            }
        }

        // LED タイプのデフォルト設定(改訂<65>)。現行値 "" を渡し LED または未変更(空)を得る。
        string ledType = LampDefaultTypeResolver.ResolveDefaultType(kairoar, string.Empty);
        if (ledType.Length > 0)
        {
            SetTypeSlot(lamp, dataTypes, displayTypes, 3, ledType);
        }
    }

    /// <summary>
    /// WL ランプのデフォルトタイプ(TR)・径サイズ(22/25)・電圧を設定する。
    /// 【C原典】PropChgWlTypeAndKei(Fysk00.c, 改訂&lt;80&gt;)。
    /// </summary>
    private void ApplyTypeAndRadius(int typeSpecified, MainCircuitResult lamp,
        IReadOnlyList<MainCircuitResult> records, string kairoar, string[] defType,
        NumericElectricalParameters[] sep, string requestDetailNumber)
    {
        int radius = ExtractRadius(kairoar);
        if (radius != 0 && radius != 22)
        {
            return;
        }

        int fuseCount = 0;
        foreach (MainCircuitResult record in records)
        {
            if (Matches(record.SequenceNumber, lamp.SequenceNumber, 3))
            {
                fuseCount = Stoi(record.Data.ElectricalParameterSlots[0].Qty.ToString(), 1);
                break;
            }
        }

        string preceding = _circuitDescriptions.GetPrecedingDescription(
            lamp.Data.DescriptionRow, lamp.Data.DescriptionColumn);

        if (radius == 0 && HasPekob(requestDetailNumber))
        {
            // PEKOB は径サイズ 25mm。
            sep[0].Ksize = 25.0;
            sep[2].Ksize = 25.0;
            lamp.Data.ElectricalParameterSlots[0].Ksize = "025.0";
            lamp.Data.ElectricalParameterSlots[2].Ksize = "025.0";
        }
        else if ((preceding.Contains('F') || Contains(preceding, "PLTR")) && Contains(preceding, "+("))
        {
            // TR に変更しない。
        }
        else if (fuseCount > 1)
        {
            // ヒューズが 2 個以上なので TR に変更しない。
        }
        else if (preceding.Contains('F'))
        {
            if (typeSpecified == 0)
            {
                defType[0] = "TR     ";
            }
            else if (!Contains(kairoar, "DI"))
            {
                // +(DI) がなければデフォルトタイプ変更。
                defType[0] = "TR     ";
            }

            // タイプ1 が AN のときタイプ4 が WP の機器は存在しない(改訂<135>)。
            if (!Matches(lamp.Data.DataType[1], "AN ", 3))
            {
                defType[4] = "WP     ";
            }

            sep[0].Ksize = 22.0;
            sep[2].Ksize = 22.0;

            MainCircuitResult? powerSource = FindPowerSource(lamp, records);
            if (powerSource != null &&
                (powerSource.Data.CircuitPhaseCount == '3' ||
                 (powerSource.Data.CircuitWireType == '2' &&
                  Matches(powerSource.Data.CircuitVoltage[0], "210", 3))))
            {
                sep[0].V2[0] = 220.0;
                sep[2].V2[0] = 220.0;
            }
            else
            {
                sep[0].V2[0] = 110.0;
                sep[2].V2[0] = 110.0;
            }

            lamp.Data.ElectricalParameterSlots[0].Ksize = "022.0";
            lamp.Data.ElectricalParameterSlots[2].Ksize = "022.0";
        }
    }

    /// <summary>依頼明細番号の品番情報に "PEKOB" が含まれるか。【C原典】PropChkHbnPEKOB(0:あり)。</summary>
    private bool HasPekob(string requestDetailNumber)
    {
        PartNumberInfo? partNumber = _partNumberRepository.Find(requestDetailNumber);
        return partNumber != null && Contains(partNumber.InputPartNumber, "PEKOB");
    }

    // 【C原典】親追番(oyatno)を辿り予約語 "P" の電源レコードを返す。無ければ null。
    private static MainCircuitResult? FindPowerSource(MainCircuitResult lamp,
                                                      IReadOnlyList<MainCircuitResult> records)
    {
        string parentNo = lamp.Data.ParentSequenceNumber;
        while (true)
        {
            MainCircuitResult? parent = null;
            foreach (MainCircuitResult record in records)
            {
                if (Matches(record.SequenceNumber, parentNo, 3))
                {
                    parent = record;
                    break;
                }
            }

            if (parent == null)
            {
                return null;
            }
            if (Matches(parent.Data.ReservedWord, "P  ", 3))
            {
                return parent;
            }
            parentNo = parent.Data.ParentSequenceNumber;
        }
    }

    // 【C原典】strchr(kairoar,'P') の 2 文字手前を atoi した径サイズ。P 無しや先頭 2 桁未満は 0。
    private static int ExtractRadius(string circuitDescription)
    {
        int p = circuitDescription.IndexOf('P');
        if (p < 2)
        {
            return 0;
        }

        return Stoi(circuitDescription.Substring(p - 2, 2), 2);
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

    private static void SetTypeSlot(MainCircuitResult lamp, string[] dataTypes,
                                    string[] displayTypes, int index, string type)
    {
        displayTypes[index] = type;
        dataTypes[index] = type;
        lamp.Data.DataType[index] = type;
    }

    private static bool Contains(string value, string token) =>
        value.Contains(token, StringComparison.Ordinal);

    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;

    private static int Stoi(string value, int size) =>
        EquipmentParameterFormatter.Stoi(value, size);
}
