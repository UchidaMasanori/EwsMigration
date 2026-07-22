using System.Data;
using Ews.Domain.Masters;
using Microsoft.Data.SqlClient;

namespace Ews.Data.Seeding;

/// <summary>
/// 機器マスター(FYDM805)の固定長エクスポートを読み込み、SQL Server の
/// EquipmentMaster テーブルへシードする。
///
/// 【入力】hostdt/FYDM805.data
///   - 旧 EWS-ISAM ファイル FYDM805.af1 を固定長テキストへエクスポートしたもの。
///   - 1 レコード = struct FYDM805(【C原典】fydm805.h, ﾚｺｰﾄﾞ長 579)を Shift-JIS で
///     出力し、行末を LF(0x0A)で区切る。各フィールドは CHAR[n] の固定幅。
///   - フィールド抽出は <see cref="EquipmentMaster.FromFixedRecord"/>(バイトオフセット)
///     に委譲する。LF(0x0A)は Shift-JIS の第2バイトに現れないため、バイト単位での
///     行分割は安全。
/// </summary>
public sealed class EquipmentMasterLoader
{
    private readonly SqlServer.SqlConnectionFactory _factory;

    public EquipmentMasterLoader(SqlServer.SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// 型付けするフィールドをすべて読むために必要な最小バイト長。
    /// 【C原典】pstring[64] の終端 = offset 180 + 64 = 244。これに満たない
    /// 末尾の断片レコード(エクスポート末尾に現れる不完全行)は読み飛ばす。
    /// </summary>
    private const int MinRecordBytes = 244;

    /// <summary>
    /// FYDM805.data を解析して <see cref="EquipmentMaster"/> 一覧を返す。
    /// </summary>
    public static IReadOnlyList<EquipmentMaster> ParseEquipmentMaster(string dataPath)
    {
        byte[] all = File.ReadAllBytes(dataPath);
        var list = new List<EquipmentMaster>();

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
                list.Add(EquipmentMaster.FromFixedRecord(all.AsSpan(start, length)));
            }

            start = i + 1;
        }

        return list;
    }

    /// <summary>
    /// FYDM805.data を SQL Server の EquipmentMaster テーブルへ投入する(全置換)。
    /// 件数が多い(約 8,300 件)ため <see cref="SqlBulkCopy"/> で一括投入する。
    /// </summary>
    /// <returns>投入件数。</returns>
    public int SeedEquipmentMaster(string dataPath)
    {
        IReadOnlyList<EquipmentMaster> rows = ParseEquipmentMaster(dataPath);
        DataTable table = BuildDataTable(rows);

        using SqlConnection connection = _factory.CreateOpen();
        using SqlTransaction transaction = connection.BeginTransaction();

        using (var delete = new SqlCommand("DELETE FROM EquipmentMaster", connection, transaction))
        {
            delete.ExecuteNonQuery();
        }

        using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
        {
            DestinationTableName = "EquipmentMaster",
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

    private static DataTable BuildDataTable(IReadOnlyList<EquipmentMaster> rows)
    {
        var table = new DataTable();
        table.Columns.Add("ReservedWord", typeof(string));
        table.Columns.Add("MakerCode", typeof(string));
        table.Columns.Add("ParameterType", typeof(string));
        table.Columns.Add("RatingKey", typeof(string));
        table.Columns.Add("PartNumber", typeof(string));
        table.Columns.Add("PartName", typeof(string));
        table.Columns.Add("ElectricalParameters", typeof(string));

        foreach (EquipmentMaster row in rows)
        {
            table.Rows.Add(
                row.ReservedWord,
                row.MakerCode,
                row.ParameterType,
                row.RatingKey,
                row.PartNumber.Length == 0 ? DBNull.Value : row.PartNumber,
                row.PartName.Length == 0 ? DBNull.Value : row.PartName,
                row.ElectricalParameters.Length == 0 ? DBNull.Value : row.ElectricalParameters);
        }

        return table;
    }
}
