using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// INV 機器選定エラー時の表示文字列作成用に、電気パラメータへ設定 kw を W 値として書き込む。
///
/// 【C原典】PropSetInvErrEpstr(toku/sekkei/src/Fysk01.c:5730, 改訂&lt;28&gt;)。
///   sep[0].epaw1 = sep[1].epaw1 = sep[2].epaw1 = kw * 1000.0(kw を W に換算し先頭 3 要素へ設定)。
/// </summary>
public static class InverterErrorParameterSetter
{
    /// <summary>W 換算係数。【C原典】kw * 1000.0。</summary>
    private const double WattPerKw = 1000.0;

    /// <summary>設定対象の電気パラメータ要素数。【C原典】sep[0..2]。</summary>
    private const int TargetCount = 3;

    /// <summary>
    /// 設定 kw を W 値へ換算し、電気パラメータ先頭 3 要素の負荷容量(W)へ書き込む。
    /// </summary>
    public static void SetWattFromKw(IReadOnlyList<NumericElectricalParameters> parameters, double kw)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        double watt = kw * WattPerKw;
        for (int i = 0; i < TargetCount; i++)
        {
            parameters[i].W1 = watt;
        }
    }
}
