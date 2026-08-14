using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// タイプにより AT/AF/MA/AM の値を変える。機器選定時の電気パラメータ設定。
///
/// 【C原典】Fysk01_Set_ATAFMA(toku/sekkei/src/Fysk01.c:4700, static SHORT)。
///   epno==2(下位機器 sep[2])では Get_Ibs で基準電流を求めて AT/AF/MA/AM を設定し、
///   それ以外(上位機器 sep[1])では sep[0](入力)を優先しつつ sep[2] をフォールバックに AT/AF/MA/AM を設定する。
///   感度電流(MA)は ELB 系で <see cref="SearchElbSensitivityCurrentSetter"/>(=Fysk0e_SetELBkando2)へ委譲する。
/// </summary>
public static class AtAfMaAmSetter
{
    /// <summary>正常終了。【C原典】return(0)。</summary>
    private const short Good = 0;

    /// <summary>システムエラー(基準電流が負)。【C原典】return(SYS_ERR)。SYS_ERR==-1(fyrt808.h:35)。</summary>
    private const short SysErr = -1;

    /// <summary>実数一致許容誤差。【C原典】TOL == 0.001(fyrt808.h:25)。</summary>
    private const double Tol = 0.001;

    /// <summary>異常大値の上限。【C原典】fabs(...) &gt; 99999.0 なら 0 扱い。</summary>
    private const double MaxValidValue = 99999.0;

    /// <summary>下位機器 sep[2]・フラグ添字[1]。【C原典】epno == 2。</summary>
    private const int LowerParameterNo = 2;

    // 予約語(memcmp 幅ぶん空白埋め比較)。
    private const int ReservedWordWidth = 6;
    private const int NhmbWidth = 5;

    /// <summary>
    /// AT/AF/MA/AM を設定する。
    /// </summary>
    /// <param name="reservedWord">予約語(yo)。</param>
    /// <param name="electricalParameterNo">電気パラメータ番号(epno)。2 なら下位機器。</param>
    /// <param name="parameters">電気パラメータ sep[0..2](入力=sep[0]、設定先=sep[1]/sep[2])。</param>
    /// <param name="dataType">タイプ(dtype)。要素0 の先頭4/5バイトで AM 判定、要素1 で EV 判定。</param>
    /// <param name="work">選定ワーク(wk1)。負荷容量/通電電流/相数/電圧/始動区分/親相数。</param>
    /// <param name="flags">項目書替えフラグ(wk3)。</param>
    /// <returns>正常時 0、基準電流が負なら SYS_ERR(-1)。</returns>
    public static short Apply(
        string reservedWord,
        int electricalParameterNo,
        NumericElectricalParameters[] parameters,
        string[] dataType,
        SelectionWorkParameters work,
        AreaRewriteFlags flags)
    {
        ArgumentNullException.ThrowIfNull(reservedWord);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(dataType);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(flags);

        if (electricalParameterNo == LowerParameterNo)
        {
            return ApplyLower(reservedWord, parameters, dataType, work, flags);
        }

        ApplyUpper(reservedWord, parameters, dataType, work, flags);
        return Good;
    }

    // 【C原典】epno == 2 の分岐。下位機器 sep[2] へ Get_Ibs 由来の基準電流を設定する。
    private static short ApplyLower(
        string reservedWord,
        NumericElectricalParameters[] parameters,
        string[] dataType,
        SelectionWorkParameters work,
        AreaRewriteFlags flags)
    {
        double ibs = LoadCurrentCalculator.Calculate(
            work.LoadKind,
            reservedWord,
            work.EnergizingCurrent,
            Element(dataType, 0),
            work.LoadCapacity,
            work.PhaseCount,
            work.CircuitVoltage,
            work.StartKind);
        if (ibs < 0.0)
        {
            return SysErr;
        }

        if (FixedEquals(reservedWord, "CKS", ReservedWordWidth))
        {
            parameters[2].A2 = ibs;
            flags.A2[1] = true;
            return Good;
        }

        parameters[2].At = ibs;
        flags.At[1] = true;

        if (FixedEquals(reservedWord, "NHMB", NhmbWidth))
        {
            return Good;
        }

        parameters[2].Af = Math.Abs(parameters[0].Af) < Tol ? ibs : parameters[0].Af;
        flags.Af[1] = true;

        if (IsElb(reservedWord))
        {
            if (Math.Abs(parameters[0].Ma[0]) < Tol)
            {
                SearchElbSensitivityCurrentSetter.Apply(
                    parameters[2].Af, work.ParentPhaseCount, dataType, parameters[2]);
            }
            else
            {
                parameters[2].Ma[0] = parameters[0].Ma[0];
                parameters[2].Ma[1] = parameters[0].Ma[1];
                parameters[2].Ma[2] = parameters[0].Ma[2];
            }

            flags.Ma[1] = true;
        }
        else if (IsHpsbOrHsb(reservedWord) && FixedEquals(Element(dataType, 0), "AM", 4))
        {
            parameters[2].Am = Math.Abs(parameters[0].Am) < Tol ? work.EnergizingCurrent : parameters[0].Am;
            flags.Am[1] = true;
        }

        return Good;
    }

