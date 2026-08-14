using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 漏電ブレーカ(ELB)の感度電流(MA)を機器サーチ用に設定する。
///
/// 【C原典】Fysk0e_SetELBkando2(toku/sekkei/src/Fysk0e.c:98, VOID)。
///   char 版 Fysk0e_SetELBkando(=<see cref="CurrentParameterSetter"/> 内 ApplyElbSensitivity)の
///   数値(eparmg_s)版。呼び出し元は Fysk01_Set_ATAFMA。ep.Ma[0](epama[0]) を数値で設定する。
/// </summary>
public static class SearchElbSensitivityCurrentSetter
{
    /// <summary>動力回路を表す親相数。【C原典】kpaph == '3'。</summary>
    private const char PowerCircuit = '3';

    /// <summary>電灯回路を表す親相数。【C原典】kpaph == '1'。</summary>
    private const char LightingCircuit = '1';

    /// <summary>高感度形(EV)判定に用いるタイプ要素の添字。【C原典】&amp;type[7](type[][7] の要素1)。</summary>
    private const int EvTypeIndex = 1;

    /// <summary>高感度形を表すタイプ値。【C原典】memcmp(&amp;type[7], "EV ", 3)。</summary>
    private const string EvType = "EV ";

    /// <summary>
    /// フレーム容量・親相数・データタイプから感度電流 ep.Ma[0] を設定する。
    ///
    /// 【C原典】Fysk0e_SetELBkando2(af, kpaph, type, ep)。
    ///   kpaph=='3'(動力): af&lt;=60 は EV形15/他30、af&lt;=100 は100、超過は200。
    ///   kpaph=='1'(電灯): af&lt;=100 は EV形15/他30、超過は200。
    ///   いずれの相数でもなければ何もしない。
    /// </summary>
    /// <param name="frameCurrent">フレーム容量(af)。</param>
    /// <param name="parentPhase">親の回路相数(kpaph)。</param>
    /// <param name="dataType">データタイプ(type[][7])。type[1]=="EV " なら高感度形。</param>
    /// <param name="ep">設定先の電気パラメータ(ep)。</param>
    public static void Apply(
        double frameCurrent,
        char parentPhase,
        string[] dataType,
        NumericElectricalParameters ep)
    {
        ArgumentNullException.ThrowIfNull(dataType);
        ArgumentNullException.ThrowIfNull(ep);

        bool isEv = IsEvType(dataType);

        if (parentPhase == PowerCircuit)
        {
            // 動力回路。
            if (frameCurrent <= 60.0)
            {
                ep.Ma[0] = isEv ? 15.0 : 30.0;
            }
            else if (frameCurrent <= 100.0)
            {
                ep.Ma[0] = 100.0;
            }
            else
            {
                ep.Ma[0] = 200.0;
            }
        }
        else if (parentPhase == LightingCircuit)
        {
            // 電灯回路。
            if (frameCurrent <= 100.0)
            {
                ep.Ma[0] = isEv ? 15.0 : 30.0;
            }
            else
            {
                ep.Ma[0] = 200.0;
            }
        }
    }

    // 【C原典】memcmp(&type[7], "EV ", 3) == 0。type 要素1 の先頭3バイトが "EV " か(不足分は空白扱い)。
    private static bool IsEvType(string[] dataType)
    {
        if (dataType.Length <= EvTypeIndex)
        {
            return false;
        }

        string element = dataType[EvTypeIndex] ?? string.Empty;
        for (int i = 0; i < EvType.Length; i++)
        {
            char c = i < element.Length ? element[i] : ' ';
            if (c != EvType[i])
            {
                return false;
            }
        }

        return true;
    }
}
