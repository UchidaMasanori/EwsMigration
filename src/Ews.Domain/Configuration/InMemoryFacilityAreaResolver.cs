namespace Ews.Domain.Configuration;

/// <summary>
/// メモリ上の地区情報テーブルを値源とする <see cref="IFacilityAreaResolver"/> 実装。
/// 【C原典】FyGetInterTbl でロード済みの static テーブル interf[][] を引く FyGetFacGrp。
/// </summary>
public sealed class InMemoryFacilityAreaResolver : IFacilityAreaResolver
{
    /// <summary>地区定義が無い/情報無しのときの既定地区グループ。【C原典】本社地区。</summary>
    public const int HomeAreaGroup = 5;

    // 【C原典】strcmp( interf[i][IDX_ZONECD], zonecd ) による完全一致検索 → Ordinal 辞書。
    // 同一地区コードが複数あれば C は最初の一致を採るため、先勝ちで格納する。
    private readonly Dictionary<string, int> _facilityGroups;

    public InMemoryFacilityAreaResolver(IEnumerable<FacilityAreaEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _facilityGroups = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (FacilityAreaEntry entry in entries)
        {
            _facilityGroups.TryAdd(entry.ZoneCode, entry.FacilityGroup);
        }
    }

    public int GetFacilityGroup(string zoneCode)
    {
        ArgumentNullException.ThrowIfNull(zoneCode);
        return _facilityGroups.TryGetValue(zoneCode, out int group) ? group : HomeAreaGroup;
    }
}
