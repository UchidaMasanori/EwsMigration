using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 三菱製ブレーカ(MCB/ELB)でフレーム電流(AF)入力が無い場合に、トリップ電流(AT)に応じて
/// フレーム電流を 50A に補完する。
///
/// 【C原典】PropSetAfForMitsubishi(toku/sekkei/src/Fysk01.c:5187, static VOID, 改訂&lt;10&gt;)。
///   メーカーコードが M/MN/MKY のいずれか、かつ予約語が MCB/ELB のとき、sep[0].epaaf 未入力
///   (|epaaf|&lt;=TOL)で 5&lt;sep[0].epaat&lt;=50 なら sep[epno].epaaf=50 を設定する。
/// </summary>
public static class MitsubishiFrameCurrentSetter
{
    /// <summary>補完するフレーム電流(AF)。【C原典】sep[epno].epaaf=50.0。</summary>
    private const double FrameCurrent = 50.0;

    /// <summary>トリップ電流(AT)下限(排他)。【C原典】5.0 &lt; epaat。</summary>
    private const double AtLower = 5.0;

    /// <summary>トリップ電流(AT)上限(包含)。【C原典】epaat &lt;= 50.0。</summary>
    private const double AtUpper = 50.0;

    /// <summary>入力有無の判定しきい値。【C原典】TOL(fyrt808.h:25)。</summary>
    private const double Tolerance = 0.001;

    /// <summary>三菱系メーカーコード(先頭3バイト)。【C原典】"M  "/"MN "/"MKY"。</summary>
    private static readonly string[] MitsubishiMakerCodes = ["M  ", "MN ", "MKY"];

    /// <summary>対象予約語(先頭4バイト)。【C原典】"MCB "/"ELB "。</summary>
    private static readonly string[] TargetReservedWords = ["MCB ", "ELB "];

    /// <summary>
    /// 三菱製 MCB/ELB のフレーム電流(AF)を条件付きで 50A に補完する。
    /// </summary>
    /// <param name="reservedWord">予約語(先頭4バイトを照合)。</param>
    /// <param name="makerCode">メーカーコード(先頭3バイトを照合)。</param>
    /// <param name="epno">設定対象の電気パラメータ番号。</param>
    /// <param name="parameters">電気パラメータ配列(判定は [0]、設定は [epno])。</param>
    public static void Apply(
        string reservedWord,
        string makerCode,
        int epno,
        IReadOnlyList<NumericElectricalParameters> parameters)
    {
        ArgumentNullException.ThrowIfNull(reservedWord);
        ArgumentNullException.ThrowIfNull(makerCode);
        ArgumentNullException.ThrowIfNull(parameters);

        // 【C原典】三菱(M/MN/MKY)以外はパス。
        if (!HasPrefix(makerCode, MitsubishiMakerCodes))
        {
            return;
        }

        // 【C原典】MCB/ELB 以外はパス。
        if (!HasPrefix(reservedWord, TargetReservedWords))
        {
            return;
        }

        NumericElectricalParameters first = parameters[0];

        // 【C原典】自由文字でフレーム容量の入力ありはパス。
        if (Math.Abs(first.Af) > Tolerance)
        {
            return;
        }

        if (AtLower < first.At && first.At <= AtUpper)
        {
            parameters[epno].Af = FrameCurrent;
        }
    }

    private static bool HasPrefix(string value, string[] codes)
    {
        foreach (string code in codes)
        {
            if (value.Length >= code.Length && value.AsSpan(0, code.Length).SequenceEqual(code))
            {
                return true;
            }
        }

        return false;
    }
}
