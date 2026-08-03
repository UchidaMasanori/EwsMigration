using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 負荷発生元の変更処理(<see cref="LoadSourceChanger"/>)の移植検証。
/// 【C原典】Fyss3F_Fuka_Change(toku/sekkei/src/Fyss3F.c)。
/// 先頭機器の使用相別の積算エリア相間振替・負荷発生元区分セット・下流フラグクリアを検証する。
/// </summary>
public sealed class LoadSourceChangerTests
{
    private static MainCircuitResult Row(
        string datano,
        string oyatno = "000",
        char kiryoso = '1',
        char sentflg = '1',
        char ahassei = ' ',
        string siyouso = "")
    {
        var r = new MainCircuitResult
        {
            SequenceNumber = datano,
            Data = new MainCircuitData
            {
                ParentSequenceNumber = oyatno,
                CircuitElement = kiryoso,
                LoadSourceKind = ahassei,
                UsedPhase = siyouso,
            },
        };
        r.Work.LeadingEquipmentFlag = sentflg;
        return r;
    }

    private static void SetPhase(MainCircuitResult r, int slot, double a = 0, double b = 0, double c = 0,
        double d = 0, double e = 0, double m = 0, double s = 0)
    {
        AccumulationArea x = r.Work.AccumulationSlots[slot];
        x.A = a; x.B = b; x.C = c; x.D = d; x.E = e; x.M = m; x.S = s;
    }

    [Fact]
    public void 対象外機器は負荷発生元区分を変更しない()
    {
        var notLeading = Row("001", kiryoso: '1', sentflg: ' ');
        var notElement = Row("002", kiryoso: '2', sentflg: '1');
        LoadSourceChanger.ChangeLoadSource([notLeading, notElement]);

        Assert.Equal(' ', notLeading.Data.LoadSourceKind);
        Assert.Equal(' ', notElement.Data.LoadSourceKind);
    }

    [Fact]
    public void 先頭機器は負荷発生元区分1をセットする()
    {
        var r = Row("001", siyouso: "");
        LoadSourceChanger.ChangeLoadSource([r]);
        Assert.Equal('1', r.Data.LoadSourceKind);
    }

    [Fact]
    public void XN相はX相の0要素へY相を複写しY相をクリアする()
    {
        var r = Row("001", siyouso: "XN  ");
        SetPhase(r, 3, a: 0, b: 5);   // X相(既存 b は保持、a は Y から補完)
        SetPhase(r, 4, a: 10, b: 20); // Y相
        LoadSourceChanger.ChangeLoadSource([r]);

        Assert.Equal(10, r.Work.AccumulationSlots[3].A); // 0 だった a に Y の a
        Assert.Equal(5, r.Work.AccumulationSlots[3].B);  // 非0 の b は保持
        Assert.Equal(0, r.Work.AccumulationSlots[4].A);  // Y相クリア
        Assert.Equal(0, r.Work.AccumulationSlots[4].B);
    }

    [Fact]
    public void RN相はR相の0要素へS優先T補完しSとTをクリアする()
    {
        var r = Row("001", siyouso: "RN  ");
        SetPhase(r, 0, a: 0, b: 0, c: 7); // R相
        SetPhase(r, 1, a: 11, b: 0);      // S相(a のみ)
        SetPhase(r, 2, a: 99, b: 22);     // T相
        LoadSourceChanger.ChangeLoadSource([r]);

        Assert.Equal(11, r.Work.AccumulationSlots[0].A); // S 優先
        Assert.Equal(22, r.Work.AccumulationSlots[0].B); // S=0 なので T
        Assert.Equal(7, r.Work.AccumulationSlots[0].C);  // 元々非0 は保持
        Assert.Equal(0, r.Work.AccumulationSlots[1].A);  // S相クリア
        Assert.Equal(0, r.Work.AccumulationSlots[2].A);  // T相クリア
    }

    [Fact]
    public void RS相はRS2相補完しT相をクリアする()
    {
        // RS: RST2_set(0,0,1,1,2,1,1,0,0,2) → [0]==0 なら [0]<-[1],[1]<-[2] / else [1]==0 なら [1]<-[0],[0]<-[2]
        var r = Row("001", siyouso: "RS  ");
        SetPhase(r, 0, a: 0);   // R相 a=0 → 分岐1
        SetPhase(r, 1, a: 30);  // S相
        SetPhase(r, 2, a: 40);  // T相
        LoadSourceChanger.ChangeLoadSource([r]);

        Assert.Equal(30, r.Work.AccumulationSlots[0].A); // [0]<-[1]
        Assert.Equal(40, r.Work.AccumulationSlots[1].A); // [1]<-[2]
        Assert.Equal(0, r.Work.AccumulationSlots[2].A);  // T相クリア
    }

    [Fact]
    public void 下流の子孫要素の負荷発生元区分をクリアする()
    {
        var parent = Row("001", oyatno: "000", siyouso: "");
        var child = Row("002", oyatno: "001", sentflg: ' ', ahassei: '1');       // parent の子
        var grandChild = Row("003", oyatno: "002", sentflg: ' ', ahassei: '1');  // child の子
        var unrelated = Row("004", oyatno: "099", sentflg: ' ', ahassei: '1');   // 無関係

        LoadSourceChanger.ChangeLoadSource([parent, child, grandChild, unrelated]);

        Assert.Equal('1', parent.Data.LoadSourceKind); // 先頭機器はセット
        Assert.Equal(' ', child.Data.LoadSourceKind);  // 再帰クリア
        Assert.Equal(' ', grandChild.Data.LoadSourceKind);
        Assert.Equal('1', unrelated.Data.LoadSourceKind); // 無関係は不変
    }
}
