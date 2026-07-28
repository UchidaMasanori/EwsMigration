namespace Ews.Domain.Analysis;

/// <summary>
/// 1‹@Ší•ª‚Ì“d‹Cƒpƒ‰ƒ[ƒ^(”’l•ÏŠ·Œã)ByCŒ´“Tz<c>struct eparmg_s</c>(toku/include/sekkei/fyscommon.h:24)B
///
/// <see cref="ElectricalParameters"/>(=eparmg, ®Œ`Ï‚İŒÅ’è’·•¶š—ñ)‚ğ”’l‰»‚µ‚½ double ”Å‚ÅA
/// ‹@Ší‘I’è(Fysk00/Fysk01/Fysk04)‚ª’èŠi’lƒL[¶¬E”äŠrEƒ}ƒXƒ^Æ‡‚Ì“ü—Í‚Æ‚µ‚Ä—p‚¢‚éB
/// C Œ´“T‚Å‚Í <c>eparmg</c>(char) ‚©‚ç <c>eparmg_s</c>(double) ‚Ö‚Ì•ÏŠ·(Fysk0c_Edit_Epara)‚Å¶¬‚³‚ê‚é‚ªA
/// –{ˆÚA‚Å‚Í‚Ü‚¸–{”’lƒ‚ƒfƒ‹‚ÆA‚»‚ê‚ğQÆ‚·‚é’èŠi’lƒL[¶¬(Fysk04_Make_Teikakuchi)‚ğæs®”õ‚·‚éB
///
/// ƒtƒB[ƒ‹ƒh‚Í <c>eparmg_s</c> ‚ÌéŒ¾‡E–¼Ì‚É‘Î‰(yCŒ´“TzƒRƒƒ“ƒg‚ÉŒ³–¼‚ğ•¹‹L)B
/// ‹æ•ªŒn(V2Kbn/VcKbn/Bn)‚Æ V2Idx ‚Í charBŠ´“x“d—¬ <see cref="Ma"/> ‚Í eparmg_s ‚Å‚Í [3] ‚¾‚ªA
/// Fysk00_Get_Datachi ‚Ì€”Ô 53 ‚ª epama[3] ‚ğQÆ‚·‚é(char ”Å eparmg ‚Í epama[4])‚½‚ßA–{ˆÚA‚Å‚Í [4] ‚Å•Û‚·‚éB
/// </summary>
public sealed class NumericElectricalParameters
{
    /// <summary>‘Š”‚P(‚o‚g‚P)ByCŒ´“Tzepaph1BTR ‚Ì1Ÿ‘¤‘Š”B</summary>
    public double Ph1 { get; set; }

    /// <summary>‘Š”‚Q(‚o‚g‚Q)ByCŒ´“Tzepaph2[2]B</summary>
    public double[] Ph2 { get; set; } = new double[2];

    /// <summary>ü®‚P(‚v‚q‚P)ByCŒ´“Tzepawr1BTR ‚Ì1Ÿ‘¤ü®B</summary>
    public double Wr1 { get; set; }

    /// <summary>ü®‚Q(‚v‚q‚Q)ByCŒ´“Tzepawr2[2]B</summary>
    public double[] Wr2 { get; set; } = new double[2];

    /// <summary>ü”g”(‚g‚y)ByCŒ´“TzepahzB</summary>
    public double Hz { get; set; }

    /// <summary>‹É”(‚o)ByCŒ´“TzepapB</summary>
    public double P { get; set; }

    /// <summary>ƒGƒŒƒƒ“ƒg”(‚d)ByCŒ´“TzepaeB</summary>
    public double E { get; set; }

    /// <summary>ƒtƒŒ[ƒ€“d—¬(‚`‚e)ByCŒ´“TzepaafB</summary>
    public double Af { get; set; }

    /// <summary>ƒgƒŠƒbƒv“d—¬(‚`‚s)ByCŒ´“TzepaatB</summary>
    public double At { get; set; }

    /// <summary>’èŠi“d—¬‚P(‚`‚P)ByCŒ´“Tzepaa1BWH/CT/AM ‚Ì1Ÿ‘¤“d—¬B</summary>
    public double A1 { get; set; }

