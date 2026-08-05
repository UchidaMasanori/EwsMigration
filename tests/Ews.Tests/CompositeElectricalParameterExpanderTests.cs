using System.Collections.Generic;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CompositeElectricalParameterExpander"/>
/// (【C原典】Fyss40_Compo_DenryuuParm、toku/sekkei/src/Fyss40.c:89)の単体テスト。
/// ep[0] の非ゼロフィールドを ep[1]・ep[2] へ展開し、極数(改訂1 の MC/TakeTest 特例)、
/// トリップ電流 99999.999 リセット、エレメント数 9 リセット、接点数の個別/一括複写、
/// 系統種別 '1' 時の ep[1] クリア(Ele_Area1_Clear)、ep[2]→ep[1] 再セット(Ele_Area2_Copy)を検証する。
/// </summary>
public class CompositeElectricalParameterExpanderTests
{
    /// <summary>Fyss40 が「未設定」とみなすゼロ整形値(小数点付き)で 1 スロットを初期化する。</summary>
    private static ElectricalParameters Zero() => new()
    {
        Ph1 = "0",
        Ph2 = ["0", "0"],
        Wr1 = "0",
        Wr2 = ["0", "0"],
        Hz = "00",
        P = "000",
        E = "0",
        Af = "00000.000",
        At = "00000.000",
        A1 = "00000.000",
        A2 = "00000.000",
        W1 = "0000000.00",
        Va = "0000000.00",
        Kvar = "000.00",
        Uf = "000000.0",
        Ma = ["0000", "0000", "0000", "0000"],
        V1 = ["000000.0", "000000.0", "000000.0"],
        V1Idx = "0",
        V2 = ["000000.0", "000000.0", "000000.0"],
        V2Idx = "0",
        V2Kbn = ' ',
        Am = "000",
        Vc = "000",
        VcKbn = ' ',
        Sset = "000000000.000",
        Ss = "000000000.000",
        S = "000000000.000",
        Ac = "00",
        Bc = "00",
        Cc = "00",
        T = "000.0",
        K = "000",
        Qty = '0',
        Bn = ' ',
        Sq = "000.00",
        C = '0',
        Ksu = '0',
        Mah = "00000",
        O = "0000.0",
        W2 = "000",
        Ksize = "000.0",
        Cset = "000",
        C1 = "000",
        C2 = "000",
    };

    /// <summary>3 スロットをゼロ整形値で初期化した主回路データを 1 件生成する。</summary>
    private static MainCircuitResult Rec(
        string seq = "001",
        string yoyaku = "MC",
        char ksyubetu = '0',
        string oyatno = "000")
    {
        var r = new MainCircuitResult { SequenceNumber = seq };
        MainCircuitData d = r.Data;
        d.ReservedWord = yoyaku;
        d.SystemKind = ksyubetu;
        d.ParentSequenceNumber = oyatno;
        d.ElectricalParameterSlots[0] = Zero();
        d.ElectricalParameterSlots[1] = Zero();
        d.ElectricalParameterSlots[2] = Zero();
        return r;
    }

    [Fact]
    public void 汎用フィールドをep1とep2へ複写する()
    {
        MainCircuitResult r = Rec();
        r.Data.ElectricalParameterSlots[0].Af = "00012.500";

        CompositeElectricalParameterExpander.Expand([r]);

        Assert.Equal("00012.500", r.Data.ElectricalParameterSlots[1].Af);
        Assert.Equal("00012.500", r.Data.ElectricalParameterSlots[2].Af);
    }

    [Fact]
    public void MC極数はTakeTest成立でep0を000にしep2は据え置く()
    {
        MainCircuitResult parent = Rec(seq: "001", yoyaku: "TR");
        parent.Data.ElectricalParameterSlots[0].E = "1"; // TakeTest 一致条件
        MainCircuitResult child = Rec(seq: "002", yoyaku: "MC", oyatno: "001");
        child.Data.ElectricalParameterSlots[0].P = "003";
        child.Data.ElectricalParameterSlots[2].P = "001";

        CompositeElectricalParameterExpander.Expand([parent, child]);

        Assert.Equal("000", child.Data.ElectricalParameterSlots[0].P);
        Assert.Equal("003", child.Data.ElectricalParameterSlots[1].P);
        Assert.Equal("001", child.Data.ElectricalParameterSlots[2].P);
    }

    [Fact]
    public void MC極数はTakeTest不成立でep2にも複写する()
    {
        MainCircuitResult child = Rec(seq: "002", yoyaku: "MC", oyatno: "999");
        child.Data.ElectricalParameterSlots[0].P = "003";
        child.Data.ElectricalParameterSlots[2].P = "001";

        CompositeElectricalParameterExpander.Expand([child]);

        Assert.Equal("003", child.Data.ElectricalParameterSlots[0].P);
        Assert.Equal("003", child.Data.ElectricalParameterSlots[1].P);
        Assert.Equal("003", child.Data.ElectricalParameterSlots[2].P);
    }

