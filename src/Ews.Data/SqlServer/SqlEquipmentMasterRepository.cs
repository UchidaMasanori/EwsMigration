using Dapper;
using Ews.Data.Abstractions;
using Ews.Domain.Masters;

namespace Ews.Data.SqlServer;

/// <summary>
/// 機器マスター(FYDM805)の SQL Server リポジトリ。
///
/// 【C原典】
///   - データ: struct FYDM805 (機器マスター, EWS-ISAM)
///   - アクセス: FyIsamRead / FyIsamStartR+FyIsamNextR (品番・予約語キー)
///
/// SQL Server 側は C原典に忠実に PRIMARY キー = (ReservedWord, MakerCode)=(予約語 + メーカーコード)
/// とし、品番(ALTERNATE キー1, C原典で NULL あり)は NULL 除外のフィルタ付き一意インデックス
/// (UX_EquipmentMaster_PartNumber)で引く。
/// </summary>
public sealed class SqlEquipmentMasterRepository : IIsamTable<EquipmentMaster, string>
{
    private readonly SqlConnectionFactory _factory;

    public SqlEquipmentMasterRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// 品番でマスターを1件取得する。【C原典】FyIsamRead(機器マスター, 品番キー)。
    /// </summary>
    public (IsamStatus Status, EquipmentMaster? Record) Read(string partNumber, LockMode lockMode = LockMode.Unlock)
    {
        using var connection = _factory.CreateOpen();
        EquipmentMaster? record = connection.QuerySingleOrDefault<EquipmentMaster>(
            """
            SELECT  ReservedWord, MakerCode, PartNumber, PartName, ElectricalParameters
            FROM    EquipmentMaster
            WHERE   PartNumber = @PartNumber
            """,
            new { PartNumber = partNumber });

        return record is null
            ? (IsamStatus.NotFound, null)
            : (IsamStatus.Ok, record);
    }

    /// <summary>
    /// 予約語の前方一致でマスターを順次取得する。
    /// 【C原典】FyIsamStartR → FyIsamNextR(予約語キー)のループ。
    /// </summary>
    public IEnumerable<EquipmentMaster> ReadSequential(string reservedWordPrefix)
    {
        using var connection = _factory.CreateOpen();
        return connection.Query<EquipmentMaster>(
            """
            SELECT  ReservedWord, MakerCode, PartNumber, PartName, ElectricalParameters
            FROM    EquipmentMaster
            WHERE   ReservedWord LIKE @Prefix + '%'
            ORDER BY ReservedWord, MakerCode
            """,
            new { Prefix = reservedWordPrefix }).ToList();
    }

    /// <summary>【C原典】FyIsamAdd(機器マスター)。</summary>
    public IsamStatus Add(EquipmentMaster record)
    {
        using var connection = _factory.CreateOpen();
        connection.Execute(
            """
            INSERT INTO EquipmentMaster (ReservedWord, MakerCode, PartNumber, PartName, ElectricalParameters)
            VALUES (@ReservedWord, @MakerCode, @PartNumber, @PartName, @ElectricalParameters)
            """,
            record);
        return IsamStatus.Ok;
    }

    /// <summary>【C原典】FyIsamRewrite(機器マスター)。</summary>
    public IsamStatus Rewrite(EquipmentMaster record)
    {
        using var connection = _factory.CreateOpen();
        int affected = connection.Execute(
            """
            UPDATE EquipmentMaster
            SET    PartName = @PartName,
                   ElectricalParameters = @ElectricalParameters
            WHERE  PartNumber = @PartNumber
            """,
            record);
        return affected > 0 ? IsamStatus.Ok : IsamStatus.NotFound;
    }

    /// <summary>【C原典】FyIsamDelete(機器マスター)。</summary>
    public IsamStatus Delete(string partNumber)
    {
        using var connection = _factory.CreateOpen();
        int affected = connection.Execute(
            "DELETE FROM EquipmentMaster WHERE PartNumber = @PartNumber",
            new { PartNumber = partNumber });
        return affected > 0 ? IsamStatus.Ok : IsamStatus.NotFound;
    }
}
