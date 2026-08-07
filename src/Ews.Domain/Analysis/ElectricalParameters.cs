namespace Ews.Domain.Analysis;

/// <summary>
/// 1‹@Ší•ª‚Ì“d‹Cƒpƒ‰ƒ[ƒ^(®Œ`Ï‚İŒÅ’è’·)ByCŒ´“Tz<c>struct eparmg</c>(toku/include/common/fycommon.h)B
///
/// <c>eparm_set</c>(Fyss1f.c:2208)‚ª <see cref="RatingValues"/>(union fyrt811 / key_tbl)‚Ì
/// ŒŸØÏ‚İ’l‚ğ—\–ñŒê•Ê‚ÉŒÅ’è’·‚Ì”’l•¶š—ñ‚Ö®Œ`‚µ‚ÄŠi”[‚·‚éo—Í\‘¢B
/// C Œ´“T‚Å‚Í <c>syukairo</c>(FYDF806)‚Ìˆê•”‚Æ‚µ‚Ä <c>Main_Area_Clear</c> ‚ª‘S‘Ì‚ğ '0' ‚Å–„‚ß‚é‚½‚ßA
/// –{ˆÚA‚Å‚àŠeƒtƒB[ƒ‹ƒh‚ğ•‚Ô‚ñ‚Ì '0' ‚Å‰Šú‰»‚·‚é(eparm_set ‚ªG‚ê‚È‚¢ƒtƒB[ƒ‹ƒh‚Í '0' ‚Ì‚Ü‚Ü)B
/// ”z—ñƒtƒB[ƒ‹ƒh(epaph2/epawr2/epama/epav1/epav2)‚Í—v‘f‚²‚Æ‚É•‚Ô‚ñ‚Ì '0' •¶š—ñ‚ğ‚ÂB
///
/// ’l‚Ì®Œ`‚Í <c>Ews.Analysis.EquipmentParameterFormatter</c>(yCŒ´“Tzeparm_set/set_9)‚ªs‚¤B
/// </summary>
public sealed class ElectricalParameters
{
    private static string Zero(int width) => new('0', width);

    private static string[] ZeroArray(int count, int width)
    {
        string[] a = new string[count];
        for (int i = 0; i < count; i++)
        {
            a[i] = Zero(width);
        }
        return a;
    }

    /// <summary>‘Š”‚P(‚o‚g‚P)ByCŒ´“Tzepaph1(1)BTR ‚Ì1Ÿ‘¤‘Š”B</summary>
    public string Ph1 { get; set; } = Zero(1);

    /// <summary>‘Š”‚Q(‚o‚g‚Q)ByCŒ´“Tzepaph2[2](Še1)B</summary>
    public string[] Ph2 { get; set; } = ZeroArray(2, 1);

    /// <summary>ü®‚P(‚v‚q‚P)ByCŒ´“Tzepawr1(1)BTR ‚Ì1Ÿ‘¤ü®B</summary>
    public string Wr1 { get; set; } = Zero(1);

    /// <summary>ü®‚Q(‚v‚q‚Q)ByCŒ´“Tzepawr2[2](Še1)B</summary>
    public string[] Wr2 { get; set; } = ZeroArray(2, 1);

    /// <summary>ü”g”(‚g‚y)ByCŒ´“Tzepahz[2]B</summary>
    public string Hz { get; set; } = Zero(2);

    /// <summary>‹É”(‚o)ByCŒ´“Tzepap[3]B</summary>
    public string P { get; set; } = Zero(3);

    /// <summary>ƒGƒŒƒƒ“ƒg”(‚d)ByCŒ´“Tzepae(1)B</summary>
    public string E { get; set; } = Zero(1);

    /// <summary>ƒtƒŒ[ƒ€“d—¬(‚`‚e).999ByCŒ´“Tzepaaf[9]B</summary>
    public string Af { get; set; } = Zero(9);

    /// <summary>ƒgƒŠƒbƒv“d—¬(‚`‚s).999ByCŒ´“Tzepaat[9]B</summary>
    public string At { get; set; } = Zero(9);

    /// <summary>’èŠi“d—¬‚P(‚`‚P).999ByCŒ´“Tzepaa1[9]BWH/CT/AM ‚Ì1Ÿ‘¤“d—¬B</summary>
    public string A1 { get; set; } = Zero(9);

