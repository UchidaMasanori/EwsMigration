using Dapper;
using Ews.Data.Abstractions;
using Ews.Domain.Circuits;

namespace Ews.Data.SqlServer;

/// <summary>
/// 回路内容記述ファイル(FYDF805)のキー。
/// 【C原典】struct FYDF805.key (新規登録依頼明細番号 + 行番号)。
/// </summary>
/// <param name="RequestNumber">新規登録依頼番号。【C原典】key.im.airaino。</param>
/// <param name="ItemNumber">新規登録明細番号。【C原典】key.im.ameisano。</param>
public readonly record struct CircuitLineKey(string RequestNumber, string ItemNumber);

/// <summary>
/// 回路内容記述ファイル(FYDF805)の SQL Server リポジトリ。
///
/// 【C原典】
///   - データ: struct FYDF805 (回路内容記述ファイル, EWS-ISAM)
///   - アクセス: 依頼明細番号で FyIsamStartR → FyIsamNextR し行番号順に走査。
/// </summary>
public sealed class SqlCircuitDescriptionRepository : IIsamTable<CircuitDescriptionLine, CircuitLineKey>
{
    private readonly SqlConnectionFactory _factory;

    public SqlCircuitDescriptionRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// 依頼明細番号+行番号で1行取得する。【C原典】FyIsamRead(回路内容記述)。
    /// </summary>
    public (IsamStatus Status, CircuitDescriptionLine? Record) Read(CircuitLineKey key, LockMode lockMode = LockMode.Unlock)
    {
        using var connection = _factory.CreateOpen();
        CircuitDescriptionLine? record = connection.QuerySingleOrDefault<CircuitDescriptionLine>(
            """
            SELECT  RequestNumber, ItemNumber, LineNumber, LineType, CircuitText, OriginalLineNumber, Command
            FROM    CircuitDescription
            WHERE   RequestNumber = @RequestNumber AND ItemNumber = @ItemNumber
            ORDER BY LineNumber
            """,
            new { key.RequestNumber, key.ItemNumber });

        return record is null
            ? (IsamStatus.NotFound, null)
            : (IsamStatus.Ok, record);
    }

    /// <summary>
    /// 依頼明細番号一致の全行を行番号順に取得する。
    /// 【C原典】FyIsamStartR(依頼明細番号) → FyIsamNextR ループ → FyIsamEndR。
    /// </summary>
    public IEnumerable<CircuitDescriptionLine> ReadSequential(CircuitLineKey partialKey)
    {
        using var connection = _factory.CreateOpen();
        return connection.Query<CircuitDescriptionLine>(
            """
            SELECT  RequestNumber, ItemNumber, LineNumber, LineType, CircuitText, OriginalLineNumber, Command
            FROM    CircuitDescription
            WHERE   RequestNumber = @RequestNumber AND ItemNumber = @ItemNumber
            ORDER BY LineNumber
            """,
            new { partialKey.RequestNumber, partialKey.ItemNumber }).ToList();
    }

    /// <summary>【C原典】FyIsamAdd(回路内容記述)。</summary>
    public IsamStatus Add(CircuitDescriptionLine record)
    {
        using var connection = _factory.CreateOpen();
        connection.Execute(
            """
            INSERT INTO CircuitDescription
                (RequestNumber, ItemNumber, LineNumber, LineType, CircuitText, OriginalLineNumber, Command)
            VALUES
                (@RequestNumber, @ItemNumber, @LineNumber, @LineType, @CircuitText, @OriginalLineNumber, @Command)
            """,
            record);
        return IsamStatus.Ok;
    }

    /// <summary>【C原典】FyIsamRewrite(回路内容記述)。</summary>
    public IsamStatus Rewrite(CircuitDescriptionLine record)
    {
        using var connection = _factory.CreateOpen();
        int affected = connection.Execute(
            """
            UPDATE CircuitDescription
            SET    LineType = @LineType, CircuitText = @CircuitText,
                   OriginalLineNumber = @OriginalLineNumber, Command = @Command
            WHERE  RequestNumber = @RequestNumber AND ItemNumber = @ItemNumber AND LineNumber = @LineNumber
            """,
            record);
        return affected > 0 ? IsamStatus.Ok : IsamStatus.NotFound;
    }

    /// <summary>【C原典】FyIsamDelete(回路内容記述, 依頼明細番号)。</summary>
    public IsamStatus Delete(CircuitLineKey key)
    {
        using var connection = _factory.CreateOpen();
        int affected = connection.Execute(
            "DELETE FROM CircuitDescription WHERE RequestNumber = @RequestNumber AND ItemNumber = @ItemNumber",
            new { key.RequestNumber, key.ItemNumber });
        return affected > 0 ? IsamStatus.Ok : IsamStatus.NotFound;
    }
}