    [Fact]
    public void トリップ電流99999_999は00000_000として展開する()
    {
        MainCircuitResult r = Rec();
        r.Data.ElectricalParameterSlots[0].At = "99999.999";

        CompositeElectricalParameterExpander.Expand([r]);

        Assert.Equal("00000.000", r.Data.ElectricalParameterSlots[1].At);
        Assert.Equal("00000.000", r.Data.ElectricalParameterSlots[2].At);
    }

    [Fact]
    public void エレメント数9はep1とep2で0にリセットする()
    {
        MainCircuitResult r = Rec();
        r.Data.ElectricalParameterSlots[0].E = "9";

        CompositeElectricalParameterExpander.Expand([r]);

        Assert.Equal("0", r.Data.ElectricalParameterSlots[1].E);
        Assert.Equal("0", r.Data.ElectricalParameterSlots[2].E);
    }

    [Fact]
    public void 接点数はep1へ個別複写しep2へ一括複写する()
    {
        MainCircuitResult r = Rec();
        r.Data.ElectricalParameterSlots[0].Ac = "02";
        r.Data.ElectricalParameterSlots[0].Bc = "03";

        CompositeElectricalParameterExpander.Expand([r]);

        Assert.Equal("02", r.Data.ElectricalParameterSlots[1].Ac);
        Assert.Equal("03", r.Data.ElectricalParameterSlots[1].Bc);
        Assert.Equal("02", r.Data.ElectricalParameterSlots[2].Ac);
        Assert.Equal("03", r.Data.ElectricalParameterSlots[2].Bc);
        Assert.Equal("00", r.Data.ElectricalParameterSlots[2].Cc);
    }

    [Fact]
    public void 系統種別1はep1をクリアする()
    {
        MainCircuitResult r = Rec(ksyubetu: '1');
        r.Data.ElectricalParameterSlots[1].Va = "0000123.00";
        r.Data.ElectricalParameterSlots[1].Bn = 'A';

        CompositeElectricalParameterExpander.Expand([r]);

        Assert.Equal("0000000.00", r.Data.ElectricalParameterSlots[1].Va);
        Assert.Equal("00000.000", r.Data.ElectricalParameterSlots[1].At);
        Assert.Equal(' ', r.Data.ElectricalParameterSlots[1].Bn);
    }

    [Fact]
    public void 系統種別1でもep0が非ゼロならep1をクリアしない()
    {
        MainCircuitResult r = Rec(ksyubetu: '1');
        r.Data.ElectricalParameterSlots[0].Af = "00012.500";

        CompositeElectricalParameterExpander.Expand([r]);

        Assert.Equal("00012.500", r.Data.ElectricalParameterSlots[1].Af);
    }

    [Fact]
    public void EleArea2Copyはep2をep1へ再セットし接点数を保持する()
    {
        MainCircuitResult r = Rec();
        r.Data.ElectricalParameterSlots[0].Af = "00012.500"; // フラグ成立
        r.Data.ElectricalParameterSlots[0].Cc = "01";        // ep0 接点非ゼロ→退避を復元
        r.Data.ElectricalParameterSlots[1].Ac = "05";        // 復元対象の元値
        r.Data.ElectricalParameterSlots[2].Ac = "09";        // 一括複写候補(復元で無効化)
        r.Data.ElectricalParameterSlots[2].Uf = "000123.4";  // 再セットの可視化

        CompositeElectricalParameterExpander.Expand([r]);

        Assert.Equal("05", r.Data.ElectricalParameterSlots[1].Ac);       // ep2 の 09 でなく元の 05
        Assert.Equal("01", r.Data.ElectricalParameterSlots[1].Cc);
        Assert.Equal("000123.4", r.Data.ElectricalParameterSlots[1].Uf); // ep2 から再セット
        Assert.Equal("00", r.Data.ElectricalParameterSlots[2].Ac);       // 一括複写で ep0 の 00
    }

    [Fact]
    public void 感度電流配列をep1とep2へ複写する()
    {
        MainCircuitResult r = Rec();
        r.Data.ElectricalParameterSlots[0].Ma = ["0100", "0200", "0000", "0000"];

        CompositeElectricalParameterExpander.Expand([r]);

        Assert.Equal("0100", r.Data.ElectricalParameterSlots[1].Ma[0]);
        Assert.Equal("0200", r.Data.ElectricalParameterSlots[1].Ma[1]);
        Assert.Equal("0100", r.Data.ElectricalParameterSlots[2].Ma[0]);
    }
}