    /// <summary>’èŠi“d—¬‚Q(‚`‚Q).999ByCŒ´“Tzepaa2[9]B</summary>
    public string A2 { get; set; } = Zero(9);

    /// <summary>•‰‰×—e—Ê(‚v).99ByCŒ´“Tzepaw1[10]B</summary>
    public string W1 { get; set; } = Zero(10);

    /// <summary>•‰‰×—e—Ê(‚u‚`).99ByCŒ´“Tzepava[10]B</summary>
    public string Va { get; set; } = Zero(10);

    /// <summary>’èŠi—e—Ê(‚j‚u‚`‚q).99ByCŒ´“Tzepakvar[6]B</summary>
    public string Kvar { get; set; } = Zero(6);

    /// <summary>Ã“d—e—Ê(‚t‚e).9ByCŒ´“Tzepauf[8]B</summary>
    public string Uf { get; set; } = Zero(8);

    /// <summary>Š´“x“d—¬(‚l‚`)ByCŒ´“Tzepama[4][4](Še4)B</summary>
    public string[] Ma { get; set; } = ZeroArray(4, 4);

    /// <summary>’èŠi“dˆ³1(‚u‚P).9ByCŒ´“Tzepav1[3][8](Še8)BTR/VT/VM/RTR/WH ‚Ì1Ÿ‘¤“dˆ³B</summary>
    public string[] V1 { get; set; } = ZeroArray(3, 8);

    /// <summary>ƒ^ƒbƒv“dˆ³g—pƒCƒ“ƒfƒbƒNƒX(’èŠi“dˆ³1)ByCŒ´“Tzepav1idx(1)B</summary>
    public string V1Idx { get; set; } = Zero(1);

    /// <summary>’èŠi“dˆ³2(‚u‚Q).9ByCŒ´“Tzepav2[3][8](Še8)B</summary>
    public string[] V2 { get; set; } = ZeroArray(3, 8);

    /// <summary>ƒ^ƒbƒv“dˆ³g—pƒCƒ“ƒfƒbƒNƒX(’èŠi“dˆ³2)ByCŒ´“Tzepav2idx(1)B</summary>
    public string V2Idx { get; set; } = Zero(1);

    /// <summary>’èŠi“dˆ³2 ‚`‚b^‚c‚b‹æ•ª 'A':AC 'D':DCByCŒ´“Tzepav2kbn(1)B</summary>
    public char V2Kbn { get; set; } = '0';

    /// <summary>ƒ[ƒ^[’èŠi(‚`‚l)ByCŒ´“Tzepaam[3]B</summary>
    public string Am { get; set; } = Zero(3);

    /// <summary>§Œä“dˆ³(‚u‚b)ByCŒ´“Tzepavc[3]B</summary>
    public string Vc { get; set; } = Zero(3);

    /// <summary>§Œä“dˆ³ ‚`‚b^‚c‚b‹æ•ª 'A':AC 'D':DCByCŒ´“Tzepavckbn(1)B</summary>
    public char VcKbn { get; set; } = '0';

    /// <summary>ƒZƒbƒgŠÔ(‚r‚r‚d‚s).999ByCŒ´“Tzepasset[13]B</summary>
    public string Sset { get; set; } = Zero(13);

    /// <summary>İ’è”ÍˆÍŠÔ(‚r^).999ByCŒ´“Tzepass[13]B</summary>
    public string Ss { get; set; } = Zero(13);

    /// <summary>İ’è”ÍˆÍŠÔ(‚r).999ByCŒ´“Tzepas[13]B</summary>
    public string S { get; set; } = Zero(13);

    /// <summary>‚Ú“_”(‚`‚b)ByCŒ´“Tzepaac[2]B</summary>
    public string Ac { get; set; } = Zero(2);

    /// <summary>‚‚Ú“_”(‚a‚b)ByCŒ´“Tzepabc[2]B</summary>
    public string Bc { get; set; } = Zero(2);

    /// <summary>‚ƒÚ“_”(‚b‚b)ByCŒ´“Tzepacc[2]B</summary>
    public string Cc { get; set; } = Zero(2);

    /// <summary>”ÂŒú(‚s).9ByCŒ´“Tzepat[5]B</summary>
    public string T { get; set; } = Zero(5);

    /// <summary>‰ñ˜H”(‚j)ByCŒ´“Tzepak[3]B</summary>
    public string K { get; set; } = Zero(3);

