using Ews.Domain.Analysis;
using Ews.Domain.Configuration;
using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// ヒューズ(F)のデフォルト機器タイプ設定。
/// 【C原典】PropChgFuseType_SY(Fysk00.c:6350, 改訂&lt;73&gt;?, LAMP22 有効時)。
///   回路内容記述・地区グループ・ヒューズ個数・品番情報(hbninf)・後続ランプの径などから、
///   ヒューズの機器タイプを "GT"(=WL ユニット付ヒューズ)へ、メーカーを FT へ調整し、
///   併せて子 WL の回路電圧を変更する(<see cref="WlCircuitVoltageAdjuster"/>)。
///
/// 移植済みの依存を結線する統合関数:
///   FyGetFacGrp=<see cref="IFacilityAreaResolver"/> /
///   Fysk11_FYDF805_KkGet/_Ato=<see cref="CircuitDescriptionArea"/> /
///   FyCpHbHbnInfFileR=<see cref="IPartNumberInfoRepository"/> /
///   PropAdjustMakerCode=<see cref="MakerCodePriorityAdjuster"/> /
///   PropChangeWlKpav=<see cref="WlCircuitVoltageAdjuster"/>。
/// </summary>
public sealed class FuseDefaultTypeResolver
{
    private const string GtType = "GT     ";           // 7 桁
    private const int SapporoFacilityGroup = 1;        // 札幌工場
    private const int MinamataFacilityGroup = 4;       // 水俣工場

    private readonly IFacilityAreaResolver _facilityAreaResolver;
    private readonly IRuntimeParameterProvider _parameters;
    private readonly CircuitDescriptionArea _circuitDescriptions;
    private readonly IPartNumberInfoRepository _partNumberRepository;

