using System.Data;
using Ews.Domain.Masters;
using Microsoft.Data.SqlClient;

namespace Ews.Data.Seeding;

/// <summary>
/// 物件情報(FYDF801)の固定長エクスポートを読み込み、SQL Server の
/// ProjectInformation テーブルへシードする。
///
/// 【入力】master/FYDF801.data
///   - 旧 EWS-ISAM 物件情報ファイル FYDF801 を固定長テキストへエクスポートしたもの。
///   - 1 レコード = struct FYDF801(【C原典】fydf801.h, ﾚｺｰﾄﾞ長 1200)を Shift-JIS で
///     出力し、行末を LF(0x0A)で区切る(1201 バイト/行)。各フィールドは CHAR[n] の固定幅。
///   - 明細番号(meisaino)ブランクのレコードが「物件共通情報」で、'01'～'99' は
///     「盤明細情報」(union redefines)。本ローダは両者を同一構造として読み込む
///     (盤明細レコードの物件共通情報オフセットは union のため異なる意味を持つが、
///     キー(依頼番号+明細番号)は共通)。
///   - フィールド抽出は <see cref="ProjectInformation.FromFixedRecord"/>(バイトオフセット)
///     に委譲する。LF(0x0A)は Shift-JIS の第2バイトに現れないため、バイト単位での
///     行分割は安全。
/// </summary>
public sealed class ProjectInformationLoader
{
    private readonly SqlServer.SqlConnectionFactory _factory;

    public ProjectInformationLoader(SqlServer.SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// 型付けするフィールドをすべて読むために必要な最小バイト長。
    /// 【C原典】周波数区分 hzkbn の終端 = offset 199 + 1 = 200。これに満たない
    /// 末尾の断片レコード(エクスポート末尾に現れる不完全行)は読み飛ばす。
    /// </summary>
    private const int MinRecordBytes = 200;

    /// <summary>
    /// FYDF801.data を解析して <see cref="ProjectInformation"/> 一覧を返す。
    /// 各レコードは 1200 バイト固定長・LF(0x0A)区切り。
    /// </summary>
    public static IReadOnlyList<ProjectInformation> ParseProjectInformation(string dataPath)
    {
        byte[] all = File.ReadAllBytes(dataPath);
        var list = new List<ProjectInformation>();

        int start = 0;
        for (int i = 0; i <= all.Length; i++)
        {
            if (i != all.Length && all[i] != (byte)'\n')
            {
                continue;
            }

            // レコード = [start, i)。末尾 CR(0x0D)があれば除外する。
            int end = i;
            if (end > start && all[end - 1] == (byte)'\r')
            {
                end--;
            }

            int length = end - start;
            if (length >= MinRecordBytes)
            {
                list.Add(ProjectInformation.FromFixedRecord(all.AsSpan(start, length)));
            }

            start = i + 1;
        }

        return list;
    }

    /// <summary>
    /// FYDF801.data を SQL Server の ProjectInformation テーブルへ投入する(全置換)。
    /// 件数が多い(約 33,000 件)ため <see cref="SqlBulkCopy"/> で一括投入する。
    /// </summary>
    /// <returns>投入件数。</returns>
    public int SeedProjectInformation(string dataPath)
    {
        IReadOnlyList<ProjectInformation> rows = ParseProjectInformation(dataPath);
        DataTable table = BuildDataTable(rows);

        using SqlConnection connection = _factory.CreateOpen();
        using SqlTransaction transaction = connection.BeginTransaction();

        using (var delete = new SqlCommand("DELETE FROM ProjectInformation", connection, transaction))
        {
            delete.ExecuteNonQuery();
        }

        using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
        {
            DestinationTableName = "ProjectInformation",
        })
        {
            foreach (DataColumn column in table.Columns)
            {
                bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            bulk.WriteToServer(table);
        }

        transaction.Commit();
        return rows.Count;
    }

    private static DataTable BuildDataTable(IReadOnlyList<ProjectInformation> rows)
    {
        var table = new DataTable();
        table.Columns.Add("RequestNumber", typeof(string));
        table.Columns.Add("DetailNumber", typeof(string));
        table.Columns.Add("DrawingNumberUpper", typeof(string));
        table.Columns.Add("DrawingNumberLower", typeof(string));
        table.Columns.Add("SalesOfficeName", typeof(string));
        table.Columns.Add("StaffName", typeof(string));
        table.Columns.Add("ProjectName1", typeof(string));
        table.Columns.Add("ProjectName2", typeof(string));
        table.Columns.Add("ManufacturingSpecKind", typeof(string));
        table.Columns.Add("SpecificationName", typeof(string));
        table.Columns.Add("DrawingKind", typeof(string));
        table.Columns.Add("DrawingRank", typeof(string));
        table.Columns.Add("FrequencyKind", typeof(string));

        static object Nz(string s) => s.Length == 0 ? DBNull.Value : s;

        foreach (ProjectInformation row in rows)
        {
            table.Rows.Add(
                row.RequestNumber,
                row.DetailNumber,
                Nz(row.DrawingNumberUpper),
                Nz(row.DrawingNumberLower),
                Nz(row.SalesOfficeName),
                Nz(row.StaffName),
                Nz(row.ProjectName1),
                Nz(row.ProjectName2),
                Nz(row.ManufacturingSpecKind),
                Nz(row.SpecificationName),
                Nz(row.DrawingKind),
                Nz(row.DrawingRank),
                Nz(row.FrequencyKind));
        }

        return table;
    }
}
