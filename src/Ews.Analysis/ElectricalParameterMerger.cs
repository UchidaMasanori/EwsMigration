using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 電気パラメータの入力有無チェックとマージ。【C原典】<c>Fysk0c_Edit_Epara</c>(toku/sekkei/src/Fysk0c.c:66)。
///
/// 特注(特殊)機器の電気パラメータ文字列を組み立てる前段で、上流(2次側)パラメータ ep[2] をベースに、
/// 機器自身のパラメータ ep[0] が入力を持つフィールドだけを上書きした「入れ換え後」パラメータを生成する。
/// C 原典は <c>eparmg_s sep[]</c>(sep[0]=ep[0]・sep[2]=ep[2])を受け取り <c>wep</c> を出力する。
/// 数値フィールドは <see cref="Tol"/> 超で入力あり、区分文字(V2Kbn/VcKbn)は空白以外で入力ありと判定する。
/// </summary>
public static class ElectricalParameterMerger
{
    /// <summary>入力有無判定のしきい値。【C原典】TOL(fyrt808.h) = 0.001。</summary>
    private const double Tol = 0.001;

    /// <summary>
    /// ep[2](上流)をベースに ep[0](機器自身)の入力済みフィールドで上書きしたパラメータを返す。
    /// 【C原典】<c>Fysk0c_Edit_Epara(&amp;sep[0], &amp;wep)</c>(Fysk0c.c:66)。
    /// </summary>
    /// <param name="own">機器自身の電気パラメータ。【C原典】sep[0](=ep[0])。</param>
    /// <param name="upper">上流(2次側)の電気パラメータ。【C原典】sep[2](=ep[2])。マージのベース。</param>
    /// <returns>入れ換え後の電気パラメータ。【C原典】wep。</returns>
    public static NumericElectricalParameters Merge(
        NumericElectricalParameters own,
        NumericElectricalParameters upper)
    {
        ArgumentNullException.ThrowIfNull(own);
        ArgumentNullException.ThrowIfNull(upper);

        // 【C原典】memcpy(wep,&sep[2],sizeof(struct eparmg_s));  ベースは ep[2]。
        NumericElectricalParameters wep = upper.Clone();

        // 【C原典】以降、sep[0](ep[0])が入力を持つフィールドだけを上書き。
        if (own.Ph1 > Tol) wep.Ph1 = own.Ph1;
        if (own.Ph2[0] > Tol) wep.Ph2[0] = own.Ph2[0];
        if (own.Wr1 > Tol) wep.Wr1 = own.Wr1;
        if (own.Wr2[0] > Tol) wep.Wr2[0] = own.Wr2[0];
        if (own.Hz > Tol) wep.Hz = own.Hz;
        if (own.P > Tol) wep.P = own.P;
        if (own.E > Tol) wep.E = own.E;
        if (own.Af > Tol) wep.Af = own.Af;
        if (own.At > Tol) wep.At = own.At;
        if (own.A1 > Tol) wep.A1 = own.A1;
        if (own.A2 > Tol) wep.A2 = own.A2;
        if (own.W1 > Tol) wep.W1 = own.W1;
        if (own.Va > Tol) wep.Va = own.Va;
        if (own.Kvar > Tol) wep.Kvar = own.Kvar;
        if (own.Uf > Tol) wep.Uf = own.Uf;
        if (own.Ma[0] > Tol) wep.Ma[0] = own.Ma[0];
        if (own.Ma[1] > Tol) wep.Ma[1] = own.Ma[1];
        if (own.Ma[2] > Tol) wep.Ma[2] = own.Ma[2];
        if (own.V1[0] > Tol) wep.V1[0] = own.V1[0];
        if (own.V1[1] > Tol) wep.V1[1] = own.V1[1];
        if (own.V1[2] > Tol) wep.V1[2] = own.V1[2];
        if (own.V1Idx > Tol) wep.V1Idx = own.V1Idx;
        if (own.V2[0] > Tol) wep.V2[0] = own.V2[0];
        if (own.V2[1] > Tol) wep.V2[1] = own.V2[1];
        if (own.V2[2] > Tol) wep.V2[2] = own.V2[2];
        if (own.V2Idx > Tol) wep.V2Idx = own.V2Idx;   // 【C原典】epav2idx は char だが >TOL で判定
        if (own.V2Kbn != ' ') wep.V2Kbn = own.V2Kbn;
        if (own.Am > Tol) wep.Am = own.Am;
        if (own.Vc > Tol) wep.Vc = own.Vc;
        if (own.VcKbn != ' ') wep.VcKbn = own.VcKbn;
        if (own.Sset > Tol) wep.Sset = own.Sset;
        if (own.Ss > Tol) wep.Ss = own.Ss;
        if (own.S > Tol) wep.S = own.S;
        if (own.Ac > Tol) wep.Ac = own.Ac;
        if (own.Bc > Tol) wep.Bc = own.Bc;
        if (own.Cc > Tol) wep.Cc = own.Cc;
        if (own.T > Tol) wep.T = own.T;
        if (own.K > Tol) wep.K = own.K;
        if (own.Sq > Tol) wep.Sq = own.Sq;
        if (own.Esq > Tol) wep.Esq = own.Esq;         // 【C原典】改訂<1>
        if (own.C > Tol) wep.C = own.C;
        if (own.Ksu > Tol) wep.Ksu = own.Ksu;
        if (own.Mah > Tol) wep.Mah = own.Mah;
        if (own.O > Tol) wep.O = own.O;
        if (own.W2 > Tol) wep.W2 = own.W2;
        if (own.Ksize > Tol) wep.Ksize = own.Ksize;
        if (own.Cset > Tol) wep.Cset = own.Cset;
        if (own.C1 > Tol) wep.C1 = own.C1;
        if (own.C2 > Tol) wep.C2 = own.C2;
        if (own.Ph2[1] > Tol) wep.Ph2[1] = own.Ph2[1];
        if (own.Wr2[1] > Tol) wep.Wr2[1] = own.Wr2[1];
        if (own.Ma[3] > Tol) wep.Ma[3] = own.Ma[3];

        // 【C原典】epaqty(項番39)・epabn(項番40)はマージ対象外(ep[2]の値を保持)。
        return wep;
    }
}