    // 【C原典】epno != 2 の分岐。上位機器 sep[1] へ sep[0] 優先・sep[2] フォールバックで設定する。
    private static void ApplyUpper(
        string reservedWord,
        NumericElectricalParameters[] parameters,
        string[] dataType,
        SelectionWorkParameters work,
        AreaRewriteFlags flags)
    {
        if (FixedEquals(reservedWord, "CKS", ReservedWordWidth))
        {
            parameters[1].A2 = SelectPreferred(parameters[0].A2, parameters[2].A2);
            flags.A2[0] = true;
            return;
        }

        if (FixedEquals(reservedWord, "NHMB", NhmbWidth))
        {
            parameters[1].At = SelectPreferred(parameters[0].At, parameters[2].At);
            flags.At[0] = true;
            return;
        }

        parameters[1].At = IsAtDirectReservedWord(reservedWord)
            ? SelectPreferred(parameters[0].At, parameters[2].At)
            : SelectAtWithWattFallback(parameters, work);
        flags.At[0] = true;

        if (Math.Abs(parameters[0].Af) > Tol)
        {
            parameters[1].Af = parameters[0].Af;
        }
        else
        {
            parameters[1].Af = Math.Abs(parameters[1].At) > Tol ? parameters[1].At : parameters[2].Af;
        }

        flags.Af[0] = true;

        if (IsElb(reservedWord))
        {
            if (Math.Abs(parameters[0].Ma[0]) > Tol)
            {
                parameters[1].Ma[0] = parameters[0].Ma[0];
                parameters[1].Ma[1] = parameters[0].Ma[1];
                parameters[1].Ma[2] = parameters[0].Ma[2];
                flags.Ma[0] = true;
            }
            else
            {
                // 【C原典】改訂<3> 以降、感度電流は SetELBkando2 で設定(旧固定値ロジックは #if 0 で無効)。フラグは立てない。
                SearchElbSensitivityCurrentSetter.Apply(
                    parameters[1].Af, work.ParentPhaseCount, dataType, parameters[1]);
            }
        }
        else if (IsHpsbOrHsb(reservedWord) && FixedEquals(Element(dataType, 0), "AM", 5))
        {
            parameters[1].Am = Math.Abs(parameters[0].Am) > Tol ? parameters[0].Am : parameters[1].At;
            flags.Am[0] = true;
        }
    }

    // 【C原典】fabs(base)>99999→0、>TOL→base、他→fallback。
    private static double SelectPreferred(double baseValue, double fallback)
    {
        if (Math.Abs(baseValue) > MaxValidValue)
        {
            return 0.0;
        }

        return Math.Abs(baseValue) > Tol ? baseValue : fallback;
    }

    // 【C原典】上位 AT: fabs(epaat)>99999→0、>TOL→epaat、epaw1>TOL→Change_W_AT、他→sep[2].epaat。
    private static double SelectAtWithWattFallback(
        NumericElectricalParameters[] parameters, SelectionWorkParameters work)
    {
        if (Math.Abs(parameters[0].At) > MaxValidValue)
        {
            return 0.0;
        }

        if (Math.Abs(parameters[0].At) > Tol)
        {
            return parameters[0].At;
        }

        if (Math.Abs(parameters[0].W1) > Tol)
        {
            return WattToAmpereConverter.Convert(parameters[0].W1, work.PhaseCount, work.CircuitVoltage);
        }

        return parameters[2].At;
    }

    // 【C原典】yo が ELB/ELMB/RELB/RELMB(6バイト)か。
    private static bool IsElb(string reservedWord)
        => FixedEquals(reservedWord, "ELB", ReservedWordWidth)
        || FixedEquals(reservedWord, "ELMB", ReservedWordWidth)
        || FixedEquals(reservedWord, "RELB", ReservedWordWidth)
        || FixedEquals(reservedWord, "RELMB", ReservedWordWidth);

    // 【C原典】yo が HPSB/HSB(6バイト)か。
    private static bool IsHpsbOrHsb(string reservedWord)
        => FixedEquals(reservedWord, "HPSB", ReservedWordWidth)
        || FixedEquals(reservedWord, "HSB", ReservedWordWidth);

    // 【C原典】yo が SB/RMCB/MCB/CP/ELB/HPSB/HSB/RELB(6バイト)なら W→AT 変換を使わず直接 AT 設定。
    private static bool IsAtDirectReservedWord(string reservedWord)
        => FixedEquals(reservedWord, "SB", ReservedWordWidth)
        || FixedEquals(reservedWord, "RMCB", ReservedWordWidth)
        || FixedEquals(reservedWord, "MCB", ReservedWordWidth)
        || FixedEquals(reservedWord, "CP", ReservedWordWidth)
        || FixedEquals(reservedWord, "ELB", ReservedWordWidth)
        || FixedEquals(reservedWord, "HPSB", ReservedWordWidth)
        || FixedEquals(reservedWord, "HSB", ReservedWordWidth)
        || FixedEquals(reservedWord, "RELB", ReservedWordWidth);

    private static string Element(string[] values, int index)
        => index < values.Length ? values[index] ?? string.Empty : string.Empty;

    // 固定幅バイト比較。不足文字は空白扱い(C の空白埋めバッファ memcmp 等価)。
    private static bool FixedEquals(string value, string expected, int width)
    {
        for (int i = 0; i < width; i++)
        {
            char v = i < value.Length ? value[i] : ' ';
            char e = i < expected.Length ? expected[i] : ' ';
            if (v != e)
            {
                return false;
            }
        }

        return true;
    }
}
