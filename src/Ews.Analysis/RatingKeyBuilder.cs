using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ’èŠi’lƒL[(kteichi 50ƒoƒCƒg)‚Ì¶¬ByCŒ´“Tz<c>Fysk04_Make_Teikakuchi</c>(toku/sekkei/src/Fysk04.c:41)
/// ‚¨‚æ‚Ñ <c>Fysk00_Get_Datachi</c>(toku/sekkei/src/Fysk00.c:3562)B
///
/// —\–ñŒê•Ê‚Ì <see cref="RatingKeyTableEntry"/> ƒe[ƒuƒ‹‚ÆA”’l‰»Ï‚İ“d‹Cƒpƒ‰ƒ[ƒ^
/// <see cref="NumericElectricalParameters"/>(=eparmg_s)‚©‚çA‹@Ší‘I’è‚Ìƒ}ƒXƒ^Æ‡ƒL[‚Æ‚È‚é
/// ŒÅ’è’·50ƒoƒCƒg•¶š—ñ‚ğ‘g‚İ—§‚Ä‚éBŠe€–Ú‚Í <see cref="NumericConverter.PowerOfTen"/> ‚ÅƒXƒP[ƒ‹‚µ‚½ŒãA
/// •‚Ô‚ñƒ[ƒ–„‚ß‚µ‚½®”•\Œ»‚Æ‚µ‚Ä˜AŒ‹‚µA—]‚è‚Í‹ó”’‚Å–„‚ß‚éB
/// </summary>
public static class RatingKeyBuilder
{
    /// <summary>’èŠi’lƒL[‚Ì‘S’·ByCŒ´“Tzmemcpy(tc,&amp;str[0],50) ‚Ì 50B</summary>
    public const int KeyLength = 50;

    /// <summary>‘I‘ğ“Áê‹æ•ª: ˆÈ~‚ğ‘ÅØ‚èByCŒ´“Tzs_toku == -3B</summary>
    private const short SelectBreak = -3;

    /// <summary>‘I‘ğ“Áê‹æ•ª: “–ŠYsƒXƒLƒbƒvByCŒ´“Tzs_toku == -2B</summary>
    private const short SelectSkip = -2;

    /// <summary>‘I‘ğ“Áê‹æ•ª: ‹æ•ª“Çæ‚è(AC/DC)ByCŒ´“Tzs_toku == -1B</summary>
    private const short SelectKindSwitch = -1;

    /// <summary>‹æ•ªÆ‡—p‚Ì•¶šW‡ "AD"(A=AC, D=DC)ByCŒ´“Tzstatic CHAR kbn[3]="AD"(Fysk04.c:44)B</summary>
    private const string KindChars = "AD";

    /// <summary>
    /// Fysk00_Get_Datachi / Fysk04_Make_Teikakuchi ‚ªó‚¯“n‚·1€–Ú•ª‚Ì’lB
    /// yCŒ´“Tz<c>IFC.su</c>(union {DOUBLE fsu; CHAR csu[8];}Afyrt814.h)B
    /// ”’l€–Ú‚Í <see cref="Numeric"/>A‹æ•ª€–Ú(€”Ô 27/30/40)‚Í <see cref="Char"/> ‚ğ—p‚¢‚éB
    /// </summary>
    public readonly record struct DataValue(double Numeric, char Char);

    /// <summary>
    /// ’èŠi’lƒL[(50ƒoƒCƒg)‚ğ¶¬‚·‚éByCŒ´“Tz<c>Fysk04_Make_Teikakuchi</c>(Fysk04.c:41)B
    ///
    /// ƒe[ƒuƒ‹‚ğæ“ª‚©‚ç‘–¸‚µAI’[(<see cref="RatingKeyTableEntry.IsEnd"/>)‚Ü‚½‚Í
    /// ‘ÅØ‚è‹æ•ª(s_toku == -3)‚Å’â~‚·‚éBƒXƒLƒbƒv‹æ•ª(s_toku == -2)‚Ìs‚Í–³‹‚µA
    /// ‹æ•ª“Çæ‚è(s_toku == -1)‚Ìs‚Å‚Íæ“¾’l‚Ìæ“ª•¶š‚ğ "AD" ‚ÆÆ‡‚µ‚ÄÌ—p‹æ•ª n ‚ğŒˆ‚ß‚éB
    /// íÌ—p(s_toku == 0)‚Ü‚½‚Í‹æ•ªˆê’v(s_toku == n)‚Ìs‚Ì‚İA’l‚ğ 10^d_len ‚ÅƒXƒP[ƒ‹‚µ‚Ä
    /// • len ‚Éƒ[ƒ–„‚ß‚µ‚½®”•\Œ»‚Å˜AŒ‹‚·‚éBc—]‚Í‹ó”’‚Å–„‚ß‚éB
    /// </summary>
    /// <param name="table">—\–ñŒê•Ê‚Ì’èŠi’l•ÒWƒe[ƒuƒ‹ByCŒ´“TzTCHI_T t[]B</param>
    /// <param name="parameters">”’l‰»Ï‚İ“d‹Cƒpƒ‰ƒ[ƒ^ByCŒ´“Tzstruct eparmg_s *sepB</param>
    /// <returns>50ƒoƒCƒg‚Ì’èŠi’lƒL[•¶š—ñB</returns>
    public static string MakeRatingKey(RatingKeyTableEntry[] table, NumericElectricalParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(parameters);

        // yCŒ´“TzCHAR str[...] ‚ğ–Í‚µ‚½ŒÅ’èƒoƒbƒtƒ@Bsprintf ‚Í•‚ğ’´‚¦‚Ä‚à‘‚Ş‚ªA
        // kk ‚Í len ‚Ô‚ñ‚¾‚¯i‚Ş‚½‚ßAC ‚Æ“¯‚¶ã‘‚«‹““®‚ğÄŒ»‚·‚éB
        char[] buffer = new char[KeyLength + 16];
        int position = 0;   // yCŒ´“Tzkk
        short matchedKind = 0;   // yCŒ´“Tzn

        foreach (RatingKeyTableEntry entry in table)
        {
            // yCŒ´“Tzif(t[i].len == -1 || t[i].s_toku == -3) break;
            if (entry.IsEnd || entry.SelectFlag == SelectBreak)
            {
                break;
            }

            // yCŒ´“Tzelse if(t[i].s_toku != -2)  c -2 ‚Í“–ŠYsƒXƒLƒbƒv
            if (entry.SelectFlag == SelectSkip)
            {
                continue;
            }

            DataValue value = GetDataValue(entry.ItemNo, parameters);

            if (entry.SelectFlag == SelectKindSwitch)
            {
                // yCŒ´“Tzfor(j=0;j<2;j++) if(ifc.su.csu[0]==kbn[j]){ n=j+1; break; }
                int kindIndex = KindChars.IndexOf(value.Char);
                if (kindIndex >= 0)
                {
                    matchedKind = (short)(kindIndex + 1);
                }
            }
            else if (entry.SelectFlag == 0 || entry.SelectFlag == matchedKind)
            {
                // yCŒ´“Tzaa = ifc.su.fsu * Ketaawase(t[i].d_len);
                double scaled = value.Numeric * NumericConverter.PowerOfTen(entry.DecimalScale);

                // yCŒ´“Tzsprintf(frm,"%%0%3.1ff",(FLOAT)len); sprintf(&str[kk],frm,aa);
                //          ¨ "%0<len>.0f"(• lenE¬”0Œ…Eƒ[ƒ–„‚ß)
                string field = FormatField(scaled, entry.Width);

                for (int j = 0; j < field.Length && position + j < buffer.Length; j++)
                {
                    buffer[position + j] = field[j];
                }
                position += entry.Width;   // yCŒ´“Tzkk += t[i].len;
            }
        }

        // yCŒ´“Tzmemset(&str[kk],' ',50-kk);
        if (position < 0)
        {
            position = 0;
        }
        for (int i = position; i < KeyLength; i++)
        {
            buffer[i] = ' ';
        }

        // yCŒ´“Tzmemcpy(tc,&str[0],50);
        return new string(buffer, 0, KeyLength);
    }

    /// <summary>
    /// ƒXƒP[ƒ‹Ï‚İ‚Ì’l‚ğu• widthE¬”0Œ…Eƒ[ƒ–„‚ßv‚Å®Œ`‚·‚éB
    /// yCŒ´“Tz<c>sprintf(frm,"%%0%3.1ff",(FLOAT)len); sprintf(&amp;str[kk],frm,aa);</c>(Fysk04.c:79-80)B
    /// C ‚Ì <c>%.0f</c> ‚ÍÅ‹ßÚ‹ô”‚ÖŠÛ‚ß‚é‚½‚ß <see cref="MidpointRounding.ToEven"/> ‚ğ—p‚¢‚éB
    /// </summary>
    private static string FormatField(double value, short width)
    {
        long rounded = (long)Math.Round(value, MidpointRounding.ToEven);
        string text = rounded.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return text.Length >= width ? text : text.PadLeft(width, '0');
    }

    /// <summary>‹¤—pî•ñ‚ğ”º‚í‚È‚¢ê‡‚ÌŠù’è’lB€”Ô 1`53 ‚Ì‚İ‚ğæ“¾‚·‚éŒÄo‚µ‚Å—p‚¢‚éB</summary>
    private static readonly NumericSharedInfo EmptySharedInfo = new();

    /// <summary>
    /// “d‹Cƒpƒ‰ƒ[ƒ^‚©‚ç€”Ô‚É‘Î‰‚·‚é’l‚ğæ‚èo‚·(‹¤—pî•ñ‚È‚µ)ByCŒ´“Tz<c>Fysk00_Get_Datachi</c>(Fysk00.c:3562)B
    /// €”Ô 1`53(”’lƒpƒ‰ƒ[ƒ^ eparmg_s)ê—pB€”Ô 61`87(’¼‹ßã‰ºˆÊ‹¤—pî•ñ kyoyojg_s)‚ğ
    /// QÆ‚·‚éê‡‚Í <see cref="GetDataValue(short, NumericElectricalParameters, NumericSharedInfo)"/> ‚ğ—p‚¢‚éB
    /// </summary>
    /// <param name="itemNo">ƒf[ƒ^€”ÔByCŒ´“TzSHORT noB</param>
    /// <param name="p">”’l‰»Ï‚İ“d‹Cƒpƒ‰ƒ[ƒ^ByCŒ´“Tzstruct eparmg_s *sepB</param>
    public static DataValue GetDataValue(short itemNo, NumericElectricalParameters p)
        => GetDataValue(itemNo, p, EmptySharedInfo);

    /// <summary>
    /// “d‹Cƒpƒ‰ƒ[ƒ^E‹¤—pî•ñ‚©‚ç€”Ô‚É‘Î‰‚·‚é’l‚ğæ‚èo‚·ByCŒ´“Tz<c>Fysk00_Get_Datachi</c>(Fysk00.c:3562)B
    /// €”Ô 1`53 ‚Í”’lƒpƒ‰ƒ[ƒ^(eparmg_s)A€”Ô 61`87 ‚Í’¼‹ßã‰ºˆÊ‹¤—pî•ñ(kyoyojg_s)‚ğQÆ‚·‚éB
    ///
    /// yŒ´“T‚Ì’ˆÓ(€”Ô 85)z<c>km_s.kyomad</c> ‚Í <c>DOUBLE[3]</c> éŒ¾‚¾‚ªŒ´“T‚Í
    /// <c>scd.km_s.kyomad[3]</c> ‚ğ“Ç‚ŞB‚±‚Ì“YšŠOƒAƒNƒZƒX‚Í\‘¢‘Ìã‚Å’¼Œã‚É—×Ú‚·‚é
    /// <c>kv1_s.kyov1d1</c> ‚Æ“¯ˆêƒƒ‚ƒŠ‚ğw‚·‚½‚ßA€”Ô 85 ‚Í€”Ô 66(ˆêŸ“dˆ³’l1)‚Æ
    /// í‚É“¯‚¶’l‚É‚È‚éB‚±‚Ì”z—ñŠOQÆ‹““®‚ğ’‰À‚ÉÄŒ»‚·‚éB
    /// </summary>
    /// <param name="itemNo">ƒf[ƒ^€”ÔByCŒ´“TzSHORT noB</param>
    /// <param name="p">”’l‰»Ï‚İ“d‹Cƒpƒ‰ƒ[ƒ^ByCŒ´“Tzstruct eparmg_s *sepB</param>
    /// <param name="scd">”’l‰»Ï‚İ‹¤—pî•ñByCŒ´“Tzstruct kyoyojg_s scdB</param>
    public static DataValue GetDataValue(short itemNo, NumericElectricalParameters p, NumericSharedInfo scd)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(scd);

        return itemNo switch
        {
            1 => Num(p.Ph1),         // yCŒ´“Tzepaph1
            2 => Num(p.Ph2[0]),      // yCŒ´“Tzepaph2[0]
            3 => Num(p.Wr1),         // yCŒ´“Tzepawr1
            4 => Num(p.Wr2[0]),      // yCŒ´“Tzepawr2[0]
            5 => Num(p.Hz),          // yCŒ´“Tzepahz
            6 => Num(p.P),           // yCŒ´“Tzepap
            7 => Num(p.E),           // yCŒ´“Tzepae
            8 => Num(p.Af),          // yCŒ´“Tzepaaf
            9 => Num(p.At),          // yCŒ´“Tzepaat
            10 => Num(p.A1),         // yCŒ´“Tzepaa1
            11 => Num(p.A2),         // yCŒ´“Tzepaa2
            12 => Num(p.W1),         // yCŒ´“Tzepaw1
            13 => Num(p.Va),         // yCŒ´“Tzepava
            14 => Num(p.Kvar),       // yCŒ´“Tzepakvar
            15 => Num(p.Uf),         // yCŒ´“Tzepauf
            16 => Num(p.Ma[0]),      // yCŒ´“Tzepama[0]
            17 => Num(p.Ma[1]),      // yCŒ´“Tzepama[1]
            18 => Num(p.Ma[2]),      // yCŒ´“Tzepama[2]
            19 => Num(p.V1[0]),      // yCŒ´“Tzepav1[0]
            20 => Num(p.V1[1]),      // yCŒ´“Tzepav1[1]
            21 => Num(p.V1[2]),      // yCŒ´“Tzepav1[2]
            22 => Num(p.V1Idx),      // yCŒ´“Tzepav1idx
            23 => Num(p.V2[0]),      // yCŒ´“Tzepav2[0]
            24 => Num(p.V2[1]),      // yCŒ´“Tzepav2[1]
            25 => Num(p.V2[2]),      // yCŒ´“Tzepav2[2]
            26 => Num(p.V2Idx),      // yCŒ´“Tzepav2idx(char ‚ğ”’l‰»)
            27 => Chr(p.V2Kbn),      // yCŒ´“Tzepav2kbn(‹æ•ª)
            28 => Num(p.Am),         // yCŒ´“Tzepaam
            29 => Num(p.Vc),         // yCŒ´“Tzepavc
            30 => Chr(p.VcKbn),      // yCŒ´“Tzepavckbn(‹æ•ª)
            31 => Num(p.Sset),       // yCŒ´“Tzepasset
            32 => Num(p.Ss),         // yCŒ´“Tzepass
            33 => Num(p.S),          // yCŒ´“Tzepas
            34 => Num(p.Ac),         // yCŒ´“Tzepaac
            35 => Num(p.Bc),         // yCŒ´“Tzepabc
            36 => Num(p.Cc),         // yCŒ´“Tzepacc
            37 => Num(p.T),          // yCŒ´“Tzepat
            38 => Num(p.K),          // yCŒ´“Tzepak
            39 => Num(p.Qty),        // yCŒ´“Tzepaqty
            40 => Chr(p.Bn),         // yCŒ´“Tzepabn(‹æ•ª)
            41 => Num(p.Sq),         // yCŒ´“Tzepasq
            42 => Num(p.C),          // yCŒ´“Tzepac
            43 => Num(p.Ksu),        // yCŒ´“Tzepaksu
            44 => Num(p.Mah),        // yCŒ´“Tzepamah
            45 => Num(p.O),          // yCŒ´“Tzepao
            46 => Num(p.W2),         // yCŒ´“Tzepaw2
            47 => Num(p.Ksize),      // yCŒ´“Tzepaksize
            48 => Num(p.Cset),       // yCŒ´“Tzepacset
            49 => Num(p.C1),         // yCŒ´“Tzepac1
            50 => Num(p.C2),         // yCŒ´“Tzepac2
            51 => Num(p.Ph2[1]),     // yCŒ´“Tzepaph2[1]
            52 => Num(p.Wr2[1]),     // yCŒ´“Tzepawr2[1]
            53 => Num(p.Ma[3]),      // yCŒ´“Tzepama[3]

            // ---- ’¼‹ßã‰ºˆÊ‹¤—pî•ñƒf[ƒ^(kyoyojg_s scd) €”Ô 61`87 ----
            61 => Chr(scd.MainPowerSharedAcDc),          // yCŒ´“Tzscd.ksadkbn
            62 => Chr(scd.ControlPowerSharedAcDc),       // yCŒ´“Tzscd.kcadkbn
            63 => Num(At(scd.SensitivityCurrents, 0)),   // yCŒ´“Tzscd.km_s.kyomad[0]
            64 => Num(At(scd.SensitivityCurrents, 1)),   // yCŒ´“Tzscd.km_s.kyomad[1]
            65 => Num(At(scd.SensitivityCurrents, 2)),   // yCŒ´“Tzscd.km_s.kyomad[2]
            66 => Num(At(scd.PrimaryVoltageValues, 0)),  // yCŒ´“Tzscd.kv1_s.kyov1d1
            67 => Chr(AtChar(scd.PrimaryVoltageKinds, 0)),   // yCŒ´“Tzscd.kv1_s.kyov1k1
            68 => Num(At(scd.PrimaryVoltageValues, 1)),  // yCŒ´“Tzscd.kv1_s.kyov1d2
            69 => Chr(AtChar(scd.PrimaryVoltageKinds, 1)),   // yCŒ´“Tzscd.kv1_s.kyov1k2
            70 => Num(At(scd.PrimaryVoltageValues, 2)),  // yCŒ´“Tzscd.kv1_s.kyov1d3
            71 => Num(At(scd.SecondaryVoltageValues, 0)),    // yCŒ´“Tzscd.kv2_s.kyov2d1
            72 => Chr(AtChar(scd.SecondaryVoltageKinds, 0)), // yCŒ´“Tzscd.kv2_s.kyov2k1
            73 => Num(At(scd.SecondaryVoltageValues, 1)),    // yCŒ´“Tzscd.kv2_s.kyov2d2
            74 => Chr(AtChar(scd.SecondaryVoltageKinds, 1)), // yCŒ´“Tzscd.kv2_s.kyov2k2
            75 => Num(At(scd.SecondaryVoltageValues, 2)),    // yCŒ´“Tzscd.kv2_s.kyov2d3
            76 => Chr(AtChar(scd.SecondaryVoltageKinds, 2)), // yCŒ´“Tzscd.kv2_s.kyov2k3
            77 => Num(At(scd.SecondaryVoltageValues, 3)),    // yCŒ´“Tzscd.kv2_s.kyov2d4
            78 => Num(At(scd.ControlVoltageValues, 0)),  // yCŒ´“Tzscd.kvc_s.kyovcd1
            79 => Chr(AtChar(scd.ControlVoltageKinds, 0)),   // yCŒ´“Tzscd.kvc_s.kyovck1
            80 => Num(At(scd.ControlVoltageValues, 1)),  // yCŒ´“Tzscd.kvc_s.kyovcd2
            81 => Chr(AtChar(scd.ControlVoltageKinds, 1)),   // yCŒ´“Tzscd.kvc_s.kyovck2
            82 => Num(At(scd.ControlVoltageValues, 2)),  // yCŒ´“Tzscd.kvc_s.kyovcd3
            83 => Chr(AtChar(scd.ControlVoltageKinds, 2)),   // yCŒ´“Tzscd.kvc_s.kyovck3
            84 => Num(At(scd.ControlVoltageValues, 3)),  // yCŒ´“Tzscd.kvc_s.kyovcd4
            // yCŒ´“Tzscd.km_s.kyomad[3](”z—ñŠOQÆ=kv1_s.kyov1d1 ‚Æ“¯ˆêƒƒ‚ƒŠ)B€”Ô 66 ‚Æ“¯’lB
            85 => Num(At(scd.PrimaryVoltageValues, 0)),
            86 => Num(scd.ControlVoltageRangeFrom),      // yCŒ´“Tzscd.vcfrom
            87 => Num(scd.ControlVoltageRangeTo),        // yCŒ´“Tzscd.vcto

            _ => default,
        };
    }

    private static DataValue Num(double value) => new(value, '\0');

    private static DataValue Chr(char value) => new(0.0, value);

    /// <summary>”’lƒŠƒXƒg‚©‚ç”ÍˆÍ“à‚Ì—v‘f‚ğæ‚èo‚·(”ÍˆÍŠO‚Í 0.0)B</summary>
    private static double At(IReadOnlyList<double> list, int index)
        => index >= 0 && index < list.Count ? list[index] : 0.0;

    /// <summary>‹æ•ªƒŠƒXƒg‚©‚ç”ÍˆÍ“à‚Ì—v‘f‚ğæ‚èo‚·(”ÍˆÍŠO‚Í‹ó”’)B</summary>
    private static char AtChar(IReadOnlyList<char> list, int index)
        => index >= 0 && index < list.Count ? list[index] : ' ';
}
