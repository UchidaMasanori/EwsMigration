using Ews.Domain.Masters;

namespace Ews.Data.Seeding;

/// <summary>
/// WORK ディレクトリ配下の .clh ファイルを値源とする <see cref="IPartNumberInfoRepository"/> 実装。
///
/// 【C原典】FyCpHbHbnInfFileR + FyCpFileGet(cpgtfile.c)。
///   C 原典は <c>FyGetFilePath("WORK") + "/" + 依頼明細番号(空白除去) + ".clh"</c> でパスを構築する。
///   実データの配置は <c>&lt;WORK&gt;/&lt;依頼明細番号&gt;/&lt;依頼明細番号&gt;.clh</c>
///   (例: WORK/2607AL01/2607AL01.clh)であり、本実装もこの配置で解決する。
///   読み込み自体は <see cref="PartNumberInfoLoader.ReadFromFile"/>(サイズ不一致は null)に委譲する。
/// </summary>
public sealed class FilePartNumberInfoRepository : IPartNumberInfoRepository
{
    private const string ClhExtension = ".clh";

    private readonly string _workDirectory;

    /// <param name="workDirectory">WORK ディレクトリの物理パス。【C原典】FyGetFilePath("WORK")。</param>
    public FilePartNumberInfoRepository(string workDirectory)
    {
        ArgumentNullException.ThrowIfNull(workDirectory);
        _workDirectory = workDirectory;
    }

    public PartNumberInfo? Find(string requestDetailNumber)
    {
        ArgumentNullException.ThrowIfNull(requestDetailNumber);

        // 【C原典】依頼明細番号の空白を除去してファイル名に用いる。
        string key = requestDetailNumber.Replace(" ", string.Empty);
        if (key.Length == 0)
        {
            return null;
        }

        string path = Path.Combine(_workDirectory, key, key + ClhExtension);
        return PartNumberInfoLoader.ReadFromFile(path);
    }
}
