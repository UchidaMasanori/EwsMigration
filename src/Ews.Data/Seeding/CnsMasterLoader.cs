using Dapper;
using Ews.Domain.Common;
using Ews.Domain.Masters;

namespace Ews.Data.Seeding;

/// <summary>
/// .cns テキストマスタ(Shift-JIS)を読み込み、SQL Server の正規化テーブルへシードする。
///
/// 【C原典】
///   - 部門マスタ: bumon.*.cns
///   - 解析方式 : fopen("rt") + fgets + strchr によるカンマ分割
///     (例 toku/usr/.../InstructionsCns.c, toku/master/fydm830/src/Fydm830Info.c)
///
/// 本パイロットでは部門(bumon)マスタを代表例として実装する。営業所(eigyo*)・
/// 電力管内(denkan)等も同様のパターンで追加可能。
/// </summary>
public sealed class CnsMasterLoader
{
    private readonly SqlServer.SqlConnectionFactory _factory;

    public CnsMasterLoader(SqlServer.SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// bumon.*.cns をパースして <see cref="DepartmentMaster"/> 一覧を返す。
    /// 【C原典】bumon.cns(コード, 名称, 電話番号,)。
    /// </summary>
    public static IReadOnlyList<DepartmentMaster> ParseDepartmentMaster(string cnsPath)
    {
        var list = new List<DepartmentMaster>();
        foreach (string rawLine in File.ReadLines(cnsPath, FixedFieldCodec.ShiftJis))
        {
            string line = rawLine.TrimEnd('\r', '\n');
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            string[] fields = line.Split(',');
            if (fields.Length < 2)
            {
                continue;
            }

            list.Add(new DepartmentMaster(
                DepartmentCode: fields[0].Trim(),
                DepartmentName: fields[1].Trim(' ', '\u3000'),       // 全角空白パディング除去
                PhoneNumber: fields.Length > 2 ? fields[2].Trim() : string.Empty));
        }

        return list;
    }

    /// <summary>
    /// 部門マスタを SQL Server の DepartmentMaster テーブルへ投入する(全置換)。
    /// </summary>
    /// <returns>投入件数。</returns>
    public int SeedDepartmentMaster(string cnsPath)
    {
        IReadOnlyList<DepartmentMaster> rows = ParseDepartmentMaster(cnsPath);

        using var connection = _factory.CreateOpen();
        using var transaction = connection.BeginTransaction();

        connection.Execute("DELETE FROM DepartmentMaster", transaction: transaction);
        connection.Execute(
            """
            INSERT INTO DepartmentMaster (DepartmentCode, DepartmentName, PhoneNumber)
            VALUES (@DepartmentCode, @DepartmentName, @PhoneNumber)
            """,
            rows,
            transaction: transaction);

        transaction.Commit();
        return rows.Count;
    }
}
