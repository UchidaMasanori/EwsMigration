namespace Ews.Domain.Configuration;

/// <summary>
/// 地区コード(ZONECD)から地区グループ(工場区分)を求める抽象。
/// 【C原典】FyGetFacGrp(getinterfdt.c:168)。地区情報定義ファイル interfdt.inf を引き、
/// 該当地区コードの地区グループを返す。定義が無い/情報無しの場合は本社地区(5)を返す。
///
/// 地区グループ: 1=札幌 / 2=つくば・相模 / 3=相模原 / 4=水俣 / 5=本社地区(図面センター・
/// 暁第一工場・SIS 名古屋・情報システム・東京支店 等)。
/// </summary>
public interface IFacilityAreaResolver
{
    /// <summary>指定地区コードの地区グループを返す。未定義は本社地区(5)。【C原典】FyGetFacGrp(zonecd)。</summary>
    int GetFacilityGroup(string zoneCode);
}
