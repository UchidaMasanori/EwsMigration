using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 負荷容量(fuka)から通電電流値(denryu)を算出する。負荷発生元設定
/// (<c>Fyss31_FukaHassei_Set</c>)およびその補助関数(<c>get_ep</c>)の下請け。
///
/// 【C原典】<c>set_denryu</c>(toku/sekkei/src/Fyss31.c:889, static)。
///
/// 負荷種類(fpalw1 の先頭2文字)で分岐して回路電圧(kpav[0])・相数(kpaph)から電流値を求める。
///   - "M "(電動機): 三相は容量帯別 pow 近似式(MG/INVBP/製作仕様の強制値あり)、単相は電圧別 pow 近似式。
///   - "H "(ヒーター): 単相=fuka/v、三相=fuka/v/√3(下限 0.01)。
///   - "S "(水銀灯): fuka/v に 36A 境界の係数。
///   - "HA"/"FL"/"NA"/"YA"/"YS": fuka/v。
///   - "TR": 単相=fuka/v、三相=fuka/v/√3。
///   - 上記以外: 算出不可。
/// </summary>
public static class EnergizingCurrentCalculator
{
    private static readonly double Sqrt3 = Math.Pow(3.0, 0.5);

    /// <summary>河村標準仕様でない(改訂&lt;5&gt;の強制電流値を適用する)製作仕様値。【C原典】seisakushiyou==2。</summary>
    public const int NonStandardProductionSpec = 2;

    /// <summary>
    /// 負荷容量から通電電流値を算出する。算出できた場合 true(<paramref name="current"/> に設定)、
    /// 負荷種類が対象外なら false。
    /// 【C原典】set_denryu(dt, denryu, fuka, fpalw1)。戻り値 0=算出/1=対象外。
    /// </summary>
    /// <param name="circuit">主回路データ。【C原典】dt(kpav[0]/kpaph/yoyaku/tokkbn)。</param>
    /// <param name="loadCapacity">負荷容量(W/VA)。【C原典】fuka。</param>
    /// <param name="loadKind">負荷種類(先頭2文字で分岐)。【C原典】fpalw1。</param>
    /// <param name="current">算出した通電電流値。【C原典】*denryu。</param>
    /// <param name="productionSpec">製作仕様(改訂&lt;5&gt;)。【C原典】seisakushiyou。既定は河村標準(強制値なし)。</param>
    public static bool TryCalculate(
        MainCircuitData circuit,
        double loadCapacity,
        string loadKind,
        out double current,
        int productionSpec = 1)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        current = 0.0;
        double v = EquipmentParameterFormatter.Stof(circuit.CircuitVoltage[0], 3);
        double fuka = loadCapacity;

        if (Matches(loadKind, "M ", 2))
        {
            current = CalculateMotor(circuit, fuka, v, productionSpec);
            return true;
        }

        if (Matches(loadKind, "H ", 2))
        {
            // 改訂<7>: 三相で 7270～10000 は計算式の意味不明により 7270 へ強制。
            if (circuit.CircuitPhaseCount != '1' && fuka >= 7270 && fuka <= 10000)
            {
                fuka = 7270;
            }

            current = circuit.CircuitPhaseCount == '1' ? fuka / v : fuka / v / Sqrt3;
            if (current < 0.01)
            {
                current = 0.01;
            }

            return true;
        }

        if (Matches(loadKind, "S ", 2))
        {
            double den = fuka / v;
            current = den > 36.0 ? den * 1.40 : den * 1.67;
            return true;
        }

        if (Matches(loadKind, "HA", 2) || Matches(loadKind, "FL", 2) || Matches(loadKind, "NA", 2))
        {
            current = fuka / v;
            return true;
        }

        if (Matches(loadKind, "TR", 2))
        {
            current = circuit.CircuitPhaseCount == '1' ? fuka / v : fuka / v / Sqrt3;
            return true;
        }

        if (Matches(loadKind, "YA", 2) || Matches(loadKind, "YS", 2))
        {
            current = fuka / v;
            return true;
        }

        return false;
    }

    /// <summary>電動機("M ")の通電電流値算出。【C原典】set_denryu の fpalw1=="M " ブロック。</summary>
    private static double CalculateMotor(MainCircuitData circuit, double fuka, double v, int productionSpec)
    {
        double denryu;

        if (circuit.CircuitPhaseCount == '3')
        {
            if (v <= 220.0)
            {
                denryu = fuka >= 1000.0
                    ? Math.Pow(fuka / 1000.0, 0.948) * 4.4
                    : Math.Pow(fuka / 1000.0, 0.945) * (6.0 - (1.6 * fuka / 1000.0));

                // 改訂<8>: MG は 0.75kW 超 1.0kW 以下で 4.4A 固定。
                if (Matches(circuit.ReservedWord, "MG", 3) && fuka > 750.0 && fuka <= 1000.0)
                {
                    denryu = 4.4;
                }

                // 改訂<5>: 河村標準仕様でない場合の容量帯別強制値。
                if (productionSpec == NonStandardProductionSpec)
                {
                    if (fuka > 1500.0 && fuka <= 2200.0)
                    {
                        denryu = 11.10;
                    }

                    if (fuka > 2200.0 && fuka <= 3700.0)
                    {
                        denryu = 15.20;
                    }

                    if (fuka > 549.0 && fuka <= 600.0)
                    {
                        denryu = 3.11;
                    }
                }
            }
            else
            {
                denryu = fuka >= 1500.0
                    ? Math.Pow(fuka / 1000.0, 0.948) * 2.26
                    : Math.Pow(fuka / 1000.0, 0.948) * (3.3 - (1.25 * fuka / 1000.0));
            }

            // 改訂<10>: INVBP の THR は容量帯別に電流値を強制。
            if (Matches(circuit.ReservedWord, "THR", 4) && circuit.SpecialReservedWordKind == '7')
            {
                denryu = InvbpThrCurrent(fuka, denryu);
            }
        }
        else
        {
            denryu = v <= 105.0
                ? Math.Pow(fuka / 1000.0, 0.71) * 18.3
                : Math.Pow(fuka / 1000.0, 0.71) * 9.1;
        }

        return denryu;
    }

    /// <summary>INVBP の THR の容量帯別強制電流値。【C原典】改訂&lt;10&gt;の階段判定。</summary>
    private static double InvbpThrCurrent(double fuka, double fallback) => fuka switch
    {
        <= 100.0 => 0.7,
        <= 200.0 => 1.3,
        <= 400.0 => 2.1,
        <= 750.0 => 3.6,
        <= 1500.0 => 6.6,
        <= 2200.0 => 9.0,
        <= 3700.0 => 15.0,
        <= 5500.0 => 22.0,
        <= 7500.0 => 29.0,
        <= 11000.0 => 42.0,
        <= 15000.0 => 54.0,
        <= 18500.0 => 67.0,
        <= 22000.0 => 82.0,
        <= 30000.0 => 105.0,
        _ => fallback,
    };

    // 【C原典】strncmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
