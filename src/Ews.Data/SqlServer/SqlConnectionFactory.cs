using Microsoft.Data.SqlClient;

namespace Ews.Data.SqlServer;

/// <summary>
/// SQL Server 接続を生成するファクトリ。
///
/// 【背景】
/// 旧構成では filepath.inf / datafile.inf により論理ファイルID→物理パスを解決して
/// ISAM ファイルを開いていた(FyGetFileName/FyGetFilePath)。SQL Server 化後は
/// それらの解決を不要とし、単一の接続文字列でテーブルへアクセスする。
/// </summary>
public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    /// <summary>開いた状態の接続を返す。呼び出し側で破棄すること。</summary>
    public SqlConnection CreateOpen()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
