using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 上流パラメータ生成における ep[2](システム側生成値)の設定。
/// 【C原典】toku/sekkei/src/Fyss14.c の SetParam_ep2_* 群。
///
/// 回路電気値(<c>dt.kpa*</c>=<see cref="MainCircuitData"/> の CircuitPhaseCount/CircuitWireType/
/// CircuitPoleCount/CircuitVoltage[3]/CircuitVoltageKind)から、予約語別に ep[2]
/// (3 スロット目の電気パラメータ=システム側の生成値)を決定する決定的処理を移植する。
/// Make_UpperParm(主回路上流パラメータ生成)から呼び出される。
///
/// 本クラスは単一レコード(<see cref="MainCircuitData"/>)内で完結する決定的セッタのみを収録する。
/// 次の関数は依存が未モデル化のため段階移植の後続とする:
///   ・SetParam_ep2_RTR_V1 … 親機器相対参照(datano/oyatno でレコードを遡る)・PLTR 依存。
///   ・SetParam_ep2_MC_AC / SetParam_ep2_MC_BC … INVBP(tokkbn=='7')・fp.fpalw2 依存(改訂&lt;37&gt;)。
///   ・SetParam_ep2_TR_V2 / SetParam_ep2_DCPW_V1 / SetParam_ep2_epap2P … 追加引数・改訂依存。
///   ・SetParam_ep2(ディスパッチャ) … bukken(FYDF801)・MCPRMS 依存。
/// </summary>
public static class SecondaryParameterSetter
{
    /// <summary>
    /// MCB 用 極数(Ｐ)の設定。【C原典】SetParam_ep2_MCB_P(Fyss14.c:2350)。
    /// 回路極数が '1' なら ep[2] の極数3桁目を '2'、それ以外は回路極数そのものとする。
    /// </summary>
    public static void SetMcbPole(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        char p = data.CircuitPoleCount == '1' ? '2' : data.CircuitPoleCount;
        ep2.P = SetCharAt(ep2.P, 2, p);
    }

    /// <summary>
    /// MCB 用 エレメント数(Ｅ)の設定。【C原典】SetParam_ep2_MCB_E(Fyss14.c)。
    /// ep[0] の AF/AT が "99999.999"(=定格なし)なら '0'、
    /// それ以外は回路相数・線式・極数の組合せでエレメント数を決定する。
    /// </summary>
    public static void SetMcbElement(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // epae がこの時点で '\0' のものは '0' に置き換える。【C原典】for(i=0;i<3;i++)。
        for (int i = 0; i < 3; i++)
        {
            ElectricalParameters ep = data.ElectricalParameterSlots[i];
            if (ep.E.Length == 0 || ep.E == "\0")
            {
                ep.E = "0";
            }
        }

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        char ph = data.CircuitPhaseCount;
        char wr = data.CircuitWireType;
        char p = data.CircuitPoleCount;

        // 【C原典】memcmp(ep[0].epaat,"99999.999",9)==0 → epae='0'。
        if (data.ElectricalParameterSlots[0].At == "99999.999")
        {
            ep2.E = "0";
        }
        else if (ph == '1' && wr == '2' && p == '1')
        {
            ep2.E = "1";
        }
        else if (ph == '1' && wr == '2')
        {
            ep2.E = "2";
        }
        else if (ph == '1' && wr == '3')
        {
            ep2.E = "2";
        }
        else if (ph == '3' && wr == '3')
        {
            ep2.E = "3";
        }
        else if (ph == '3' && wr == '4')
        {
            ep2.E = "3";
        }
        else if (ph == '0' && wr == '0')
        {
            ep2.E = "2";
        }
    }

    /// <summary>
    /// MCB 用 電圧２(Ｖ２)・AC/DC 区分の設定。【C原典】SetParam_ep2_MCB_V2(Fyss14.c)。
    /// 回路電圧 3 スロットのうち最大値を ep[2] の電圧２[0] の 4 桁目以降 3 桁へ格納し、
    /// 残り 2 スロットを "000000.0"、AC/DC 区分を回路電圧区分とする。
    /// </summary>
    public static void SetMcbVoltage2(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];

