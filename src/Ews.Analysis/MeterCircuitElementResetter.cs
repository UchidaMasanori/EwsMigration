using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ŒvŠí‰ñ˜H(WH Œn)‚ğ\¬‚·‚é—v‘f‚Ì‰ñ˜H—v‘f kiryoso ‚ğ '3' ‚ÖÄİ’è‚·‚éB
/// yCŒ´“TzKeikiKairo_Bangou_Reset(toku/sekkei/src/Fyss14.c:6014, 941130)B
///
/// ‰ñ˜H—v‘f '3'(ŒvŠí‰ñ˜H‚Ì•›—v‘f)‚ğ‹N“_‚ÉA“¯ˆê‚Ì(ŠK‘w kaisonoE•À—ñ heino)‚ğ‚Â
/// ’¼ã‚Ì—v‘fŒQ‚ğ‘k‚èAWH(“d—Í—ÊŒv)\¬‚âeƒqƒ…[ƒY(F)‚Ö '3' ‚ğ”g‹y‚³‚¹‚éB
/// Fyss14_Make_UpperParm ‚Ìƒ‹[ƒvŒãˆ—‚Æ‚µ‚ÄŒÄ‚Î‚ê‚éB
/// </summary>
public static class MeterCircuitElementResetter
{
    /// <summary>
    /// ŒvŠí‰ñ˜H‚Ì‰ñ˜H—v‘f kiryoso ‚ğÄİ’è‚·‚é(in-place)B
    /// yCŒ´“TzKeikiKairo_Bangou_Reset(Fyss14.c:6014)B
    /// </summary>
    public static void Reset(IReadOnlyList<MainCircuitResult> mains)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            var di = mains[i].Data;
            if (di.CircuitElement != '3') continue;

            string oyatno = di.ParentSequenceNumber;

            // 950907: “¯ˆê(ŠK‘w,•À—ñ)‚Ì’¼ã‚É WH(kiryoso=='1')‚ª‹‚é‚©”»’è‚·‚éB
            bool resetFlg = false;
            for (int j = i - 1; j >= 0; j--)
            {
                var dj = mains[j].Data;
                if (di.HierarchyNumber == dj.HierarchyNumber && di.ParallelNumber == dj.ParallelNumber)
                {
                    if (dj.ReservedWord == "WH" && dj.CircuitElement == '1')
                    {
                        resetFlg = true;
                        break;
                    }
                }
                else break;
            }

            // 950907: WH \¬‚Ìê‡‚Í“¯ˆê(ŠK‘w,•À—ñ)‚Ì—v‘f‚Ö '3' ‚ğ”g‹y‚µAe F ‚Å‘Å‚¿Ø‚éB
            if (resetFlg)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    var dj = mains[j].Data;
                    if (di.HierarchyNumber == dj.HierarchyNumber && di.ParallelNumber == dj.ParallelNumber)
                    {
                        dj.CircuitElement = '3';
                        if (dj.ReservedWord == "F") break;
                    }
                    else break;
                }
                continue;
            }

            // “¯ˆê(ŠK‘w,•À—ñ)‚ğ‘k‚è '3' ‚ğ”g‹y(M ‚Íå‰ñ˜H}‘Î‰‚Å‘ÎÛŠOE‰ü’ù<27>)B
            int last;
            for (last = i - 1; last >= 0; last--)
            {
                var dj = mains[last].Data;
                if (di.HierarchyNumber == dj.HierarchyNumber && di.ParallelNumber == dj.ParallelNumber)
                {
                    if (dj.CircuitClass == 'M') continue;   // ‰ü’ù<27>
                    dj.CircuitElement = '3';
                    oyatno = dj.ParentSequenceNumber;
                }
                else break;
            }

            // 950208: ‘k‚èI’[‚ªeƒf[ƒ^’Ç”Ô‚Æˆê’v‚·‚é’¼—ñ 001 ‚Ì F ‚È‚ç '3' ‚É‚·‚éB
            if (last >= 0)
            {
                var dk = mains[last].Data;
                if (mains[last].SequenceNumber == oyatno && dk.SeriesNumber == "001" && dk.ReservedWord == "F")
                {
                    dk.CircuitElement = '3';
                }
            }
        }
    }
}
