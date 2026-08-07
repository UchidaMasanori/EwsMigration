namespace Ews.Analysis;

/// <summary>
/// 電力(W)を AT(アンペアトリップ)値に変換する。
/// 【C原典】Fysk01_Change_W_AT(toku/sekkei/src/Fysk01.c:4524)。
///   相数(sou)と定格電圧(v)で計算式を切り替え、pow による近似式で AT を求める。
///   ・三相 220V 以下: w&gt;=1000 は w^0.948*4.4、未満は w^0.945*(6.0-1.6*w)
///   ・三相 220V 超  : w&gt;=1500 は w^0.948*2.26、未満は w^0.948*(3.3-1.25*w)
///   ・単相 105V 以下: w^0.71*18.3
///   ・上記以外      : w^0.71*9.1
///   ※ w は kW 換算(w/1000.0)で計算する。
/// </summary>
public static class WattToAmpereConverter
{
    /// <summary>
    /// 電力(W)を AT 値へ変換する。
    /// 【C原典】Fysk01_Change_W_AT(w, sou, v)。
    /// </summary>
    /// <param name="watt">電力 W。【C原典】DOUBLE w。</param>
    /// <param name="phaseCount">相数。【C原典】SHORT sou。</param>
    /// <param name="voltage">定格電圧。【C原典】DOUBLE v。</param>
    public static double Convert(double watt, short phaseCount, double voltage)
    {
        double at;

        if (phaseCount == 3 && voltage <= 220.0)
        {
            at = watt >= 1000.0
                ? Math.Pow(watt / 1000.0, 0.948) * 4.4
                : Math.Pow(watt / 1000.0, 0.945) * (6.0 - 1.6 * watt / 1000.0);
        }
        else if (phaseCount == 3 && voltage > 220.0)
        {
            at = watt >= 1500.0
                ? Math.Pow(watt / 1000.0, 0.948) * 2.26
                : Math.Pow(watt / 1000.0, 0.948) * (3.3 - 1.25 * watt / 1000.0);
        }
        else if (phaseCount == 1 && voltage <= 105.0)
        {
            at = Math.Pow(watt / 1000.0, 0.71) * 18.3;
        }
        else
        {
            at = Math.Pow(watt / 1000.0, 0.71) * 9.1;
        }

        return at;
    }
}
