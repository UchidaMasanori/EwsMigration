namespace Ews.Domain.Configuration;

/// <summary>
/// 地区情報定義ファイル interfdt.inf の 1 行(地区コード → 地区グループ)を表す。
/// 【C原典】static <c>interf[TBL_MAX][6][64]</c>(getinterfdt.c)のうち、
/// FyGetFacGrp が参照する [IDX_ZONECD]=地区コード と [IDX_AREAGR]=地区グループ の対。
/// (地区名/サーバーホスト名/地区特性/地区サーバーホスト名は本移植では未使用のため保持しない)
/// </summary>
/// <param name="ZoneCode">地区コード。【C原典】interf[x][IDX_ZONECD]。</param>
/// <param name="FacilityGroup">地区グループ(1?5)。【C原典】atoi(interf[x][IDX_AREAGR])。</param>
public sealed record FacilityAreaEntry(string ZoneCode, int FacilityGroup);
