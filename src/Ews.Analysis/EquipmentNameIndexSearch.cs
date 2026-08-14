using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// 機器マスター品名索引(FYDF817)を品名+データ追番で読み、PT 機器選定用のレコードを取得する。
/// 【C原典】<c>Fysk01_Kikisearch_PT</c>/<c>Fysk01_Kikisearch_PT2</c>(toku/sekkei/src/Fysk01.c:4404/4470)。
/// C 原典は FyIsamOpen/FyIsamStartR による索引読みだが、既存の直近上下位検索と同様に
/// キー順の候補リスト(索引全件)を受け取り前方一致で照合するモデルへ置き換える。
/// </summary>
public static class EquipmentNameIndexSearch
{
    /// <summary>データ有り。【C原典】ret = 7。</summary>
    public const int DataFound = 7;

    /// <summary>データ無し。【C原典】ret = 8。</summary>
    public const int DataNothing = 8;

    /// <summary>
    /// 品名+データ追番で索引を 1 件読む。【C原典】<c>Fysk01_Kikisearch_PT(hinm, dno, hdata)</c>。
    /// </summary>
    /// <param name="productName">品名。【C原典】CHAR *hinm(key.hinmei[25] に転記)。</param>
    /// <param name="dataNo">データ追番。【C原典】CHAR *dno(key.datano[4] に転記)。</param>
    /// <param name="index">機器マスター品名索引 全件(キー順)。【C原典】FNAME_KH の ISAM。</param>
    /// <returns>Status=7(有り)+該当レコード、または Status=8(無し)。</returns>
    public static EquipmentNameIndexSearchResult SearchByDataNo(
        string productName, string dataNo, IReadOnlyList<EquipmentNameIndex> index)
    {
        ArgumentNullException.ThrowIfNull(productName);
        ArgumentNullException.ThrowIfNull(dataNo);
        ArgumentNullException.ThrowIfNull(index);

        foreach (EquipmentNameIndex record in index)
        {
            // 【C原典】key.hinmei[25] + key.datano[4] の完全一致(memcmp 相当)。
            if (FixedEquals(record.ProductName, productName, 25)
                && FixedEquals(record.DataNo, dataNo, 4))
            {
                return new EquipmentNameIndexSearchResult(DataFound, record);
            }
        }

        return new EquipmentNameIndexSearchResult(DataNothing, null);
    }

    /// <summary>
    /// 予約語 PT のレコードを取得する。取得データが PT 以外ならデータ追番を進めて再検索する。
    /// 【C原典】<c>Fysk01_Kikisearch_PT2(hinm, hdata)</c>。
    /// </summary>
    /// <param name="productName">品名。【C原典】CHAR *hinm。</param>
    /// <param name="index">機器マスター品名索引 全件(キー順)。【C原典】FNAME_KH の ISAM。</param>
    /// <returns>Status=7(予約語 PT を取得)+該当レコード、または Status=8(無し)。</returns>
    public static EquipmentNameIndexSearchResult SearchPt(
        string productName, IReadOnlyList<EquipmentNameIndex> index)
    {
        ArgumentNullException.ThrowIfNull(productName);
        ArgumentNullException.ThrowIfNull(index);

        for (int i = 1; ; i++)
        {
            // 【C原典】sprintf(datano, "%04d", i)。
            string dataNo = i.ToString("D4");
            EquipmentNameIndexSearchResult result = SearchByDataNo(productName, dataNo, index);

            if (result.Status != DataFound)
            {
                // 【C原典】7 以外(8 / システムエラー)はそのまま返す。
                return result;
            }

            // 【C原典】strncmp(hdata->pkey.yoyaku, "PT ", 3) == 0 なら確定、以外は追番を進めて再検索。
            if (IsReservedWordPt(result.Record))
            {
                return result;
            }
        }
    }

    /// <summary>予約語が PT(先頭 3 バイト "PT ")か判定する。【C原典】strncmp(yoyaku, "PT ", 3)==0。</summary>
    private static bool IsReservedWordPt(EquipmentNameIndex? record)
        => record is not null && Fit(record.ReservedWord, 3) == "PT ";

    /// <summary>固定幅 <paramref name="width"/> に正規化して等価か判定する(memcmp 相当)。</summary>
    private static bool FixedEquals(string a, string b, int width)
        => Fit(a, width) == Fit(b, width);

    /// <summary>文字列を空白で固定幅に右詰めし、超過分は切り捨てる。</summary>
    private static string Fit(string value, int width)
    {
        string source = value ?? string.Empty;
        return source.Length >= width ? source[..width] : source.PadRight(width);
    }
}

/// <summary>
/// 機器マスター品名索引検索の結果。【C原典】<c>Fysk01_Kikisearch_PT*</c> の戻り値(SHORT)と hdata。
/// </summary>
/// <param name="Status">7(有り)/8(無し)。【C原典】ret。</param>
/// <param name="Record">該当レコード(無しは null)。【C原典】struct FYDF817 *hdata。</param>
public readonly record struct EquipmentNameIndexSearchResult(int Status, EquipmentNameIndex? Record);
