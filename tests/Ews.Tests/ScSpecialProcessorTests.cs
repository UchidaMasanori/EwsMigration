using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// ＳＣ(進相コンデンサ)の特殊処理(<see cref="ScSpecialProcessor"/>)の移植検証。
/// 【C原典】Fyss39_SC_Proc ほか(toku/sekkei/src/Fyss39.c)。
/// 対象データチェック・静電容量/定格容量の[2]コピー・電動機容量からの静電容量算出
/// (SCuf=(Pkm)^a*b)・容量未設定時の他ＳＣからの流用 を検証する。
/// </summary>
public sealed class ScSpecialProcessorTests
{
    private static MainCircuitResult Row(
        string datano,
        char ksyubetu = '1',
        string yoyaku = "",
        string hierarchy = "000",
        string ep0Uf = "00000000",
        string ep0Kvar = "000000",
        string ep0W1 = "0000000.00",
        string ep2Uf = "00000000",
        string ep2Kvar = "000000",
        string fpaln1 = "",
        string fpalw1 = "  ",
        string fpalw2 = "0000000",
        string gyocd = "000",
        string gyoglno = "000",
        char kpaph = '0',
        string kpav0 = "000",
        string kpahz = "00")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                SystemKind = ksyubetu,
                ReservedWord = yoyaku,
                HierarchyNumber = hierarchy,
                LineTypeCode = gyocd,
                LineTypeGroupNumber = gyoglno,
                CircuitPhaseCount = kpaph,
                CircuitFrequency = kpahz,
            },
        };
        r.Data.CircuitVoltage[0] = kpav0;
        r.Data.ElectricalParameterSlots[0].Uf = ep0Uf;
        r.Data.ElectricalParameterSlots[0].Kvar = ep0Kvar;
        r.Data.ElectricalParameterSlots[0].W1 = ep0W1;
        r.Data.ElectricalParameterSlots[2].Uf = ep2Uf;
        r.Data.ElectricalParameterSlots[2].Kvar = ep2Kvar;
        r.Data.AttachedParameter.LoadName[1] = fpaln1;
        r.Data.AttachedParameter.LoadKind = fpalw1;
        r.Data.AttachedParameter.LoadCapacity = fpalw2;
        return r;
    }

    // ── 対象データチェック(Fyss39_Chk_Yoyaku) ───────────────────────────────

    [Fact]
    public void 系統種別が1以外なら対象外()
    {
        var r = Row("001", ksyubetu: '2', yoyaku: "SC");
        Assert.Equal((1, 0.0, 0.0), ScSpecialProcessor.CheckReservedWord([r], 0));
    }

    [Fact]
    public void 予約語がSC以外なら対象外()
    {
        var r = Row("001", ksyubetu: '1', yoyaku: "NT");
        Assert.Equal((1, 0.0, 0.0), ScSpecialProcessor.CheckReservedWord([r], 0));
    }

    [Fact]
    public void 系統種別1かつSCならUFとKVARを返す()
    {
        var r = Row("001", ksyubetu: '1', yoyaku: "SC", ep0Uf: "000123.4", ep0Kvar: "005.50");
        Assert.Equal((0, 123.4, 5.5), ScSpecialProcessor.CheckReservedWord([r], 0));
    }

    // ── 入力済みＳＣ(UF/KVAR≠0)の[2]コピー ─────────────────────────────────

    [Fact]
    public void UF入力済みなら電気パラメータ2へ整形コピーしフラグ1()
    {
        var sc = Row("001", yoyaku: "SC", ep0Uf: "000123.4", ep0Kvar: "005.50");
        ScSpecialProcessor.ProcessSc([sc]);

        Assert.Equal("000123.4", sc.Data.ElectricalParameterSlots[2].Uf);
        Assert.Equal("005.50", sc.Data.ElectricalParameterSlots[2].Kvar);
        Assert.Equal('1', sc.Work.ScProcessedFlag);
    }

    [Fact]
    public void KVARのみ入力済みでも対象となりコピーされる()
    {
        var sc = Row("001", yoyaku: "SC", ep0Uf: "00000000", ep0Kvar: "012.00");
        ScSpecialProcessor.ProcessSc([sc]);

        Assert.Equal("012.00", sc.Data.ElectricalParameterSlots[2].Kvar);
        Assert.Equal('1', sc.Work.ScProcessedFlag);
    }

    [Fact]
    public void 系統種別1以外のSCは処理対象外()
    {
        var sc = Row("001", ksyubetu: '2', yoyaku: "SC", ep0Uf: "000123.4");
        ScSpecialProcessor.ProcessSc([sc]);

        Assert.Equal("00000000", sc.Data.ElectricalParameterSlots[2].Uf);
        Assert.Equal(' ', sc.Work.ScProcessedFlag);
    }

    // ── 静電容量算出(Fyss39_Get_Seiden) ─────────────────────────────────────

    [Fact]
    public void 三相220V50Hz電動機容量4kWから静電容量を算出する()
    {
        // 電動機容量 4000W → Pkm=4.0 → a=0.56,b=37.0 → pow(4,0.56)*37 ≒ 80.4uF
        var upstream = Row("001", yoyaku: "");
        var sc = Row("002", yoyaku: "SC", ep0Uf: "00000000", ep0Kvar: "000000",
            fpalw1: "M ", fpalw2: "0004000", kpaph: '3', kpav0: "220", kpahz: "50");

        ScSpecialProcessor.ProcessSc([upstream, sc]);

        Assert.Equal("000080.4", sc.Data.ElectricalParameterSlots[2].Uf);
        Assert.Equal('1', sc.Work.ScProcessedFlag);
        Assert.Equal('1', upstream.Work.ScProcessedFlag); // no-2 のフラグセット
    }

    [Fact]
    public void 単相105V50Hz電動機容量2kWから静電容量を算出する()
    {
        // 電動機容量 2000W → Pkm=2.0 → a=0.16,b=112 → pow(2,0.16)*112 ≒ 125.1uF
        var upstream = Row("001", yoyaku: "");
        var sc = Row("002", yoyaku: "SC", ep0Uf: "00000000", ep0Kvar: "000000",
            fpalw1: "M ", fpalw2: "0002000", kpaph: '1', kpav0: "100", kpahz: "50");

        ScSpecialProcessor.ProcessSc([upstream, sc]);

        Assert.Equal("000125.1", sc.Data.ElectricalParameterSlots[2].Uf);
    }

    [Fact]
    public void 負荷名称が0KWのSCは静電容量を算出しない()
    {
        var upstream = Row("001", yoyaku: "");
        var sc = Row("002", yoyaku: "SC", ep0Uf: "00000000", ep0Kvar: "000000",
            fpaln1: "0KW", fpalw1: "M ", fpalw2: "0004000", kpaph: '3', kpav0: "220", kpahz: "50");

        ScSpecialProcessor.ProcessSc([upstream, sc]);

        Assert.Equal("00000000", sc.Data.ElectricalParameterSlots[2].Uf); // 未処理のまま
        Assert.Equal(' ', sc.Work.ScProcessedFlag);
    }

    // ── 容量未設定時の他ＳＣからの流用(2003.05.13) ─────────────────────────

    [Fact]
    public void 容量0のSCは後方の非0SCから流用する()
    {
        // element0: 電動機容量なし・行種不一致 → SCuf=0 → "000000.0"
        var zeroSc = Row("001", yoyaku: "SC", ep0Uf: "00000000", ep0Kvar: "000000",
            fpalw1: "X ");
        // element1: UF入力済み → "000050.0"
        var setSc = Row("002", yoyaku: "SC", ep0Uf: "000050.0");

        ScSpecialProcessor.ProcessSc([zeroSc, setSc]);

        Assert.Equal("000050.0", zeroSc.Data.ElectricalParameterSlots[2].Uf);
        Assert.Equal("000050.0", setSc.Data.ElectricalParameterSlots[2].Uf);
    }
}
