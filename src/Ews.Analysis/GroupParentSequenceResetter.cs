using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// Œn““à‚ÅŠK‘w”Ô† 001 ‚ÌƒOƒ‹[ƒv•À—ñ’Ç”Ô(glheino)‚ª—LŒø‚È—v‘f‚ª–³‚¢ê‡‚ÉA
/// ‚»‚ÌŒn“‚ÌŠK‘w”Ô† 001 —v‘f‚·‚×‚Ä‚ÌƒOƒ‹[ƒveƒf[ƒ^’Ç”Ô(goyano)‚ğ "000" ‚É–ß‚·B
/// yCŒ´“Tztoku/sekkei/src/Fyss14.c <c>Main_Rank_Update</c>(2208)B
/// </summary>
public static class GroupParentSequenceResetter
{
    /// <summary>
    /// ƒOƒ‹[ƒveƒf[ƒ^’Ç”Ô‚ğğŒ•t‚«‚Å "000" ‚Éİ’è‚·‚éByCŒ´“TzMain_Rank_Update(Fyss14.c:2208)B
    /// </summary>
    /// <param name="mains">å‰ñ˜HƒŒƒR[ƒh—ñBGroupParentSequenceNumber ‚ğ in-place XV‚·‚éB</param>
    public static void Reset(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        for (int i = 0; i < mains.Count; i++)
        {
            // yCŒ´“Tz‚Ü‚¸ŠK‘w”Ô† 000 ‚Ì—v‘f‚ğ’T‚·B
            if (mains[i].Data.HierarchyNumber != "000")
            {
                continue;
            }

            // yCŒ´“Tz‚»‚ê‚É‘±‚­ŠK‘w”Ô† 001 ‚Ì—v‘f‚Ü‚Å i ‚ği‚ß‚éB
            for (; i < mains.Count; i++)
            {
                if (mains[i].Data.HierarchyNumber == "001")
                {
                    break;
                }
            }

            if (i >= mains.Count)
            {
                break;
            }

            string systemNumber = mains[i].Data.SystemNumber;

            // yCŒ´“TzŒn“”Ô†ˆê’vEŠK‘w”Ô† 001E•À—ñ’Ç”Ô!="001"EƒOƒ‹[ƒv•À—ñ’Ç”Ô!="000"EŒn“í•Ê 1 ‚Ì—v‘f”‚ğ”‚¦‚éB
            int n = 0;
            for (int j = i; j < mains.Count; j++)
            {
                MainCircuitData dj = mains[j].Data;
                if (dj.SystemNumber == systemNumber &&
                    dj.HierarchyNumber == "001" &&
                    dj.ParallelNumber != "001" &&
                    dj.GroupParallelNumber != "000" &&
                    dj.SystemKind == '1')
                {
                    n++;
                }
            }

            if (n != 0)
            {
                continue;   // 940821
            }

            // yCŒ´“TzŠY“–‚ª–³‚¯‚ê‚ÎAŒn“”Ô†ˆê’vEŠK‘w”Ô† 001EŒn“í•Ê 1 ‚Ì‘S—v‘f‚Ö goyano="000"B
            for (int j = i; j < mains.Count; j++)
            {
                MainCircuitData dj = mains[j].Data;
                if (dj.SystemNumber == systemNumber &&
                    dj.HierarchyNumber == "001" &&
                    dj.SystemKind == '1')
                {
                    dj.GroupParentSequenceNumber = "000";
                }
            }
        }
    }
}
