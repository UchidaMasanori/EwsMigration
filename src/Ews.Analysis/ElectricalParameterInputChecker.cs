using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 電気パラメータ入力有無チェック(<see cref="ElectricalParameterInputChecker.Check"/>)の結果。
/// </summary>
/// <param name="ParameterNumber">電気パラメータ指定番号(epno)。1:電流系入力あり 2:それ以外。</param>
/// <param name="InputFlags">入力有項目番号 sfg[0..54]。sfg[0]=いずれか入力ありの総括フラグ。</param>
public sealed record ElectricalParameterInput(int ParameterNumber, IReadOnlyList<int> InputFlags);

/// <summary>
/// 電気パラメータの入力有無をチェックし、入力有項目フラグ(sfg)と電気パラメータ指定番号(epno)を求める。
/// 【C原典】Fysk0a_EparInput_Check(toku/sekkei/src/Fysk0a.c:71)。
///   各項目の絶対値が許容誤差 TOL を超えれば「入力あり」として sfg にフラグを立て、
///   電流系(AT/AF/A1/A2/W1/MA0)のいずれかに入力があれば epno=1、なければ epno=2 を返す。
/// </summary>
public static class ElectricalParameterInputChecker
{
    /// <summary>実数一致許容誤差。【C原典】TOL == 0.001。</summary>
    private const double Tolerance = 0.001;

    /// <summary>sfg 配列長。【C原典】CHAR sfg[55]。</summary>
    private const int FlagCount = 55;

    /// <summary>
    /// 電気パラメータの入力有無をチェックする。
    /// 【C原典】Fysk0a_EparInput_Check(sep, sfg) → epno。
    /// </summary>
    /// <param name="parameters">数値変換後の電気パラメータ。【C原典】struct eparmg_s sep。</param>
    public static ElectricalParameterInput Check(NumericElectricalParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        int[] sfg = new int[FlagCount];
        NumericElectricalParameters p = parameters;

        if (Has(p.Ph1)) sfg[1] = 1;
        if (Has(p.Ph2[0])) sfg[2] = 1;
        if (Has(p.Wr1)) sfg[3] = 1;
        if (Has(p.Wr2[0])) sfg[4] = 1;
        if (Has(p.Hz)) sfg[5] = 1;
        if (Has(p.P)) sfg[6] = 1;
        if (Has(p.E)) sfg[7] = 1;
        if (Has(p.Af)) sfg[8] = 1;
        if (Has(p.At)) sfg[9] = 1;
        if (Has(p.A1)) sfg[10] = 1;
        if (Has(p.A2)) sfg[11] = 1;
        if (Has(p.W1)) sfg[12] = 1;
        if (Has(p.Va)) sfg[13] = 1;
        if (Has(p.Kvar)) sfg[14] = 1;
        if (Has(p.Uf)) sfg[15] = 1;
        if (Has(p.Ma[0])) sfg[16] = 1;
        if (Has(p.Ma[1])) sfg[17] = 1;
        if (Has(p.Ma[2])) sfg[18] = 1;
        if (Has(p.V1[0])) sfg[19] = 1;
        if (Has(p.V1[1])) sfg[20] = 1;
        if (Has(p.V1[2])) sfg[21] = 1;
        if (Has(p.V1Idx)) sfg[22] = 1;
        if (Has(p.V2[0])) sfg[23] = 1;
        if (Has(p.V2[1])) sfg[24] = 1;
        if (Has(p.V2[2])) sfg[25] = 1;
        if (Has(p.V2Idx)) sfg[26] = 1;   // 【C原典】fabs(epav2idx)。char を数値として評価。
        if (p.V2Kbn != ' ') sfg[27] = 1;
        if (Has(p.Am)) sfg[28] = 1;
        if (Has(p.Vc)) sfg[29] = 1;
        if (p.VcKbn != ' ') sfg[30] = 1;
        if (Has(p.Sset)) sfg[31] = 1;
        if (Has(p.Ss)) sfg[32] = 1;
        if (Has(p.S)) sfg[33] = 1;
        if (Has(p.Ac)) sfg[34] = 1;
        if (Has(p.Bc)) sfg[35] = 1;
        if (Has(p.Cc)) sfg[36] = 1;
        if (Has(p.T)) sfg[37] = 1;
        if (Has(p.K)) sfg[38] = 1;
        // 【C原典】sfg[39](epaqty)・sfg[40](epabn) はコメントアウトされているため対象外。
        if (Has(p.Sq)) sfg[41] = 1;
        if (Has(p.C)) sfg[42] = 1;
        if (Has(p.Ksu)) sfg[43] = 1;
        if (Has(p.Mah)) sfg[44] = 1;
        if (Has(p.O)) sfg[45] = 1;
        if (Has(p.W2)) sfg[46] = 1;
        if (Has(p.Ksize)) sfg[47] = 1;
        if (Has(p.Cset)) sfg[48] = 1;
        if (Has(p.C1)) sfg[49] = 1;
        if (Has(p.C2)) sfg[50] = 1;
        if (Has(p.Ph2[1])) sfg[51] = 1;
        if (Has(p.Wr2[1])) sfg[52] = 1;
        if (Has(p.Ma[3])) sfg[53] = 1;

        // 【C原典】いずれか 1 項目でも入力があれば sfg[0]=1。
        for (int i = 0; i < 54; i++)
        {
            if (sfg[i] == 1)
            {
                sfg[0] = 1;
                break;
            }
        }

        // 【C原典】電流系(AT/AF/A1/A2/W1/MA0)のいずれかに入力があれば epno=1。
        int epno = 2;
        if (p.At > Tolerance || p.Af > Tolerance || p.A1 > Tolerance ||
            p.A2 > Tolerance || p.W1 > Tolerance || p.Ma[0] > Tolerance)
        {
            epno = 1;
        }

        return new ElectricalParameterInput(epno, sfg);
    }

    private static bool Has(double value) => Math.Abs(value) > Tolerance;

    // 【C原典】fabs(char)：char を数値コードとして評価(既定 '\0'==0 は入力なし)。
    private static bool Has(char value) => Math.Abs((double)value) > Tolerance;
}