    public FuseDefaultTypeResolver(IFacilityAreaResolver facilityAreaResolver,
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

    /// <summary>ヒューズのデフォルト機器タイプ・メーカーを調整する。</summary>
    /// <param name="fuse">ヒューズの主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位(4×3, 破壊的に更新)。【C原典】mcod。</param>
    /// <param name="dataTypes">機器タイプ(7×7, 破壊的に更新)。【C原典】dtype。</param>
    /// <param name="displayTypes">表示用機器タイプ(7×7, 破壊的に更新)。【C原典】wtype。</param>
    /// <param name="records">主回路レコード列。【C原典】f800。</param>
    /// <param name="specKind">仕様(0:特注/ブロック 1:コンポ)。【C原典】cpf。</param>
    /// <param name="requestDetailNumber">依頼明細番号(コンポ盤の品番情報取得キー)。【C原典】iraimei。</param>
    public void Resolve(MainCircuitResult fuse, string[] makerCodes, string[] dataTypes,
                        string[] displayTypes, IReadOnlyList<MainCircuitResult> records,
                        int specKind, string requestDetailNumber)
    {
        ArgumentNullException.ThrowIfNull(fuse);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(displayTypes);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(requestDetailNumber);

        int facilityGroup = _facilityAreaResolver.GetFacilityGroup(_parameters.ZoneCode);

        // ヒューズの個数取得(該当 datano の ep[0].epaqty 先頭 1 文字)。
        int fuseCount = 0;
        foreach (MainCircuitResult record in records)
        {
            if (Matches(record.SequenceNumber, fuse.SequenceNumber, 3))
            {
                fuseCount = Stoi(record.Data.ElectricalParameterSlots[0].Qty.ToString(), 1);
                break;
            }
        }

        if (!Matches(fuse.Data.ReservedWord, "F ", 2))
        {
            return;
        }

        // メーカコード保存(PropAdjustMakerCode 用)。
        string[] makerCodesOriginal = (string[])makerCodes.Clone();

        string kairoar = _circuitDescriptions.GetDescriptionAt(
            fuse.Data.DescriptionRow, fuse.Data.DescriptionColumn);
        if (kairoar.Length == 0)
        {
            return;         // 取得 NG
        }

        // PM 行・B 行以外は、ここでメーカー調整して終了。
        if (!Matches(fuse.Data.LineTypeCode, "PM ", 3) &&
            !Matches(fuse.Data.LineTypeCode, "B  ", 3))
        {
            if (!Contains(kairoar, "MK="))              // メーカ指定なし
            {
                if (!Contains(kairoar, "ST"))           // タイプ ST 指定なし
                {
                    SetMakerCodes(makerCodes, "FT ", "F  ", "K  ", "OT ");
                    ApplyMakerCodeAdjustment(makerCodes, makerCodesOriginal);
                }
            }
            return;
        }

        if (!Matches(makerCodes[0], "K  ", 3))
        {
            return;
        }

        if (fuseCount > 1)
        {
            // ヒューズが 2 個以上なので TR に変更しない。
            if (Matches(dataTypes[0], "GT ", 3))
            {
                makerCodes[0] = "FT ";
            }
            return;
        }

        string following = _circuitDescriptions.GetFollowingDescription(
            fuse.Data.DescriptionRow, fuse.Data.DescriptionColumn);
        if (following.Length == 0)
        {
            // F 以降の記述なし。
            if (Matches(dataTypes[0], "GT ", 3))
            {
                makerCodes[0] = "FT ";
            }
            return;
        }

        if (!IsLamp(following))
        {
            // F 以降にランプ以外の記述あり。
            makerCodes[0] = "FT ";
            return;
        }

        string inputPartNumber = string.Empty;
        if (specKind == 1)      // コンポ盤
        {
            PartNumberInfo? partNumber = _partNumberRepository.Find(requestDetailNumber);
            if (partNumber != null)
            {
                if (Contains(partNumber.InputPartNumber, "GWL") ||
                    Contains(partNumber.InputPartNumber, "GJWL"))
                {
                    // コンポ盤で WL ユニット指定がある。
                    SetType0(fuse, dataTypes, displayTypes, GtType);
                    if (!Contains(kairoar, "MK="))
                    {
                        makerCodes[0] = "FT ";
                    }
                }
                inputPartNumber = partNumber.InputPartNumber;
            }
        }

        if (facilityGroup == SapporoFacilityGroup || facilityGroup == MinamataFacilityGroup)
        {
            if (specKind == 0)  // 特注、ブロックコンポ
            {
                SetType0(fuse, dataTypes, displayTypes, GtType);
                if (!Contains(kairoar, "MK="))
                {
                    makerCodes[0] = "FT ";
                }
            }
            WlCircuitVoltageAdjuster.Adjust(makerCodes[0], fuse, records);
            return;         // 札幌工場、水俣工場は以降対象外
        }

        // F 以降のランプ確認。
        string defType = string.Empty;
        if (!Contains(kairoar, "+("))       // ヒューズのタイプ指定なし
        {
            // ランプタイプ指定なし(f_ato に "+(" と "DI" が両方揃っている場合のみ除外)。
            if (!(Contains(following, "+(") && Contains(following, "DI")))
            {
                int radius = ExtractRadius(following);
                if (radius == 0 || radius == 22)
                {
                    if (radius == 0 && Contains(inputPartNumber, "PEKOB"))
                    {
                        // PEKOB はヒューズに TR をつける(def_type 設定しない)。
                    }
                    else
                    {
                        defType = GtType;
                    }
                }
                if (specKind == 0)  // 特注、ブロックコンポ
                {
                    defType = GtType;
                    if (!Contains(kairoar, "MK="))
                    {
                        makerCodes[0] = "FT ";
                    }
                }
            }
        }

        if (defType.Length > 0)
        {
            SetType0(fuse, dataTypes, displayTypes, defType);
            WlCircuitVoltageAdjuster.Adjust(makerCodes[0], fuse, records);
        }
    }

    // 【C原典】strchr(f_ato,'P') の 2 文字手前を atoi した径サイズ(P 前が数字なら径入力)。
    private static int ExtractRadius(string following)
    {
        int p = following.IndexOf('P');
        if (p < 2)
        {
            return 0;
        }

        return Stoi(following.Substring(p - 2, 2), 2);
    }

    // 【C原典】strncmp(f_ato, "WL"/"RL"/"GL"/"OL"/"BL", 2)。
    private static bool IsLamp(string following) =>
        Matches(following, "WL", 2) || Matches(following, "RL", 2) ||
        Matches(following, "GL", 2) || Matches(following, "OL", 2) ||
        Matches(following, "BL", 2);

    private static void SetMakerCodes(string[] makerCodes, string s0, string s1, string s2, string s3)
    {
        makerCodes[0] = s0;
        makerCodes[1] = s1;
        makerCodes[2] = s2;
        makerCodes[3] = s3;
    }

    private static void ApplyMakerCodeAdjustment(string[] makerCodes, string[] original)
    {
        IReadOnlyList<string> adjusted = MakerCodePriorityAdjuster.RemoveUnlistedCodes(makerCodes, original);
        for (int i = 0; i < makerCodes.Length && i < adjusted.Count; i++)
        {
            makerCodes[i] = adjusted[i];
        }
    }

    private static void SetType0(MainCircuitResult fuse, string[] dataTypes, string[] displayTypes, string type)
    {
        displayTypes[0] = type;
        dataTypes[0] = type;
        fuse.Data.DataType[0] = type;
    }

    private static bool Contains(string value, string token) =>
        value.Contains(token, StringComparison.Ordinal);

    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;

    private static int Stoi(string value, int size) =>
        EquipmentParameterFormatter.Stoi(value, size);
}
