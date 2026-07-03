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
/// SQL Server 側は C原典に忠実に PRIMARY キー = struct p805_key
/// (ReservedWord + MakerCode + ParameterType + RatingKey)=(予約語 + メーカーコード
/// + パラメータタイプ + 定格キー)とする。品番(ALTERNATE キー1)は非一意のため、
/// 品番読みは品番索引 EquipmentPartNumberIndex(=FYDF816)をデータ追番順に走査する。
/// </summary>
public sealed class SqlEquipmentMasterRepository : IIsamTable<EquipmentMaster, string>
{
    private readonly SqlConnectionFactory _factory;

    public SqlEquipmentMasterRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// 品番でマスターを1件取得する。
    /// 【C原典】FyMasFYDM805ByHinban (toku/lib/libmaster/Fyfydm805.c)。
    /// 品番は非一意のため、品番索引 FYDF816 をデータ追番(datano)順に走査し、
    /// 先頭(追番 0001 相当)の PRIMARY キーで機器マスターを読む。
    /// </summary>
    public (IsamStatus Status, EquipmentMaster? Record) Read(string partNumber, LockMode lockMode = LockMode.Unlock)
    {
        using var connection = _factory.CreateOpen();
        EquipmentMaster? record = connection.QueryFirstOrDefault<EquipmentMaster>(
            """
            SELECT  TOP 1
                    m.ReservedWord, m.MakerCode, m.ParameterType, m.RatingKey,
                    m.PartNumber, m.PartName, m.ElectricalParameters
            FROM    EquipmentPartNumberIndex ix
            JOIN    EquipmentMaster m
                ON  m.ReservedWord  = ix.ReservedWord
                AND m.MakerCode     = ix.MakerCode
                AND m.ParameterType = ix.ParameterType
                AND m.RatingKey     = ix.RatingKey
            WHERE   ix.PartNumber = @PartNumber
            ORDER BY ix.DataNo
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
            SELECT  ReservedWord, MakerCode, ParameterType, RatingKey,
                    PartNumber, PartName, ElectricalParameters
            FROM    EquipmentMaster
            WHERE   ReservedWord LIKE @Prefix + '%'
            ORDER BY ReservedWord, MakerCode, ParameterType, RatingKey
            """,
            new { Prefix = reservedWordPrefix }).ToList();
    }

    /// <summary>【C原典】FyIsamAdd(機器マスター)。</summary>
    public IsamStatus Add(EquipmentMaster record)
    {
        using var connection = _factory.CreateOpen();
        connection.Execute(
            """
            INSERT INTO EquipmentMaster
                (ReservedWord, MakerCode, ParameterType, RatingKey, PartNumber, PartName, ElectricalParameters)
            VALUES
                (@ReservedWord, @MakerCode, @ParameterType, @RatingKey, @PartNumber, @PartName, @ElectricalParameters)
            """,
            record);
        return IsamStatus.Ok;
    }

    /// <summary>【C原典】FyIsamRewrite(機器マスター)。PRIMARY キー(pkey)で更新。</summary>
    public IsamStatus Rewrite(EquipmentMaster record)
    {
        using var connection = _factory.CreateOpen();
        int affected = connection.Execute(
            """
            UPDATE EquipmentMaster
            SET    PartNumber = @PartNumber,
                   PartName = @PartName,
                   ElectricalParameters = @ElectricalParameters
            WHERE  ReservedWord  = @ReservedWord
              AND  MakerCode     = @MakerCode
              AND  ParameterType = @ParameterType
              AND  RatingKey     = @RatingKey
            """,
            record);
        return affected > 0 ? IsamStatus.Ok : IsamStatus.NotFound;
    }

    /// <summary>
    /// 品番に一致する機器マスターを削除する。
    /// 【C原典】FyIsamDelete は PRIMARY キー削除だが、本インターフェースは品番キー。
    /// 品番は非一意のため、同一品番の全レコードが対象となる。
    /// </summary>
    public IsamStatus Delete(string partNumber)
    {
        using var connection = _factory.CreateOpen();
        int affected = connection.Execute(
            "DELETE FROM EquipmentMaster WHERE PartNumber = @PartNumber",
            new { PartNumber = partNumber });
        return affected > 0 ? IsamStatus.Ok : IsamStatus.NotFound;
    }
}
