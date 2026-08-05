using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="MeterCircuitElementResetter"/>(C Œ´“T KeikiKairo_Bangou_Reset)‚Ì’P‘ÌƒeƒXƒgB
/// </summary>
public sealed class MeterCircuitElementResetterTests
{
    private static MainCircuitResult Rec(
        string datano = "000",
        char kiryoso = ' ',
        string yoyaku = "",
        char kairobun = ' ',
        string kaisono = "000",
        string heino = "000",
        string chokuno = "000",
        string oyatno = "000")
    {
        return new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                CircuitElement = kiryoso,
                ReservedWord = yoyaku,
                CircuitClass = kairobun,
                HierarchyNumber = kaisono,
                ParallelNumber = heino,
                SeriesNumber = chokuno,
                ParentSequenceNumber = oyatno,
            },
        };
    }

    [Fact]
    public void ‰ñ˜H—v‘f3ˆÈŠO‚Í‘ÎÛŠO()
    {
        var a = Rec(datano: "001", kiryoso: '1', yoyaku: "WH", kaisono: "001", heino: "001");
        var mains = new[] { a };

        MeterCircuitElementResetter.Reset(mains);

        Assert.Equal('1', a.Data.CircuitElement);
    }

    [Fact]
    public void WH\¬‚Í“¯ˆêŠK‘w•À—ñ‚Ö3‚ğ”g‹y‚µeF‚Å‘Å‚¿Ø‚é()
    {
        // F(e) ¨ WH(kiryoso1) ¨ ‘ÎÛ(kiryoso3) : ‚·‚×‚Ä“¯ˆê(ŠK‘w,•À—ñ)
        var f = Rec(datano: "001", kiryoso: '1', yoyaku: "F", kaisono: "001", heino: "001");
        var wh = Rec(datano: "002", kiryoso: '1', yoyaku: "WH", kaisono: "001", heino: "001");
        var tgt = Rec(datano: "003", kiryoso: '3', yoyaku: "WH", kaisono: "001", heino: "001");
        var mains = new[] { f, wh, tgt };

        MeterCircuitElementResetter.Reset(mains);

        Assert.Equal('3', wh.Data.CircuitElement);   // WH ‚É”g‹y
        Assert.Equal('3', f.Data.CircuitElement);    // e F ‚É‚à”g‹y‚µ‘Å‚¿Ø‚è
    }

    [Fact]
    public void ˆÙ‚È‚éŠK‘w•À—ñ‚É’B‚µ‚½‚ç”g‹y‚ğ‘Å‚¿Ø‚é()
    {
        // e(•ÊŠK‘w) ¨ “¯ˆê(ŠK‘w,•À—ñ)‚Ì”ñWH—v‘f ¨ ‘ÎÛ(kiryoso3)
        var parent = Rec(datano: "001", kiryoso: '1', yoyaku: "F", kaisono: "001", heino: "001");
        var sib = Rec(datano: "002", kiryoso: '1', yoyaku: "TS", kaisono: "002", heino: "001");
        var tgt = Rec(datano: "003", kiryoso: '3', yoyaku: "TS", kaisono: "002", heino: "001");
        var mains = new[] { parent, sib, tgt };

        MeterCircuitElementResetter.Reset(mains);

        Assert.Equal('3', sib.Data.CircuitElement);   // “¯ˆê(ŠK‘w,•À—ñ)‚È‚Ì‚Å”g‹y
        Assert.Equal('1', parent.Data.CircuitElement); // •ÊŠK‘w‚È‚Ì‚Å‘ÎÛŠO
    }

    [Fact]
    public void ¶¬‰ñ˜H•ª—ŞM‚Íå‰ñ˜H}‘Î‰‚Å‘ÎÛŠO()
    {
        // M(MCB) ‚Í“¯ˆê(ŠK‘w,•À—ñ)‚Å‚à '3' ‚É‚µ‚È‚¢(‰ü’ù27)
        var m = Rec(datano: "001", kiryoso: '1', yoyaku: "MCB", kairobun: 'M', kaisono: "002", heino: "001");
        var tgt = Rec(datano: "002", kiryoso: '3', yoyaku: "TS", kaisono: "002", heino: "001");
        var mains = new[] { m, tgt };

        MeterCircuitElementResetter.Reset(mains);

        Assert.Equal('1', m.Data.CircuitElement);   // M ‚Í‘ÎÛŠO
    }

    [Fact]
    public void ‘k‚èI’[‚ªe’Ç”Ôˆê’v‚Ì’¼—ñ001F‚È‚ç3‚É‚·‚é()
    {
        // F(e, datano=001, ’¼—ñ001) ¨ ‘ÎÛ(kiryoso3, oyatno=001, •ÊŠK‘w)
        var f = Rec(datano: "001", kiryoso: '1', yoyaku: "F", chokuno: "001", kaisono: "001", heino: "001");
        var tgt = Rec(datano: "002", kiryoso: '3', yoyaku: "TS", kaisono: "002", heino: "001", oyatno: "001");
        var mains = new[] { f, tgt };

        MeterCircuitElementResetter.Reset(mains);

        // tgt ‚Æ f ‚Í (ŠK‘w,•À—ñ)‚ªˆÙ‚È‚é‚½‚ß”g‹yƒ‹[ƒv‚Í f ‚Å breakA
        // 950208 ‚Å datano==oyatno ‚©‚Â’¼—ñ001‚©‚Â F ‚È‚Ì‚Å '3'
        Assert.Equal('3', f.Data.CircuitElement);
    }
}
