namespace Ews.Data.Config;

/// <summary>
/// データファイル構成レジストリの1エントリ。
///
/// 【C原典】TOKUD/datafile.inf の1行
///   「ﾌｧｲﾙID, ﾌｧｲﾙﾊﾟｽID, ﾌｧｲﾙ名, ﾌｧｲﾙ名称」
/// 例) FYDM805, MSTCL, FYDM805, 機器ﾏｽﾀｰ
///
/// 旧構成では FyGetFileName()/FyGetFilePath() がこの情報と filepath.inf を突き合わせて
/// 物理パスを解決していた。SQL Server 化後は本エントリを構成テーブル
/// (DataFileRegistry)へ移送し、論理ファイルID→テーブル名の対応として用いる。
/// </summary>
/// <param name="FileId">ファイルID。【C原典】1列目(例 FYDM805)。</param>
/// <param name="PathId">ファイルパスID。【C原典】2列目(例 MSTCL)。filepath.inf のキー。</param>
/// <param name="FileName">物理ファイル名。【C原典】3列目。</param>
/// <param name="DisplayName">ファイル名称(日本語)。【C原典】4列目。</param>
public sealed record DataFileRegistryEntry(
    string FileId,
    string PathId,
    string FileName,
    string DisplayName);
