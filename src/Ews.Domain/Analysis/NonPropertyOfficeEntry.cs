namespace Ews.Domain.Analysis;

/// <summary>
/// 物件/非物件管理データ識別テーブル(eigyocd.cns)の 1 行。
/// 【C原典】eigyocd.cns(kawamura5/toku/const/sin/eigyocd.cns)のデータ行。
///   先頭カンマ区切りフィールドが非物件コード、6 番目以降(index 5～)が営業所コードの一覧。
///   PropChkHibknNum(Fysk00.c:6130)が営業所コードから非物件コードを逆引きするのに用いる。
/// </summary>
/// <param name="NonPropertyCode">非物件コード。【C原典】hibkn(field0, 先頭2桁)。</param>
/// <param name="OfficeCodes">営業所コード一覧。【C原典】field[5..](空白トリム済)。</param>
public sealed record NonPropertyOfficeEntry(string NonPropertyCode, IReadOnlyList<string> OfficeCodes);
