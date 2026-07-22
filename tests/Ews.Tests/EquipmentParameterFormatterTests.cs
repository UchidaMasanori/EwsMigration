using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 電気パラメータ整形(型式展開、<see cref="EquipmentParameterFormatter"/>)の検証。
/// 【C原典】toku/sekkei/src/Fyss1f.c eparm_set / set_9 / Stof。
/// 本フェーズ(Wave 1~6)は遮断器系・漏電遮断器系・引込(PS/P/UP)・電磁接触器系(MC/THR/MG/SC)・端子台計器系(NT/WH/VM/AM/VT/CT/VS/AS)・TB/CON/TR(多スロット変圧器)を対象とする。
/// 入力の <see cref="RatingValues"/> は key_check の格納結果を直接構築して与える。
/// </summary>
public sealed class EquipmentParameterFormatterTests
{
    private static ElectricalParameters Format(string yoyaku, params (string Field, string Value)[] fields)
    {
        var values = new RatingValues(yoyaku);
        foreach ((string field, string value) in fields)
        {
            values.Set(field, value);
        }
        return new EquipmentParameterFormatter().EparmSet(values, yoyaku);
    }

    // ── set_9 単体 ───────────────────────────────────────────────────

    [Theory]
    [InlineData("150", 4, 9, "%09.3f", 1.0, "00150.000")]   // フレーム電流(ゼロ埋め9桁)
    [InlineData("40", 2, 9, "%09.3f", 1.0, "00040.000")]    // SB AF
    [InlineData("30", 3, 4, "%04.0f", 1.0, "0030")]         // 感度電流 MA
    [InlineData("225", 1, 3, "%03.0f", 1.0, "002")]         // 極数: from_length=1 で先頭1桁のみ
    [InlineData("100", 3, 8, "%08.1f", 1.0, "000100.0")]    // 定格電圧2
    [InlineData("5.5", 6, 10, "%010.2f", 1000.0, "0005500.00")] // 負荷容量 kw×1000
    [InlineData("", 4, 9, "%09.3f", 1.0, "00000.000")]      // 未設定は 0
    [InlineData("99999.999", 9, 9, "%09.3f", 1.0, "99999.999")] // AT 特殊値
    public void set_9はC書式で固定長整形する(string from, int fromLen, int toLen, string fmt, double mul, string expected)
    {
        string actual = EquipmentParameterFormatter.Set9(from, fromLen, toLen, fmt, mul);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void set_9はfrom_lengthで先頭を切り出す()
    {
        // 【C原典】strncpy(work, from, from_length): "225" の先頭1桁 "2" → atof=2
        Assert.Equal("002", EquipmentParameterFormatter.Set9("225", 1, 3, "%03.0f", 1.0));
    }

    [Fact]
    public void Stofは先頭size文字をatofする()
    {
        Assert.Equal(0.0, EquipmentParameterFormatter.Stof("0", 4));
        Assert.Equal(150.0, EquipmentParameterFormatter.Stof("150", 4));
        Assert.Equal(0.0, EquipmentParameterFormatter.Stof(null, 4));
    }

    // ── MCB ─────────────────────────────────────────────────────────

    [Fact]
    public void MCBは極数AF_AT電圧を整形する()
    {
        ElectricalParameters ep = Format("MCB",
            ("p", "3"), ("e", "3"), ("af", "225"), ("at", "150"), ("v", "200"), ("fv", "A"));

        Assert.Equal("003", ep.P);
        Assert.Equal("3", ep.E);
        Assert.Equal("00225.000", ep.Af);
        Assert.Equal("00150.000", ep.At);
        Assert.Equal('A', ep.V2Kbn);
        Assert.Equal("000200.0", ep.V2[0]);
    }

    [Fact]
    public void MCBはエレメント0のとき9に置換する()
    {
        // 【C原典】if( u->mcb.e == '0' ) ep->epae = '9';
        ElectricalParameters ep = Format("MCB", ("p", "3"), ("e", "0"));
        Assert.Equal("9", ep.E);
    }

    [Fact]
    public void MCBはAT値0かつ非空のとき99999_999にする()
    {
        // 【C原典】dwork=Stof(at,4); if(dwork==0 && at[0]!='\0') set_9("99999.999",…)
        ElectricalParameters ep = Format("MCB", ("at", "0"));
        Assert.Equal("99999.999", ep.At);
    }

    [Fact]
    public void MCBはAT未設定のとき0埋めにする()
    {
        // 【C原典】at[0]=='\0'(未設定)→ else 分岐 set_9(at,…) → atof("")=0
        ElectricalParameters ep = Format("MCB", ("p", "3"));
        Assert.Equal("00000.000", ep.At);
    }

    [Fact]
    public void MCBは直流区分をDにする()
    {
        ElectricalParameters ep = Format("MCB", ("fv", "D"), ("v", "100"));
        Assert.Equal('D', ep.V2Kbn);
    }

    [Fact]
    public void MCBはfv未設定のとき空白区分にする()
    {
        // 【C原典】ep->epav2kbn = u->mcb.fv ? … : ' ';
        ElectricalParameters ep = Format("MCB", ("p", "3"));
        Assert.Equal(' ', ep.V2Kbn);
    }

    // ── ELB(感度電流 MA) ────────────────────────────────────────────

    [Fact]
    public void ELBは感度電流を3スロット整形する()
    {
        ElectricalParameters ep = Format("ELB",
            ("p", "3"), ("e", "3"), ("af", "50"), ("at", "40"),
            ("ma[0]", "15"), ("ma[1]", "30"), ("ma[2]", "100"), ("v", "200"), ("fv", "A"));

        Assert.Equal("0015", ep.Ma[0]);
        Assert.Equal("0030", ep.Ma[1]);
        Assert.Equal("0100", ep.Ma[2]);
        Assert.Equal("00050.000", ep.Af);
        Assert.Equal("00040.000", ep.At);
    }

    // ── MMCB(kw→epaw1、e の '0'→'9' 無し) ───────────────────────────

    [Fact]
    public void MMCBは負荷容量をkW千倍で整形しエレメントは置換しない()
    {
        ElectricalParameters ep = Format("MMCB",
            ("p", "3"), ("e", "0"), ("af", "100"), ("at", "75"), ("kw", "5.5"), ("v", "200"), ("fv", "A"));

        Assert.Equal("003", ep.P);
        Assert.Equal("0", ep.E);                 // MMCB は e=='0'→'9' 変換なし
        Assert.Equal("00100.000", ep.Af);
        Assert.Equal("00075.000", ep.At);
        Assert.Equal("0005500.00", ep.W1);       // 5.5 × 1000
    }

    // ── ELMB(kw と ma 両方) ─────────────────────────────────────────

    [Fact]
    public void ELMBは負荷容量と感度電流を整形する()
    {
        ElectricalParameters ep = Format("ELMB",
            ("p", "3"), ("e", "2"), ("af", "60"), ("at", "50"), ("kw", "3.7"),
            ("ma[0]", "30"), ("ma[1]", "100"), ("ma[2]", "200"), ("v", "200"), ("fv", "A"));

        Assert.Equal("0003700.00", ep.W1);
        Assert.Equal("0030", ep.Ma[0]);
        Assert.Equal("0100", ep.Ma[1]);
        Assert.Equal("0200", ep.Ma[2]);
    }

    // ── SB(AF/AT は 2桁入力) ────────────────────────────────────────

    [Fact]
    public void SBはAF_ATを整形する()
    {
        ElectricalParameters ep = Format("SB",
            ("p", "2"), ("e", "2"), ("af", "30"), ("at", "20"), ("v", "100"), ("fv", "A"));

        Assert.Equal("002", ep.P);
        Assert.Equal("00030.000", ep.Af);
        Assert.Equal("00020.000", ep.At);
    }

    // ── RMCB(漏電遮断器: 制御電圧 vc/fvc→epavc/epavckbn) ─────────────

    [Fact]
    public void RMCBは制御電圧を整形する()
    {
        ElectricalParameters ep = Format("RMCB",
            ("p", "3"), ("e", "2"), ("af", "30"), ("at", "20"), ("v", "200"), ("fv", "A"),
            ("vc", "100"), ("fvc", "A"));

        Assert.Equal("003", ep.P);
        Assert.Equal("2", ep.E);
        Assert.Equal("00030.000", ep.Af);
        Assert.Equal("00020.000", ep.At);
        Assert.Equal('A', ep.V2Kbn);
        Assert.Equal("000200.0", ep.V2[0]);
        Assert.Equal('A', ep.VcKbn);
        Assert.Equal("100", ep.Vc);
    }

    [Fact]
    public void RMCBはエレメント0を9に置換しない()
    {
        // 【C原典】RMCB は ep->epae = u->rmcb.e ? u->rmcb.e : '0'; (e=='0'→'9' 変換なし)
        ElectricalParameters ep = Format("RMCB", ("e", "0"));
        Assert.Equal("0", ep.E);
    }

    [Fact]
    public void RMCBはfvc未設定のとき空白区分にする()
    {
        // 【C原典】ep->epavckbn = u->rmcb.fvc ? … : ' ';
        ElectricalParameters ep = Format("RMCB", ("p", "3"));
        Assert.Equal(' ', ep.VcKbn);
        Assert.Equal("000", ep.Vc);
    }

    [Fact]
    public void RMCBはfvcがA以外のときDにする()
    {
        ElectricalParameters ep = Format("RMCB", ("vc", "24"), ("fvc", "D"));
        Assert.Equal('D', ep.VcKbn);
        Assert.Equal("024", ep.Vc);
    }

    // ── RELB(RMCB + 感度電流 MA) ─────────────────────────────────────

    [Fact]
    public void RELBは制御電圧と感度電流を整形する()
    {
        ElectricalParameters ep = Format("RELB",
            ("p", "3"), ("e", "3"), ("af", "60"), ("at", "50"), ("v", "200"), ("fv", "A"),
            ("vc", "100"), ("fvc", "A"),
            ("ma[0]", "15"), ("ma[1]", "30"), ("ma[2]", "100"));

        Assert.Equal("00060.000", ep.Af);
        Assert.Equal("00050.000", ep.At);
        Assert.Equal('A', ep.VcKbn);
        Assert.Equal("100", ep.Vc);
        Assert.Equal("0015", ep.Ma[0]);
        Assert.Equal("0030", ep.Ma[1]);
        Assert.Equal("0100", ep.Ma[2]);
    }

    // ── RMMCB(AT 5桁入力 + kw + 制御電圧) ───────────────────────────

    [Fact]
    public void RMMCBは負荷容量と制御電圧を整形する()
    {
        ElectricalParameters ep = Format("RMMCB",
            ("p", "3"), ("e", "0"), ("af", "60"), ("at", "12500"), ("kw", "5.5"),
            ("v", "200"), ("fv", "A"), ("vc", "100"), ("fvc", "A"));

        Assert.Equal("003", ep.P);
        Assert.Equal("0", ep.E);                 // RMMCB は e=='0'→'9' 変換なし
        Assert.Equal("00060.000", ep.Af);        // AF は from_length=2
        Assert.Equal("12500.000", ep.At);        // AT は from_length=5
        Assert.Equal("0005500.00", ep.W1);       // 5.5 × 1000
        Assert.Equal('A', ep.VcKbn);
        Assert.Equal("100", ep.Vc);
    }

    // ── RELMB(RMMCB + 感度電流 MA) ───────────────────────────────────

    [Fact]
    public void RELMBは負荷容量_感度電流_制御電圧を整形する()
    {
        ElectricalParameters ep = Format("RELMB",
            ("p", "3"), ("e", "2"), ("af", "60"), ("at", "12500"), ("kw", "3.7"),
            ("ma[0]", "30"), ("ma[1]", "100"), ("ma[2]", "200"),
            ("v", "200"), ("fv", "A"), ("vc", "100"), ("fvc", "A"));

        Assert.Equal("12500.000", ep.At);
        Assert.Equal("0003700.00", ep.W1);
        Assert.Equal("0030", ep.Ma[0]);
        Assert.Equal("0100", ep.Ma[1]);
        Assert.Equal("0200", ep.Ma[2]);
        Assert.Equal('A', ep.VcKbn);
        Assert.Equal("100", ep.Vc);
    }

    // ── PS/P/UP(引込: 相数/線式/多スロット電圧) ────────────────────

    [Fact]
    public void PSは相数線式と3スロット電圧を整形する()
    {
        ElectricalParameters ep = Format("PS",
            ("p", "3"), ("w", "4"), ("fv", "A"),
            ("v[0]", "210"), ("v[1]", "105"), ("v[2]", "100"));

        Assert.Equal("3", ep.Ph2[0]);
        Assert.Equal("4", ep.Wr2[0]);
        Assert.Equal('A', ep.V2Kbn);
        Assert.Equal("000210.0", ep.V2[0]);
        Assert.Equal("000105.0", ep.V2[1]);
        Assert.Equal("000100.0", ep.V2[2]);
    }

    [Fact]
    public void Pは電線サイズと芯数本数を整形する()
    {
        ElectricalParameters ep = Format("P",
            ("p", "3"), ("w", "4"), ("fv", "A"), ("v[0]", "210"),
            ("sq", "38"), ("esq", "14"), ("c", "3"), ("k", "2"));

        Assert.Equal("3", ep.Ph2[0]);
        Assert.Equal("038.00", ep.Sq);      // set_9(sq,3,epasq,6,"%06.2f")
        Assert.Equal("014.00", ep.Esq);
        Assert.Equal('3', ep.C);             // ep->epac = c
        Assert.Equal('2', ep.Ksu);           // ep->epaksu = k
    }

    [Fact]
    public void Pは芯数本数未設定のとき0にする()
    {
        // 【C原典】ep->epac = u->p.c ? u->p.c : '0';
        ElectricalParameters ep = Format("P", ("p", "3"));
        Assert.Equal('0', ep.C);
        Assert.Equal('0', ep.Ksu);
    }

    [Fact]
    public void UPは定格電圧2のみ整形する()
    {
        ElectricalParameters ep = Format("UP", ("fv", "A"), ("v", "100"));
        Assert.Equal('A', ep.V2Kbn);
        Assert.Equal("000100.0", ep.V2[0]);
    }

    // ── MC/MG(電磁接触器: 接点数 AC/BC + 制御電圧) ───────────────────

    [Fact]
    public void MCは電流_容量_接点数_制御電圧を整形する()
    {
        ElectricalParameters ep = Format("MC",
            ("p", "3"), ("a", "20"), ("kw", "5.5"), ("v", "200"), ("fv", "A"),
            ("vc", "100"), ("fvc", "A"), ("ac", "2"), ("bc", "1"));

        Assert.Equal("003", ep.P);
        Assert.Equal("00020.000", ep.A2);
        Assert.Equal("0005500.00", ep.W1);
        Assert.Equal('A', ep.VcKbn);
        Assert.Equal("100", ep.Vc);
        Assert.Equal("02", ep.Ac);           // "%02.0f"
        Assert.Equal("01", ep.Bc);
    }

    [Fact]
    public void MGはエレメントとトリップ電流も整形する()
    {
        ElectricalParameters ep = Format("MG",
            ("p", "3"), ("e", "2"), ("a", "20"), ("at", "30"), ("kw", "3.7"),
            ("v", "200"), ("fv", "A"), ("vc", "100"), ("fvc", "D"), ("ac", "2"), ("bc", "2"));

        Assert.Equal("2", ep.E);
        Assert.Equal("00030.000", ep.At);
        Assert.Equal("00020.000", ep.A2);
        Assert.Equal("0003700.00", ep.W1);
        Assert.Equal('D', ep.VcKbn);
        Assert.Equal("02", ep.Ac);
    }

    // ── THR(サーマル) ───────────────────────────────────────────────

    [Fact]
    public void THRはエレメント_トリップ_容量_電圧を整形する()
    {
        ElectricalParameters ep = Format("THR",
            ("e", "3"), ("at", "15"), ("kw", "2.2"), ("v", "200"), ("fv", "A"));

        Assert.Equal("3", ep.E);
        Assert.Equal("00015.000", ep.At);
        Assert.Equal("0002200.00", ep.W1);
        Assert.Equal('A', ep.V2Kbn);
        Assert.Equal("000200.0", ep.V2[0]);
    }

    // ── SC(進相コンデンサ: KVAR/UF/HZ) ──────────────────────────────

    [Fact]
    public void SCは容量_静電容量_周波数を整形する()
    {
        ElectricalParameters ep = Format("SC",
            ("p", "3"), ("kvar", "50"), ("uf", "100"), ("v", "200"), ("fv", "A"), ("Hz", "60"));

        Assert.Equal("3", ep.Ph2[0]);
        Assert.Equal("050.00", ep.Kvar);     // "%06.2f"
        Assert.Equal("000100.0", ep.Uf);     // "%08.1f"
        Assert.Equal("60", ep.Hz);           // "%02.0f"
    }

    // ── NT(中性線端子台: 極数3桁/電流2桁) ───────────────────────────

    [Fact]
    public void NTは極数と電流を整形する()
    {
        ElectricalParameters ep = Format("NT",
            ("p", "100"), ("a", "60"), ("v", "200"), ("fv", "A"));

        Assert.Equal("100", ep.P);           // p は from_length=3
        Assert.Equal("00060.000", ep.A2);    // a は from_length=2
    }

    // ── WH(電力量計: 相/線式/1次2次電流電圧/HZ) ─────────────────────

    [Fact]
    public void WHは1次2次の電流電圧と周波数を整形する()
    {
        ElectricalParameters ep = Format("WH",
            ("p", "3"), ("w", "4"), ("sa", "5"), ("a", "30"),
            ("sv", "110"), ("v", "210"), ("fv", "A"), ("Hz", "60"));

        Assert.Equal("3", ep.Ph2[0]);
        Assert.Equal("4", ep.Wr2[0]);
        Assert.Equal("00005.000", ep.A1);    // sa→epaa1
        Assert.Equal("00030.000", ep.A2);    // a→epaa2
        Assert.Equal("000110.0", ep.V1[0]);  // sv→epav1[0]
        Assert.Equal("000210.0", ep.V2[0]);  // v→epav2[0]
        Assert.Equal("60", ep.Hz);
    }

    // ── VM/VT(電圧計/計器用変圧器) ──────────────────────────────────

    [Fact]
    public void VMは1次2次電圧を整形する()
    {
        ElectricalParameters ep = Format("VM", ("sv", "110"), ("v", "210"), ("fv", "A"));
        Assert.Equal("000110.0", ep.V1[0]);
        Assert.Equal("000210.0", ep.V2[0]);
        Assert.Equal("0000000000", ep.Va);   // VM は VA を触れない(memset '0' のまま・小数点なし)
    }

    [Fact]
    public void VTは定格容量VAも整形する()
    {
        ElectricalParameters ep = Format("VT",
            ("sv", "110"), ("v", "210"), ("fv", "A"), ("va", "50"));
        Assert.Equal("000110.0", ep.V1[0]);
        Assert.Equal("0000050.00", ep.Va);   // va,3桁→epava "%010.2f"
    }

    // ── AM(電流計) ──────────────────────────────────────────────────

    [Fact]
    public void AMは1次2次電流のみ整形する()
    {
        ElectricalParameters ep = Format("AM", ("sa", "50"), ("a", "5"));
        Assert.Equal("00050.000", ep.A1);
        Assert.Equal("00005.000", ep.A2);
    }

    // ── CT(計器用変流器: SA4桁/A3桁/VA2桁) ──────────────────────────

    [Fact]
    public void CTは1次2次電流と容量を整形する()
    {
        ElectricalParameters ep = Format("CT", ("sa", "1000"), ("a", "5"), ("va", "15"));
        Assert.Equal("01000.000", ep.A1);    // sa,4桁
        Assert.Equal("00005.000", ep.A2);    // a,3桁
        Assert.Equal("0000015.00", ep.Va);   // va,2桁
    }

    // ── VS/AS(電圧計・電流計切替スイッチ: 相数/線式のみ) ─────────────

    [Fact]
    public void VSは相数と線式のみ整形する()
    {
        ElectricalParameters ep = Format("VS", ("p", "3"), ("w", "4"));
        Assert.Equal("3", ep.Ph2[0]);
        Assert.Equal("4", ep.Wr2[0]);
    }

    [Fact]
    public void ASは相数と線式のみ整形する()
    {
        ElectricalParameters ep = Format("AS", ("p", "3"), ("w", "4"));
        Assert.Equal("3", ep.Ph2[0]);
        Assert.Equal("4", ep.Wr2[0]);
    }

    // ── TB/CON(端子台・コネクタ) ────────────────────────────────────

    [Fact]
    public void TBは極数_電流_電圧_電線サイズを整形する()
    {
        ElectricalParameters ep = Format("TB",
            ("p", "100"), ("a", "60"), ("v", "200"), ("fv", "A"), ("sq", "38"));

        Assert.Equal("100", ep.P);           // p は from_length=3
        Assert.Equal("00060.000", ep.A2);
        Assert.Equal("000200.0", ep.V2[0]);
        Assert.Equal("038.00", ep.Sq);       // sq,6桁→epasq "%06.2f"
    }

    [Fact]
    public void CONは極数1桁電流2桁で整形する()
    {
        ElectricalParameters ep = Format("CON",
            ("p", "3"), ("a", "20"), ("v", "200"), ("fv", "A"));

        Assert.Equal("003", ep.P);           // p は from_length=1
        Assert.Equal("00020.000", ep.A2);    // a は from_length=2
        Assert.Equal("000200.0", ep.V2[0]);
    }

    // ── TR(変圧器: 多スロット/タップインデックス/0詰めパッキング) ────

    [Fact]
    public void TRは1次電圧3スロットとタップインデックスを整形する()
    {
        // v1[1] の4文字目 'T' → タップ使用インデックス '2'
        ElectricalParameters ep = Format("TR",
            ("p1", "3"), ("w1", "4"),
            ("v1[0]", "210"), ("v1[1]", "105T"), ("v1[2]", "100"),
            ("va", "50"));

        Assert.Equal("3", ep.Ph1);
        Assert.Equal("4", ep.Wr1);
        Assert.Equal("000210.0", ep.V1[0]);
        Assert.Equal("000105.0", ep.V1[1]);
        Assert.Equal("000100.0", ep.V1[2]);
        Assert.Equal("2", ep.V1Idx);         // v1[1][3]=='T'
        Assert.Equal("0000050.00", ep.Va);   // va,6桁→epava "%010.2f"
    }

    [Fact]
    public void TRは2次相数を0でないものから順詰めする()
    {
        // p2 非0 → {p2→epaph2[0], p3→epaph2[1]}
        ElectricalParameters ep = Format("TR",
            ("p1", "3"), ("p2", "3"), ("p3", "1"), ("w2", "4"), ("w3", "2"));

        Assert.Equal("3", ep.Ph2[0]);
        Assert.Equal("1", ep.Ph2[1]);
        Assert.Equal("4", ep.Wr2[0]);
        Assert.Equal("2", ep.Wr2[1]);
    }

    [Fact]
    public void TRはp2が0のときp3を先頭に詰める()
    {
        // p2==0 → {p3→epaph2[0]}(epaph2[1] は 0 のまま)
        ElectricalParameters ep = Format("TR",
            ("p1", "3"), ("p2", "0"), ("p3", "1"), ("w2", "0"), ("w3", "2"));

        Assert.Equal("1", ep.Ph2[0]);
        Assert.Equal("0", ep.Ph2[1]);        // memset '0' のまま
        Assert.Equal("2", ep.Wr2[0]);
        Assert.Equal("0", ep.Wr2[1]);
    }

    [Fact]
    public void TRはfv2がAC_DC以外のときfv3で区分判定する()
    {
        // fv2 が 'A'/'D' 以外 → fv3 で判定
        ElectricalParameters ep = Format("TR",
            ("p1", "3"), ("fv2", "X"), ("fv3", "D"), ("v2[0]", "210"));
        Assert.Equal('D', ep.V2Kbn);
    }

    [Fact]
    public void TRは2次電圧のchk9非0のみ詰めタップインデックスを設定する()
    {
        // v2[0] 非0(タップ 'T')、v2[1] 0 でスキップ、v2[2] 非0
        ElectricalParameters ep = Format("TR",
            ("p1", "3"), ("fv2", "A"),
            ("v2[0]", "210T"), ("v2[1]", "0"), ("v2[2]", "100"),
            ("v3[0]", "105"), ("v3[1]", "50"));

        Assert.Equal("000210.0", ep.V2[0]);
        Assert.Equal("1", ep.V2Idx);         // v2[0][3]=='T' で '1'。v2[2]="100" は4文字目なしでタップ非設定
        // v3[0]→epav2[1]、v3[1]→epav2[2] で上書き
        Assert.Equal("000105.0", ep.V2[1]);
        Assert.Equal("000050.0", ep.V2[2]);
    }

    // ── 未収録予約語は '0' 埋めのまま ────────────────────────────────

    [Fact]
    public void 未収録予約語は既定値を返す()
    {
        ElectricalParameters ep = Format("XXX", ("p", "3"));
        // 【C原典】eparm_set の分岐に該当せず、ep は Main_Area_Clear の '0' 埋め(小数点なし)のまま。
        Assert.Equal("000", ep.P);
        Assert.Equal("0", ep.E);
        Assert.Equal("000000000", ep.Af);
        Assert.Equal('0', ep.V2Kbn);
    }
}