        int n = MaxVoltageIndex(data.CircuitVoltage);
        // 【C原典】memcpy(&ep[2].epav2[0][3], kpav[n], 3)。
        ep2.V2[0] = ReplaceSegment(ep2.V2[0], 3, data.CircuitVoltage[n]);
        ep2.V2[1] = "000000.0";
        ep2.V2[2] = "000000.0";
        ep2.V2Kbn = data.CircuitVoltageKind;
    }

    /// <summary>
    /// MC 用 極数(Ｐ)の設定。【C原典】SetParam_ep2_MC_P(Fyss14.c, 950518)。
    /// 回路電圧[0] が 105 超なら 3 桁目 '2'、以下なら '1'。
    /// </summary>
    public static void SetMcPole(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        int kv0 = AtoiC(data.CircuitVoltage[0]);
        ep2.P = SetCharAt(ep2.P, 2, kv0 > 105 ? '2' : '1');
    }

    /// <summary>MC 用 電圧２の設定。【C原典】SetParam_ep2_MC_V2 = SetParam_ep2_MCB_V2。</summary>
    public static void SetMcVoltage2(MainCircuitData data) => SetMcbVoltage2(data);

    /// <summary>MG 用 エレメント数(Ｅ)の設定。【C原典】SetParam_ep2_MG_E。常に '2'。</summary>
    public static void SetMgElement(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].E = "2";
    }

    /// <summary>MG 用 電圧２の設定。【C原典】SetParam_ep2_MG_V2 = SetParam_ep2_MCB_V2。</summary>
    public static void SetMgVoltage2(MainCircuitData data) => SetMcbVoltage2(data);

    /// <summary>MG 用 ａ接点数(ＡＣ)の設定。【C原典】SetParam_ep2_MG_AC。常に "00"。</summary>
    public static void SetMgContactA(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].Ac = "00";
    }

    /// <summary>MG 用 ｂ接点数(ＢＣ)の設定。【C原典】SetParam_ep2_MG_BC。常に "00"。</summary>
    public static void SetMgContactB(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].Bc = "00";
    }

    /// <summary>TS 用 電圧２の設定。【C原典】SetParam_ep2_TS_V2 = SetParam_ep2_MCB_V2(941130)。</summary>
    public static void SetTsVoltage2(MainCircuitData data) => SetMcbVoltage2(data);

    /// <summary>
    /// TS 用 制御電圧(ＶＣ)・AC/DC 区分の設定。【C原典】SetParam_ep2_TS_VC(941130)。
    /// 回路電圧 3 スロットの最大値を ep[2] の制御電圧(3 桁)へ格納し、AC/DC 区分を回路電圧区分とする。
    /// </summary>
    public static void SetTsControlVoltage(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        int n = MaxVoltageIndex(data.CircuitVoltage);
        ep2.Vc = data.CircuitVoltage[n];
        ep2.VcKbn = data.CircuitVoltageKind;
    }

    /// <summary>TS 用 ａ接点数(ＡＣ)の設定。【C原典】SetParam_ep2_TS_AC(941130)。常に "00"。</summary>
    public static void SetTsContactA(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].Ac = "00";
    }

    /// <summary>TS 用 ｂ接点数(ＢＣ)の設定。【C原典】SetParam_ep2_TS_BC(941130)。常に "00"。</summary>
    public static void SetTsContactB(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].Bc = "00";
    }

    /// <summary>
    /// 回路電圧 3 スロットのうち最大値のインデックスを返す。
    /// 【C原典】n=((memcmp(kpav[0],kpav[1],3)&gt;0)?0:1); n=((memcmp(kpav[n],kpav[2],3)&gt;0)?n:2);。
    /// memcmp は固定 3 バイト比較のため、等長文字列の序数比較で忠実に再現する。
    /// </summary>
    private static int MaxVoltageIndex(string[] voltage)
    {
        int n = string.CompareOrdinal(voltage[0], voltage[1]) > 0 ? 0 : 1;
        n = string.CompareOrdinal(voltage[n], voltage[2]) > 0 ? n : 2;
        return n;
    }

    /// <summary>固定長文字列の指定インデックスに 1 文字を上書きする(幅は保持)。</summary>
    private static string SetCharAt(string s, int index, char c)
    {
        char[] arr = (s.Length > index ? s : s.PadRight(index + 1, '0')).ToCharArray();
        arr[index] = c;
        return new string(arr);
    }

    /// <summary>固定長文字列の指定開始位置へ部分文字列を上書きする(幅は保持)。【C原典】memcpy(&amp;dst[start],src,len)。</summary>
    private static string ReplaceSegment(string s, int start, string segment)
    {
        int required = start + segment.Length;
        char[] arr = (s.Length >= required ? s : s.PadRight(required, '0')).ToCharArray();
        for (int i = 0; i < segment.Length; i++)
        {
            arr[start + i] = segment[i];
        }
        return new string(arr);
    }

    /// <summary>先頭空白スキップ+符号+数字部のみ解釈する C の atoi 相当。【C原典】atoi。</summary>
    private static int AtoiC(string s)
    {
        int i = 0;
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t'))
        {
            i++;
        }
        int sign = 1;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            if (s[i] == '-')
            {
                sign = -1;
            }
            i++;
        }
        long value = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            value = (value * 10) + (s[i] - '0');
            i++;
        }
        return (int)(sign * value);
    }
}
