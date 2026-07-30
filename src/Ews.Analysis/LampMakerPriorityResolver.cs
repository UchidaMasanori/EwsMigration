using Ews.Domain.Analysis;
using Ews.Domain.Configuration;

namespace Ews.Analysis;

/// <summary>
/// ランプ類の優先メーカー変更。
/// 【C原典】PropChgWlLampMaker(主回路)/PropChgSeigyoLampMaker(制御回路)(Fysk00.c, 改訂&lt;88&gt;?)。
///   メーカー未指定のランプについて、地区グループと予約語からメーカーコード選定順位を設定する。
///   末尾で sel_LAMP.cns(<see cref="LampMakerEntry"/>)の一致行があれば上書きする(=PropCnsLampRead 改訂&lt;142&gt;)。
///
/// 移植済み依存を結線: FyGetFacGrp=<see cref="IFacilityAreaResolver"/>。
/// sel_LAMP.cns はローダー(LampMakerTableLoader)で読み込みコンストラクタへ注入する。
/// </summary>
public sealed class LampMakerPriorityResolver
{
    private const int MinamataFacilityGroup = 4;   // 水俣工場

    private readonly IFacilityAreaResolver _facilityAreaResolver;
    private readonly IRuntimeParameterProvider _parameters;
    private readonly IReadOnlyList<LampMakerEntry> _table;

    public LampMakerPriorityResolver(IFacilityAreaResolver facilityAreaResolver,
                                     IRuntimeParameterProvider parameters,
                                     IReadOnlyList<LampMakerEntry> table)
    {
        ArgumentNullException.ThrowIfNull(facilityAreaResolver);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(table);
        _facilityAreaResolver = facilityAreaResolver;
        _parameters = parameters;
        _table = table;
    }

    /// <summary>主回路ランプ(WL)の優先メーカーを変更する。【C原典】PropChgWlLampMaker。</summary>
    /// <param name="lamp">主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位(4×3, 破壊的に更新)。【C原典】mcod。</param>
    public void Resolve(MainCircuitResult lamp, string[] makerCodes)
    {
        ArgumentNullException.ThrowIfNull(lamp);
        ArgumentNullException.ThrowIfNull(makerCodes);

        if (IsMakerSpecified(lamp.Data.AttachedParameter.MakerCode))
        {
            return;         // メーカー指定あり
        }

        int facilityGroup = _facilityAreaResolver.GetFacilityGroup(_parameters.ZoneCode);
        string reservedWord = lamp.Data.ReservedWord;

        if (facilityGroup == MinamataFacilityGroup && Matches(reservedWord, "WL ", 3))
        {
            SetMakers(makerCodes, "MAN", "MA ", "IZ ", "   ");   // 改訂<125>
            return;
        }

        if (facilityGroup == MinamataFacilityGroup)
        {
            return;         // 水俣工場は対象外
        }

        if (Matches(reservedWord, "WL ", 3))
        {
            SetMakers(makerCodes, "IZ ", "MAN", "MA ", "   ");
        }

        ApplyFromTable(facilityGroup, reservedWord, makerCodes);
    }

    /// <summary>制御ランプ(RL/GL/OL/BL)の優先メーカーを変更する。【C原典】PropChgSeigyoLampMaker。</summary>
    /// <param name="equipment">制御機器情報。【C原典】gk-&gt;u.k(struct kikijg)。</param>
    /// <param name="makerCodes">メーカーコード選定順位(4×3, 破壊的に更新)。【C原典】mcod。</param>
    public void ResolveControl(ControlEquipmentInfo equipment, string[] makerCodes)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(makerCodes);

        if (IsMakerSpecified(equipment.MakerCode))
        {
            return;         // メーカー指定あり
        }

        int facilityGroup = _facilityAreaResolver.GetFacilityGroup(_parameters.ZoneCode);
        string reservedWord = equipment.ReservedWord;

        if (facilityGroup == MinamataFacilityGroup)
        {
            // WL 以外(RL/GL/OL/BL)はマルヤス優先(改訂<95>)。
            if (IsControlLamp(reservedWord))
            {
                SetMakers(makerCodes, "MAN", "MA ", "IZ ", "   ");
            }
            return;         // 水俣工場は対象外
        }

        if (IsControlLamp(reservedWord))
        {
            SetMakers(makerCodes, "IZ ", "MAN", "MA ", "   ");
        }

        ApplyFromTable(facilityGroup, reservedWord, makerCodes);
    }

    // 【C原典】PropCnsLampRead: 工場コードと予約語が一致する行の mkcd1?4 で上書き。
    private void ApplyFromTable(int facilityGroup, string reservedWord, string[] makerCodes)
    {
        foreach (LampMakerEntry entry in _table)
        {
            if (facilityGroup == entry.FacilityGroup &&
                Matches(reservedWord, entry.ReservedWord, entry.ReservedWord.Length))
            {
                for (int i = 0; i < 4 && i < entry.MakerCodes.Length; i++)
                {
                    makerCodes[i] = entry.MakerCodes[i].PadRight(3)[..3];
                }
                break;
            }
        }
    }

    private static bool IsControlLamp(string reservedWord) =>
        Matches(reservedWord, "RL ", 3) || Matches(reservedWord, "GL ", 3) ||
        Matches(reservedWord, "OL ", 3) || Matches(reservedWord, "BL ", 3);

    // 【C原典】fpamk[0] != ' ' でメーカー指定あり(未指定は空 or 空白)。
    private static bool IsMakerSpecified(string makerCode) =>
        makerCode.Length > 0 && makerCode[0] != ' ';

    private static void SetMakers(string[] makerCodes, string m0, string m1, string m2, string m3)
    {
        makerCodes[0] = m0;
        makerCodes[1] = m1;
        makerCodes[2] = m2;
        makerCodes[3] = m3;
    }

    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
