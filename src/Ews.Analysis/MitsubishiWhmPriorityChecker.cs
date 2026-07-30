using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>三菱製WH優先営業所チェック(PropChkHibknNum)の判定結果。</summary>
public enum MitsubishiWhmPriority
{
    /// <summary>三菱製WH優先営業所でない(whm_sentei.cns に非物件コードなし)。【C原典】return 0。</summary>
    NotPriority = 0,

    /// <summary>三菱製WH優先営業所(東京/北関東支店等)。【C原典】return 1。</summary>
    Priority = 1,

    /// <summary>営業所コードから非物件コードを取得できず(コンスタント欠落含む)。【C原典】return -1。</summary>
    Error = -1,
}

/// <summary>
/// 営業所コードが三菱製WH優先の営業所かどうかを、非物件コードを経由して判定する。
/// 【C原典】PropChkHibknNum(Fysk00.c:6130, 改訂&lt;70&gt;)。
///   (1) eigyocd.cns で営業所コードから非物件コードを逆引きし、
///   (2) whm_sentei.cns にその非物件コードが登録されていれば「優先営業所」と判定する。
/// </summary>
public sealed class MitsubishiWhmPriorityChecker
{
    private const int CodeWidth = 2;

    private readonly IReadOnlyList<NonPropertyOfficeEntry> _officeTable;
    private readonly IReadOnlyList<string> _priorityNonPropertyCodes;

    /// <param name="officeTable">営業所コード識別テーブル(eigyocd.cns)。</param>
    /// <param name="priorityNonPropertyCodes">三菱製WH優先の非物件コード一覧(whm_sentei.cns)。</param>
    public MitsubishiWhmPriorityChecker(IReadOnlyList<NonPropertyOfficeEntry> officeTable,
                                        IReadOnlyList<string> priorityNonPropertyCodes)
    {
        ArgumentNullException.ThrowIfNull(officeTable);
        ArgumentNullException.ThrowIfNull(priorityNonPropertyCodes);
        _officeTable = officeTable;
        _priorityNonPropertyCodes = priorityNonPropertyCodes;
    }

    /// <summary>
    /// 営業所コードが三菱製WH優先の営業所か判定する。
    /// 【C原典】PropChkHibknNum(bknk)。bknk-&gt;key.im.eigyocd の先頭 2 桁が入力。
    /// </summary>
    /// <param name="officeCode">営業所コード。【C原典】eigcd(先頭2桁を使用)。</param>
    public MitsubishiWhmPriority Check(string officeCode)
    {
        ArgumentNullException.ThrowIfNull(officeCode);

        string eigcd = Truncate(officeCode, CodeWidth);

        // 【C原典】eigyocd.cns を先頭行から走査し、営業所コードを含む最初の行の非物件コードを採用。
        string? nonPropertyCode = null;
        foreach (NonPropertyOfficeEntry entry in _officeTable)
        {
            foreach (string office in entry.OfficeCodes)
            {
                if (CodeEquals(eigcd, office))
                {
                    nonPropertyCode = entry.NonPropertyCode;
                    break;
                }
            }
            if (nonPropertyCode is not null)
            {
                break;
            }
        }

        // 【C原典】非物件コードが得られなければエラー(return -1)。
        if (string.IsNullOrEmpty(nonPropertyCode))
        {
            return MitsubishiWhmPriority.Error;
        }

        // 【C原典】whm_sentei.cns に登録があれば優先営業所(return 1)、なければ非優先(return 0)。
        foreach (string code in _priorityNonPropertyCodes)
        {
            if (CodeEquals(nonPropertyCode, code))
            {
                return MitsubishiWhmPriority.Priority;
            }
        }
        return MitsubishiWhmPriority.NotPriority;
    }

    // 【C原典】strncmp(a, b, 2): 先頭 2 桁の一致。
    private static bool CodeEquals(string a, string b) =>
        string.CompareOrdinal(Truncate(a, CodeWidth), Truncate(b, CodeWidth)) == 0;

    private static string Truncate(string value, int width) =>
        value.Length >= width ? value[..width] : value;
}