    /// <summary>è”z”—Ê(‚p‚s‚x)ByCŒ´“Tzepaqty(1)BVT/F/CT ‚Ég—pB</summary>
    public char Qty { get; set; } = '0';

    /// <summary>”Õí—Ş(‚a‚m)ByCŒ´“Tzepabn(1)B</summary>
    public char Bn { get; set; } = '0';

    /// <summary>“düƒTƒCƒY(‚r‚p).99ByCŒ´“Tzepasq[6]B</summary>
    public string Sq { get; set; } = Zero(6);

    /// <summary>ƒA|ƒX•”“düƒTƒCƒY(‚r‚p).99ByCŒ´“Tzepaesq[6]B</summary>
    public string Esq { get; set; } = Zero(6);

    /// <summary>c”(‚b)ByCŒ´“Tzepac(1)BsíP•ª‚Ég—pB</summary>
    public char C { get; set; } = '0';

    /// <summary>‰ñü”ByCŒ´“Tzepaksu(1)BsíP•ª‚Ég—pB</summary>
    public char Ksu { get; set; } = '0';

    /// <summary>’èŠi“d—¬(‚l‚`‚g)ByCŒ´“Tzepamah[5]B</summary>
    public string Mah { get; set; } = Zero(5);

    /// <summary>’ïR’l(‚n).9ByCŒ´“Tzepao[6]B</summary>
    public string O { get; set; } = Zero(6);

    /// <summary>•(‚v)ByCŒ´“Tzepaw2[3]B</summary>
    public string W2 { get; set; } = Zero(3);

    /// <summary>ŒaƒTƒCƒY.9ByCŒ´“Tzepaksize[5]B</summary>
    public string Ksize { get; set; } = Zero(5);

    /// <summary>ƒZƒbƒg‰·“x(‚b‚r‚d‚s)ByCŒ´“Tzepacset[3]B</summary>
    public string Cset { get; set; } = Zero(3);

    /// <summary>İ’è”ÍˆÍ‰·“x(‚b^)ByCŒ´“Tzepac1[3]B</summary>
    public string C1 { get; set; } = Zero(3);

    /// <summary>İ’è”ÍˆÍ‰·“x(‚b)ByCŒ´“Tzepac2[3]B</summary>
    public string C2 { get; set; } = Zero(3);

    /// <summary>
    /// ‘¼‚Ì“d‹Cƒpƒ‰ƒ[ƒ^‚Ì‘SƒtƒB[ƒ‹ƒh‚ğ©g‚Ö’lƒRƒs[‚·‚é(deep copy)B
    /// yCŒ´“Tzmemcpy ‚É‚æ‚é struct eparmg ‚Ì’l•¡Ê‚É‘Š“–B”z—ñƒtƒB[ƒ‹ƒh‚Í—v‘f•¡»‚·‚éB
    /// </summary>
    public void CopyFrom(ElectricalParameters src)
    {
        Ph1 = src.Ph1;
        Ph2 = (string[])src.Ph2.Clone();
        Wr1 = src.Wr1;
        Wr2 = (string[])src.Wr2.Clone();
        Hz = src.Hz;
        P = src.P;
        E = src.E;
        Af = src.Af;
        At = src.At;
        A1 = src.A1;
        A2 = src.A2;
        W1 = src.W1;
        Va = src.Va;
        Kvar = src.Kvar;
        Uf = src.Uf;
        Ma = (string[])src.Ma.Clone();
        V1 = (string[])src.V1.Clone();
        V1Idx = src.V1Idx;
        V2 = (string[])src.V2.Clone();
        V2Idx = src.V2Idx;
        V2Kbn = src.V2Kbn;
        Am = src.Am;
        Vc = src.Vc;
        VcKbn = src.VcKbn;
        Sset = src.Sset;
        Ss = src.Ss;
        S = src.S;
        Ac = src.Ac;
        Bc = src.Bc;
        Cc = src.Cc;
        T = src.T;
        K = src.K;
        Qty = src.Qty;
        Bn = src.Bn;
        Sq = src.Sq;
        Esq = src.Esq;
        C = src.C;
        Ksu = src.Ksu;
        Mah = src.Mah;
        O = src.O;
        W2 = src.W2;
        Ksize = src.Ksize;
        Cset = src.Cset;
        C1 = src.C1;
        C2 = src.C2;
    }
}