    /// <summary>’èŠi“d—¬‚Q(‚`‚Q)ByCŒ´“Tzepaa2B</summary>
    public double A2 { get; set; }

    /// <summary>•‰‰×—e—Ê(‚v)ByCŒ´“Tzepaw1B</summary>
    public double W1 { get; set; }

    /// <summary>•‰‰×—e—Ê(‚u‚`)ByCŒ´“TzepavaB</summary>
    public double Va { get; set; }

    /// <summary>’èŠi—e—Ê(‚j‚u‚`‚q)ByCŒ´“TzepakvarB</summary>
    public double Kvar { get; set; }

    /// <summary>Ã“d—e—Ê(‚t‚e)ByCŒ´“TzepaufB</summary>
    public double Uf { get; set; }

    /// <summary>Š´“x“d—¬(‚l‚`)ByCŒ´“Tzepama(€”Ô 53 ‚ª epama[3] ‚ğQÆ‚·‚é‚½‚ß [4] ‚Å•Û)B</summary>
    public double[] Ma { get; set; } = new double[4];

    /// <summary>’èŠi“dˆ³1(‚u‚P)ByCŒ´“Tzepav1[3]BTR/VT/VM/RTR/WH ‚Ì1Ÿ‘¤“dˆ³B</summary>
    public double[] V1 { get; set; } = new double[3];

    /// <summary>ƒ^ƒbƒv“dˆ³g—pƒCƒ“ƒfƒbƒNƒX(’èŠi“dˆ³1)ByCŒ´“Tzepav1idxB</summary>
    public double V1Idx { get; set; }

    /// <summary>’èŠi“dˆ³2(‚u‚Q)ByCŒ´“Tzepav2[3]B</summary>
    public double[] V2 { get; set; } = new double[3];

    /// <summary>ƒ^ƒbƒv“dˆ³g—pƒCƒ“ƒfƒbƒNƒX(’èŠi“dˆ³2)ByCŒ´“Tzepav2idxB€”Ô 26 ‚Å”’l‚Æ‚µ‚ÄQÆ‚³‚ê‚éB</summary>
    public char V2Idx { get; set; }

    /// <summary>’èŠi“dˆ³2 ‚`‚b^‚c‚b‹æ•ª 'A':AC 'D':DCByCŒ´“Tzepav2kbnB“ü—Í‚È‚µ‚Í‹ó”’B</summary>
    public char V2Kbn { get; set; } = ' ';

    /// <summary>ƒ[ƒ^[’èŠi(‚`‚l)ByCŒ´“TzepaamB</summary>
    public double Am { get; set; }

    /// <summary>§Œä“dˆ³(‚u‚b)ByCŒ´“TzepavcB</summary>
    public double Vc { get; set; }

    /// <summary>§Œä“dˆ³ ‚`‚b^‚c‚b‹æ•ª 'A':AC 'D':DCByCŒ´“TzepavckbnB“ü—Í‚È‚µ‚Í‹ó”’B</summary>
    public char VcKbn { get; set; } = ' ';

    /// <summary>ƒZƒbƒgŠÔ(‚r‚r‚d‚s)ByCŒ´“TzepassetB</summary>
    public double Sset { get; set; }

    /// <summary>İ’è”ÍˆÍŠÔ(‚r^)ByCŒ´“TzepassB</summary>
    public double Ss { get; set; }

    /// <summary>İ’è”ÍˆÍŠÔ(‚r)ByCŒ´“TzepasB</summary>
    public double S { get; set; }

    /// <summary>‚Ú“_”(‚`‚b)ByCŒ´“TzepaacB</summary>
    public double Ac { get; set; }

    /// <summary>‚‚Ú“_”(‚a‚b)ByCŒ´“TzepabcB</summary>
    public double Bc { get; set; }

    /// <summary>‚ƒÚ“_”(‚b‚b)ByCŒ´“TzepaccB</summary>
    public double Cc { get; set; }

    /// <summary>”ÂŒú(‚s)ByCŒ´“TzepatB</summary>
    public double T { get; set; }

