using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// ＮＴ(中性線端子)の特殊処理(<see cref="NtSpecialProcessor"/>)の移植検証。
/// 【C原典】Fyss38_NT_Proc／Fyss38_Get_epap(toku/sekkei/src/Fyss38.c)。
/// NT の極数(P)合計セット・MCB のトリップ電流(AT)MAX を NT の定格電流2(A2)へセットを検証する。
/// </summary>
public sealed class NtSpecialProcessorTests
{
    private static MainCircuitResult Row(
        string datano,
        char ksyubetu = '1',
        string yoyaku = "",
        string oyatno = "000",
        string kaisono = "000",
        string heino = "000",
        string chokuno = "000",
        string ep0P = "000",
        string ep2P = "000",
        string ep2At = "000000000",
        string ep0A2 = "000000000",
        string siyouso = "RST ")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                SystemKind = ksyubetu,
                ReservedWord = yoyaku,
                ParentSequenceNumber = oyatno,
                HierarchyNumber = kaisono,
                ParallelNumber = heino,
                SeriesNumber = chokuno,
                UsedPhase = siyouso,
            },
        };
        r.Data.ElectricalParameterSlots[0].P = ep0P;
        r.Data.ElectricalParameterSlots[2].P = ep2P;
        r.Data.ElectricalParameterSlots[2].At = ep2At;
        r.Data.ElectricalParameterSlots[0].A2 = ep0A2;
        return r;
    }

    // ── 極数(P)合計取得(Fyss38_Get_epap) ────────────────────────────────────

    [Fact]
    public void 同一親同一階層のMCB極数1件数を合計し偶数化する()
    {
        var m1 = Row("001", yoyaku: "MCB", oyatno: "005", kaisono: "002", ep2P: "001");
        var m2 = Row("002", yoyaku: "MCB", oyatno: "005", kaisono: "002", ep2P: "001");
        var m3 = Row("003", yoyaku: "MCB", oyatno: "005", kaisono: "002", ep2P: "001");
        var other = Row("004", yoyaku: "MCB", oyatno: "009", kaisono: "002", ep2P: "001"); // 親違い

        // MCB 3件(奇数)→ +1 → 4
        Assert.Equal(4, NtSpecialProcessor.GetPoleCountSum([m1, m2, m3, other], 5, 2));
    }

    [Fact]
    public void 極数2以外や予約語MCB以外は合計に含めない()
    {
        var p1 = Row("001", yoyaku: "MCB", oyatno: "005", kaisono: "002", ep2P: "001");
        var p2 = Row("002", yoyaku: "MCB", oyatno: "005", kaisono: "002", ep2P: "003"); // 極数!=1
        var p3 = Row("003", yoyaku: "ELB", oyatno: "005", kaisono: "002", ep2P: "001"); // 予約語違い

        // 有効は p1 のみ(1件, 奇数)→ +1 → 2
        Assert.Equal(2, NtSpecialProcessor.GetPoleCountSum([p1, p2, p3], 5, 2));
    }

    // ── NT の極数(P)セット ─────────────────────────────────────────────────

    [Fact]
    public void NTの極数0なら並列MCB合計を極数2へセットする()
    {
        var nt = Row("001", yoyaku: "NT", oyatno: "005", kaisono: "002", ep0P: "000");
        var mcb1 = Row("002", yoyaku: "MCB", oyatno: "005", kaisono: "002", ep2P: "001");
        var mcb2 = Row("003", yoyaku: "MCB", oyatno: "005", kaisono: "002", ep2P: "001");

        NtSpecialProcessor.ProcessNt([nt, mcb1, mcb2]);

        Assert.Equal("002", nt.Data.ElectricalParameterSlots[2].P); // 合計2
    }

    [Fact]
    public void NTの極数が非0なら極数0を極数2へ複写する()
    {
        var nt = Row("001", yoyaku: "NT", oyatno: "005", kaisono: "002", ep0P: "003");

        NtSpecialProcessor.ProcessNt([nt]);

        Assert.Equal("003", nt.Data.ElectricalParameterSlots[2].P);
    }

    // ── トリップ電流(AT)MAX → NT の定格電流2(A2) ──────────────────────────

    [Fact]
    public void MCB極数1のATのMAXをNTのA2へセットする()
    {
        var nt = Row("001", yoyaku: "NT", oyatno: "005", kaisono: "002", ep0P: "003",
            ep0A2: "00000.000");
        var mcb1 = Row("002", yoyaku: "MCB", oyatno: "005", kaisono: "002", ep2P: "001",
            ep2At: "00030.000");
        var mcb2 = Row("003", yoyaku: "MCB", oyatno: "005", kaisono: "002", ep2P: "001",
            ep2At: "00050.000");

        NtSpecialProcessor.ProcessNt([nt, mcb1, mcb2]);

        Assert.Equal("00050.000", nt.Data.ElectricalParameterSlots[2].A2); // MAX=50
    }

    [Fact]
    public void NTのA2が入力済みならその値を複写しMAXを使わない()
    {
        var nt = Row("001", yoyaku: "NT", oyatno: "005", kaisono: "002", ep0P: "003",
            ep0A2: "00099.000");
        var mcb = Row("002", yoyaku: "MCB", oyatno: "005", kaisono: "002", ep2P: "001",
            ep2At: "00050.000");

        NtSpecialProcessor.ProcessNt([nt, mcb]);

        Assert.Equal("00099.000", nt.Data.ElectricalParameterSlots[2].A2);
    }

    [Fact]
    public void MCB極数1は使用相1をスペースクリアする()
    {
        var mcb = Row("001", yoyaku: "MCB", oyatno: "005", kaisono: "002", ep2P: "001",
            ep2At: "00030.000", siyouso: "RST ");

        NtSpecialProcessor.ProcessNt([mcb]);

        Assert.Equal("R T ", mcb.Data.UsedPhase); // [1]='S'→' '
    }

    [Fact]
    public void 直列下流のMCB極数1は使用相1以降を削る()
    {
        var mcb = Row("001", yoyaku: "MCB", oyatno: "005", kaisono: "002", heino: "001",
            chokuno: "001", ep2P: "001", ep2At: "00030.000", siyouso: "RST ");
        var down = Row("002", yoyaku: "MCB", oyatno: "005", kaisono: "002", heino: "001",
            chokuno: "002", ep0P: "001", ep2P: "003", siyouso: "RST ");

        NtSpecialProcessor.ProcessNt([mcb, down]);

        Assert.Equal("R   ", down.Data.UsedPhase); // [1..3] クリア、[0]='R' 保持
    }
}
