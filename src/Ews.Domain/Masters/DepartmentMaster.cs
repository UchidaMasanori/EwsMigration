namespace Ews.Domain.Masters;

/// <summary>
/// 部門(営業所/支店)マスター。
///
/// 【C原典】
///   - テキストマスタ: bumon.*.cns (確認用/bumon.gai17.cns 等, Shift-JIS)
///   - 形式: 「部門コード, 部門名(全角空白パディング), 電話番号,」のカンマ区切り。
///
/// 旧構成では .cns テキストを直接読み取って部門名を解決していた。
/// SQL Server 化後は DepartmentMaster テーブルへシードして参照する。
/// </summary>
/// <param name="DepartmentCode">部門コード。【C原典】bumon.cns 1列目。</param>
/// <param name="DepartmentName">部門名。【C原典】bumon.cns 2列目(全角空白パディング除去)。</param>
/// <param name="PhoneNumber">電話番号。【C原典】bumon.cns 3列目。</param>
public sealed record DepartmentMaster(
    string DepartmentCode,
    string DepartmentName,
    string PhoneNumber);
