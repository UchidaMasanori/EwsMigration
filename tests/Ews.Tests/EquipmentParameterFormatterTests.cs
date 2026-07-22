using Ews.Analysis;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 電気パラメータ整形(型式展開、<see cref="EquipmentParameterFormatter"/>)の検証。
/// 【C原典】toku/sekkei/src/Fyss1f.c eparm_set / set_9 / Stof。
/// 本フェーズ(Wave 1)は遮断器系 MCB/ELB/MMCB/ELMB/SB を対象とする。
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
