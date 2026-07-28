using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ®Œ`Ï‚İŒÅ’è’·•¶š—ñ‚Ì“d‹Cƒpƒ‰ƒ[ƒ^(<see cref="ElectricalParameters"/> / eparmg)‚ğ
/// ”’l‰»‚µ‚½ <see cref="NumericElectricalParameters"/>(eparmg_s)‚Ö•ÏŠ·‚·‚éB
///
/// yCŒ´“Tz<c>Fysk01_Change_Epara(struct eparmg *ep, struct eparmg_s *sep)</c>(Fysk01.c:4108)B
/// ‹@Ší‘I’è‚Ì“üŒû <c>Fysk00.c</c> ‚ª <c>for(j=0;j&lt;3;j++) Fysk01_Change_Epara(&amp;epa[j],&amp;sep[j])</c>
/// (Fysk00.c:2018 ‚Ù‚©)‚Å©‹@/ãˆÊ/‰ºˆÊ‚Ì 3 ‘g‚ğ”’l‰»‚µA’èŠi’lƒL[¶¬(Fysk04_Make_Teikakuchi)‚â
/// ƒ}[ƒW(Fysk0c_Edit_Epara)‚Ì“ü—Í‚Æ‚·‚éB
///
/// Še”’lƒtƒB[ƒ‹ƒh‚Í <c>Stof(æ“ª size •¶š‚ğ atof)</c> ‚Å•ÏŠ·‚µA‹æ•ª•¶š
/// (<c>epav2kbn</c>/<c>epavckbn</c>/<c>epabn</c>)‚ÍŒ´“T‚Ç‚¨‚è char ‚ğ‚»‚Ì‚Ü‚Ü•¡Ê‚·‚éB
/// ƒ^ƒbƒvƒCƒ“ƒfƒbƒNƒX <c>epav2idx</c> ‚ÍŒ´“T‚Å double¨char ‘ã“ü‚Æ‚È‚é‚½‚ß®”‚ÖØ‚èÌ‚Ä‚ÄŠi”[‚·‚éB
/// </summary>
public static class ElectricalParameterConverter
{
    /// <summary>
    /// <paramref name="source"/>(eparmg)‚ğ”’l•ÏŠ·‚µAV‚µ‚¢ <see cref="NumericElectricalParameters"/>
    /// (eparmg_s)‚ğ•Ô‚·ByCŒ´“TzFysk01_Change_Epara ‚ÌéŒ¾‡E•E•ÏŠ·•û®‚É’‰À‚É‘Î‰‚·‚éB
    /// </summary>
    public static NumericElectricalParameters Convert(ElectricalParameters source)
    {
        ArgumentNullException.ThrowIfNull(source);

        NumericElectricalParameters result = new()
        {
            // ‘Š”‚P(‚o‚g‚P)  epaph1(1)
            Ph1 = Stof(source.Ph1, 1),
            // ü®‚P(‚v‚q‚P)  epawr1(1)
            Wr1 = Stof(source.Wr1, 1),
            // ü”g”(‚g‚y)    epahz(2)
            Hz = Stof(source.Hz, 2),
            // ‹É”(‚o)        epap(3)
            P = Stof(source.P, 3),
            // ƒGƒŒƒƒ“ƒg”(‚d) epae(1)
            E = Stof(source.E, 1),
            // ƒtƒŒ[ƒ€“d—¬(‚`‚e) epaaf(9)
            Af = Stof(source.Af, 9),
            // ƒgƒŠƒbƒv“d—¬(‚`‚s) epaat(9)
            At = Stof(source.At, 9),
            // ’èŠi“d—¬‚P(‚`‚P) epaa1(9)
            A1 = Stof(source.A1, 9),
            // ’èŠi“d—¬‚Q(‚`‚Q) epaa2(9)
            A2 = Stof(source.A2, 9),
            // •‰‰×—e—Ê(‚v)    epaw1(10)
            W1 = Stof(source.W1, 10),
            // •‰‰×—e—Ê(‚u‚`)  epava(10)
            Va = Stof(source.Va, 10),
            // ’èŠi—e—Ê(‚j‚u‚`‚q) epakvar(6)
            Kvar = Stof(source.Kvar, 6),
            // Ã“d—e—Ê(‚t‚e)  epauf(8)
            Uf = Stof(source.Uf, 8),
            // ƒ^ƒbƒvƒCƒ“ƒfƒbƒNƒX(’èŠi“dˆ³1) epav1idx(1) : double
            V1Idx = Stof(source.V1Idx, 1),
            // ƒ^ƒbƒvƒCƒ“ƒfƒbƒNƒX(’èŠi“dˆ³2) epav2idx(1) : Œ´“T‚Í double¨char ‘ã“ü‚Ì‚½‚ß®”‚ÖØ‚èÌ‚Ä
            V2Idx = (char)(int)Stof(source.V2Idx, 1),
            // ’èŠi“dˆ³2 ‚`‚b^‚c‚b‹æ•ª epav2kbn : char ‚ğ‚»‚Ì‚Ü‚Ü•¡Ê
            V2Kbn = source.V2Kbn,
            // ƒ[ƒ^[’èŠi(‚`‚l) epaam(3)
            Am = Stof(source.Am, 3),
            // §Œä“dˆ³(‚u‚b)  epavc(3)
            Vc = Stof(source.Vc, 3),
            // §Œä“dˆ³ ‚`‚b^‚c‚b‹æ•ª epavckbn : char ‚ğ‚»‚Ì‚Ü‚Ü•¡Ê
            VcKbn = source.VcKbn,
            // ƒZƒbƒgŠÔ(‚r‚r‚d‚s) epasset(13)
            Sset = Stof(source.Sset, 13),
            // İ’è”ÍˆÍŠÔ(‚r^) epass(13)
            Ss = Stof(source.Ss, 13),
            // İ’è”ÍˆÍŠÔ(‚r) epas(13)
            S = Stof(source.S, 13),
            // ‚Ú“_”(‚`‚b)  epaac(2)
            Ac = Stof(source.Ac, 2),
            // ‚‚Ú“_”(‚a‚b)  epabc(2)
            Bc = Stof(source.Bc, 2),
            // ‚ƒÚ“_”(‚b‚b)  epacc(2)
            Cc = Stof(source.Cc, 2),
            // ”ÂŒú(‚s)        epat(5)
            T = Stof(source.T, 5),
            // ‰ñ˜H”(‚j)      epak(3)
            K = Stof(source.K, 3),
            // è”z”—Ê(‚p‚s‚x) epaqty(1) : char 1 Œ…
            Qty = Stof(source.Qty.ToString(), 1),
            // ”Õí—Ş(‚a‚m)    epabn : char ‚ğ‚»‚Ì‚Ü‚Ü•¡Ê
            Bn = source.Bn,
            // “düƒTƒCƒY(‚r‚p) epasq(6)
            Sq = Stof(source.Sq, 6),
            // ƒA|ƒX•”“düƒTƒCƒY(‚r‚p) epaesq(6)
            Esq = Stof(source.Esq, 6),
            // c”(‚b)        epac(1) : char 1 Œ…
            C = Stof(source.C.ToString(), 1),
            // ‰ñü”          epaksu(1) : char 1 Œ…
            Ksu = Stof(source.Ksu.ToString(), 1),
            // ’èŠi“d—¬(‚l‚`‚g) epamah(5)
            Mah = Stof(source.Mah, 5),
            // ’ïR’l(‚n)      epao(6)
            O = Stof(source.O, 6),
            // •(‚v)          epaw2(3)
            W2 = Stof(source.W2, 3),
            // ŒaƒTƒCƒY        epaksize(5)
            Ksize = Stof(source.Ksize, 5),
            // ƒZƒbƒg‰·“x(‚b‚r‚d‚s) epacset(3)
            Cset = Stof(source.Cset, 3),
            // İ’è”ÍˆÍ‰·“x(‚b^) epac1(3)
            C1 = Stof(source.C1, 3),
            // İ’è”ÍˆÍ‰·“x(‚b) epac2(3)
            C2 = Stof(source.C2, 3),
        };

        // ‘Š”‚Q(‚o‚g‚Q) epaph2[2](Še1)
        for (int j = 0; j < 2; j++)
        {
            result.Ph2[j] = Stof(source.Ph2[j], 1);
        }

        // ü®‚Q(‚v‚q‚Q) epawr2[2](Še1)
        for (int j = 0; j < 2; j++)
        {
            result.Wr2[j] = Stof(source.Wr2[j], 1);
        }

        // Š´“x“d—¬(‚l‚`) epama[4](Še4)
        // yCŒ´“Tzeparmg_s.epama ‚Í [3] éŒ¾‚¾‚ª Change_Epara ‚Í j<4 ‚Å epama[3] ‚Ü‚Å‘‚«‚Ş‚½‚ß
        // –{ˆÚA‚Å‚Í [4] ‚Å•Û‚·‚é(NumericElectricalParameters.Ma ‚Æ‘Î‰)B
        for (int j = 0; j < 4; j++)
        {
            result.Ma[j] = Stof(source.Ma[j], 4);
        }

        // ’èŠi“dˆ³1(‚u‚P)/’èŠi“dˆ³2(‚u‚Q) epav1[3]/epav2[3](Še8)
        for (int j = 0; j < 3; j++)
        {
            result.V1[j] = Stof(source.V1[j], 8);
            result.V2[j] = Stof(source.V2[j], 8);
        }

        return result;
    }

    /// <summary>
    /// yCŒ´“Tz<c>Stof(CHAR* str, SHORT size)</c>(Fysk09.c:10)‚Ö‚ÌˆÏ÷B
    /// </summary>
    private static double Stof(string? str, int size) => EquipmentParameterFormatter.Stof(str, size);
}