    /// <summary>‰ñ˜H”(‚j)ByCŒ´“TzepakB</summary>
    public double K { get; set; }

    /// <summary>è”z”—Ê(‚p‚s‚x)ByCŒ´“TzepaqtyBVT/F/CT ‚Ég—pB</summary>
    public double Qty { get; set; }

    /// <summary>”Õí—Ş(‚a‚m)ByCŒ´“TzepabnB“ü—Í‚È‚µ‚Í‹ó”’B</summary>
    public char Bn { get; set; } = ' ';

    /// <summary>“düƒTƒCƒY(‚r‚p)ByCŒ´“TzepasqB</summary>
    public double Sq { get; set; }

    /// <summary>ƒA|ƒX•”“düƒTƒCƒY(‚r‚p)ByCŒ´“TzepaesqB</summary>
    public double Esq { get; set; }

    /// <summary>c”(‚b)ByCŒ´“TzepacBsíP•ª‚Ég—pB</summary>
    public double C { get; set; }

    /// <summary>‰ñü”ByCŒ´“TzepaksuBsíP•ª‚Ég—pB</summary>
    public double Ksu { get; set; }

    /// <summary>’èŠi“d—¬(‚l‚`‚g)ByCŒ´“TzepamahB</summary>
    public double Mah { get; set; }

    /// <summary>’ïR’l(‚n)ByCŒ´“TzepaoB</summary>
    public double O { get; set; }

    /// <summary>•(‚v)ByCŒ´“Tzepaw2B</summary>
    public double W2 { get; set; }

    /// <summary>ŒaƒTƒCƒYByCŒ´“TzepaksizeB</summary>
    public double Ksize { get; set; }

    /// <summary>ƒZƒbƒg‰·“x(‚b‚r‚d‚s)ByCŒ´“TzepacsetB</summary>
    public double Cset { get; set; }

    /// <summary>İ’è”ÍˆÍ‰·“x(‚b^)ByCŒ´“Tzepac1B</summary>
    public double C1 { get; set; }

    /// <summary>İ’è”ÍˆÍ‰·“x(‚b)ByCŒ´“Tzepac2B</summary>
    public double C2 { get; set; }

    /// <summary>
    /// ‘SƒtƒB[ƒ‹ƒh‚ğ•¡»‚µ‚½“Æ—§ƒCƒ“ƒXƒ^ƒ“ƒX‚ğ•Ô‚·B”z—ñƒtƒB[ƒ‹ƒh‚à—v‘f‚ğƒRƒs[‚·‚éB
    /// yCŒ´“Tz<c>memcpy(wep,&amp;sep[2],sizeof(struct eparmg_s))</c>(\‘¢‘Ì‘S‘Ì‚Ì’lƒRƒs[)‚É‘Š“–B
    /// </summary>
    public NumericElectricalParameters Clone()
    {
        return new NumericElectricalParameters
        {
            Ph1 = Ph1,
            Ph2 = (double[])Ph2.Clone(),
            Wr1 = Wr1,
            Wr2 = (double[])Wr2.Clone(),
            Hz = Hz,
            P = P,
            E = E,
            Af = Af,
            At = At,
            A1 = A1,
            A2 = A2,
            W1 = W1,
            Va = Va,
            Kvar = Kvar,
            Uf = Uf,
            Ma = (double[])Ma.Clone(),
            V1 = (double[])V1.Clone(),
            V1Idx = V1Idx,
            V2 = (double[])V2.Clone(),
            V2Idx = V2Idx,
            V2Kbn = V2Kbn,
            Am = Am,
            Vc = Vc,
            VcKbn = VcKbn,
            Sset = Sset,
            Ss = Ss,
            S = S,
            Ac = Ac,
            Bc = Bc,
            Cc = Cc,
            T = T,
            K = K,
            Qty = Qty,
            Bn = Bn,
            Sq = Sq,
            Esq = Esq,
            C = C,
            Ksu = Ksu,
            Mah = Mah,
            O = O,
            W2 = W2,
            Ksize = Ksize,
            Cset = Cset,
            C1 = C1,
            C2 = C2,
        };
    }
}
