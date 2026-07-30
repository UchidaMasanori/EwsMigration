using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 大崎製WHの新品番選定(50Hz エリア)。三菱優先営業所でない場合に、WH のメーカーを
/// 大崎製(ON/O)へ変更し、条件を満たせば表示タイプを検定型(KE)にする。
/// 【C原典】PropSelNewONWhm(Fysk00.c:2892, 改訂&lt;64&gt;/&lt;70&gt;/&lt;104&gt;/&lt;106&gt;)。
///   三菱優先営業所の判定は <see cref="MitsubishiWhmPriorityChecker"/>(=PropChkHibknNum)による。
/// </summary>
public sealed class OhsakiWhmMakerResolver
{
    private readonly MitsubishiWhmPriorityChecker _priorityChecker;

    /// <param name="priorityChecker">三菱製WH優先営業所チェッカ(=PropChkHibknNum)。</param>
    public OhsakiWhmMakerResolver(MitsubishiWhmPriorityChecker priorityChecker)
    {
        ArgumentNullException.ThrowIfNull(priorityChecker);
        _priorityChecker = priorityChecker;
    }

    /// <summary>
    /// 大崎製WHの新品番メーカー・表示タイプを調整する。
    /// 【C原典】PropSelNewONWhm(sk, mcod, bknk, wtype)。
    /// </summary>
    /// <param name="wh">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位。【C原典】mcod[][3]。</param>
    /// <param name="displayTypes">表示用出力機器タイプ。【C原典】wtype[][7]。</param>
    /// <param name="officeCode">営業所コード。【C原典】bknk-&gt;key.im.eigyocd(PropChkHibknNum へ渡す)。</param>
    public void Resolve(MainCircuitResult wh, string[] makerCodes, string[] displayTypes, string officeCode)
    {
        ArgumentNullException.ThrowIfNull(wh);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(displayTypes);
        ArgumentNullException.ThrowIfNull(officeCode);

        MainCircuitData d = wh.Data;
        // 【C原典】改訂<104> WH かつ タイプ2 が BN(検定)でない。
        if (!Matches(d.ReservedWord, "WH ", 3) || Matches(d.DataType[1], "BN     ", 7))
        {
            return;
        }

        // 【C原典】メーカー指定なし かつ 50Hz(ep[2].epahz) が対象。
        if (FirstChar(d.AttachedParameter.MakerCode) != ' ' ||
            !Matches(d.ElectricalParameterSlots[2].Hz, "50", 2))
        {
            return;
        }

        // 【C原典】三菱優先営業所(東京/北関東支店等)でない場合のみ大崎製へ変更。
        if (_priorityChecker.Check(officeCode) == MitsubishiWhmPriority.Priority)
        {
            return;
        }

        makerCodes[0] = "ON ";   // 新メーカーコード(大崎製)
        makerCodes[1] = "O  ";

        // 【C原典】改訂<106> +(KM) 入力でも +(KM+KE) と同じ検定型(KE)を選定させる。
        if (Matches(d.DataType[2], "KM     ", 7) &&
            Matches(d.DataType[3], "NOTHING", 7) &&
            Matches(d.ElectricalParameterSlots[0].A1, "00000.000", 9) &&
            d.CircuitPoleCount == '3')
        {
            displayTypes[0] = "KE     ";
        }
    }

    private static char FirstChar(string value) => value.Length > 0 ? value[0] : ' ';

    // 【C原典】strncmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
