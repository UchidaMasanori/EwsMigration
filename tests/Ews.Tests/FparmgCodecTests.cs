using System.Text;
using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Common;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 付属パラメータ(<see cref="AttachedParameters"/> = C <c>struct fparmg</c>)の
/// 固定長コーデック(<see cref="FparmgCodec"/>)と、mainfile_set の fp 設定移植
/// (<see cref="EquipmentParameterFormatter.FparmSet"/>, 【C原典】Fyss1f.c:1957-2205)の検証。
/// </summary>
public sealed class FparmgCodecTests
{
    private static readonly Encoding Cp932 = FixedFieldCodec.ShiftJis;

    // ── 固定長レイアウト ────────────────────────────────────────────

    [Fact]
    public void レコード長は157バイトである()
    {
        Assert.Equal(157, FparmgCodec.RecordLength);
        Assert.Equal(157, FparmgCodec.Serialize(new AttachedParameters()).Length);
    }

    [Fact]
    public void 既定値はMain_Area_Clearの初期値と一致する()
    {
        // 【C原典】Main_Area_Clear: syukairo 全体を '0' で埋めた後、一部 fp フィールドを ' ' で上書き。
        byte[] b = FparmgCodec.Serialize(new AttachedParameters());
        int pos = 0;

        AssertFill(b, ref pos, 2, (byte)' ');   // fpalw1
        AssertFill(b, ref pos, 7, (byte)'0');   // fpalw2
        Assert.Equal((byte)' ', b[pos]); pos += 1; // fpalwkbn
        AssertFill(b, ref pos, 3, (byte)'0');   // fpalv[0]
        AssertFill(b, ref pos, 3, (byte)'0');   // fpalv[1]
        AssertFill(b, ref pos, 20, (byte)' ');  // fpaln[0]
        AssertFill(b, ref pos, 20, (byte)' ');  // fpaln[1]
        AssertFill(b, ref pos, 20, (byte)' ');  // fpacm1
        AssertFill(b, ref pos, 20, (byte)' ');  // fpacm2
        AssertFill(b, ref pos, 3, (byte)'0');   // fpacglno
        AssertFill(b, ref pos, 25, (byte)' ');  // fpaitpt
        Assert.Equal((byte)' ', b[pos]); pos += 1; // spkvn
        AssertFill(b, ref pos, 4, (byte)'0');   // fpah
        AssertFill(b, ref pos, 4, (byte)'0');   // fpaw
        AssertFill(b, ref pos, 4, (byte)'0');   // fpad
        AssertFill(b, ref pos, 3, (byte)'0');   // fpasglno
        Assert.Equal((byte)' ', b[pos]); pos += 1; // fpag
        Assert.Equal((byte)' ', b[pos]); pos += 1; // fpahu
        Assert.Equal((byte)' ', b[pos]); pos += 1; // fpas
        Assert.Equal((byte)' ', b[pos]); pos += 1; // fpak
        Assert.Equal((byte)' ', b[pos]); pos += 1; // fpamh
        AssertFill(b, ref pos, 2, (byte)' ');   // fpac
        AssertFill(b, ref pos, 3, (byte)' ');   // fpamk
        AssertFill(b, ref pos, 6, (byte)' ');   // fpaup
        Assert.Equal((byte)'0', b[pos]); pos += 1; // tikbn

        Assert.Equal(157, pos);
    }

    private static void AssertFill(byte[] b, ref int pos, int width, byte expected)
    {
        for (int i = 0; i < width; i++)
        {
            Assert.Equal(expected, b[pos + i]);
        }
        pos += width;
    }

    [Fact]
    public void 論理値は先頭左詰めで残りが既定バイト埋めされる()
    {
        var fp = new AttachedParameters
        {
            LoadKind = "M",
            LoadCapacity = "0003700",
            LoadUnitKind = 'W',
            MakerCode = "AB",
        };
        byte[] b = FparmgCodec.Serialize(fp);

        // fpalw1[2]: "M" + ' ' 埋め
        Assert.Equal("M ", Cp932.GetString(b, 0, 2));
        // fpalw2[7]: "0003700"(既に7桁)
        Assert.Equal("0003700", Cp932.GetString(b, 2, 7));
        // fpalwkbn: 'W'
        Assert.Equal((byte)'W', b[9]);
        // fpamk[3]: "AB" + ' ' 埋め(オフセットは既定テストのレイアウトに準拠)
        int makerOffset = 2 + 7 + 1 + 3 + 3 + 20 + 20 + 20 + 20 + 3 + 25 + 1 + 4 + 4 + 4 + 3 + 1 + 1 + 1 + 1 + 1 + 2;
        Assert.Equal("AB ", Cp932.GetString(b, makerOffset, 3));
    }

    // ── 往復(Serialize/Deserialize) ──────────────────────────────

    [Fact]
    public void 往復で全フィールドが再現される()
    {
        // 論理値(桁詰め前の生値)。テキスト系は全角を含み、コーデックが幅ぴったりへ空白埋めする。
        var fp = new AttachedParameters
        {
            LoadKind = "M ",
            LoadCapacity = "0003700",
            LoadUnitKind = 'W',
            Comment = "テストコメント",
            LineTypeComment = "行種コメント",
            CommentGroupNumber = "001",
            ItemName = "品名テスト",
            SpFutureMountKind = '1',
            DimensionHeight = "0100",
            DimensionWidth = "0200",
            DimensionDepth = "0300",
            DimensionGroupNumber = "002",
            ExternalMountKind = 'G',
            SealKind = 'H',
            SuppliedKind = 'S',
            PartitionKind = 'K',
            MeterSealKind = 'M',
            ControlPowerNumber = "05",
            MakerCode = "MTS",
            PowerVoltage = "AC100 ",
            DoorMountKind = 'T',
        };
        fp.LoadVoltage[0] = "100";
        fp.LoadVoltage[1] = "200";
        fp.LoadName[0] = "負荷名称A";
        fp.LoadName[1] = "負荷名称B";

        byte[] bytes = FparmgCodec.Serialize(fp);
        AttachedParameters r = FparmgCodec.Deserialize(bytes);

        // 往復不変条件: 復元→再直列化がバイト一致する(固定長の安定性)。
        Assert.Equal(bytes, FparmgCodec.Serialize(r));

        // 幅ぴったりの ASCII/文字フィールドは値がそのまま復元される。
        Assert.Equal("M ", r.LoadKind);
        Assert.Equal("0003700", r.LoadCapacity);
        Assert.Equal('W', r.LoadUnitKind);
        Assert.Equal("100", r.LoadVoltage[0]);
        Assert.Equal("200", r.LoadVoltage[1]);
        Assert.Equal("001", r.CommentGroupNumber);
        Assert.Equal('1', r.SpFutureMountKind);
        Assert.Equal("0100", r.DimensionHeight);
        Assert.Equal("0200", r.DimensionWidth);
        Assert.Equal("0300", r.DimensionDepth);
        Assert.Equal("002", r.DimensionGroupNumber);
        Assert.Equal('G', r.ExternalMountKind);
        Assert.Equal('H', r.SealKind);
        Assert.Equal('S', r.SuppliedKind);
        Assert.Equal('K', r.PartitionKind);
        Assert.Equal('M', r.MeterSealKind);
        Assert.Equal("05", r.ControlPowerNumber);
        Assert.Equal("MTS", r.MakerCode);
        Assert.Equal("AC100 ", r.PowerVoltage);
        Assert.Equal('T', r.DoorMountKind);

        // 全角を含むテキスト系は空白埋め後の末尾トリムで元の論理値へ戻る。
        Assert.Equal("テストコメント", r.Comment.TrimEnd());
        Assert.Equal("行種コメント", r.LineTypeComment.TrimEnd());
        Assert.Equal("品名テスト", r.ItemName.TrimEnd());
        Assert.Equal("負荷名称A", r.LoadName[0].TrimEnd());
        Assert.Equal("負荷名称B", r.LoadName[1].TrimEnd());
    }

    [Fact]
    public void 既定値の往復で既定値が再現される()
    {
        byte[] bytes = FparmgCodec.Serialize(new AttachedParameters());
        AttachedParameters r = FparmgCodec.Deserialize(bytes);
        byte[] again = FparmgCodec.Serialize(r);
        Assert.Equal(bytes, again);
    }

    [Fact]
    public void レコードが短い場合は例外()
    {
        Assert.Throws<ArgumentException>(() => FparmgCodec.Deserialize(new byte[156]));
    }

    // ── FparmSet(mainfile_set fp ブロック移植) ──────────────────

    private static AttachedParameters Fparm(EquipmentTableEntry sKiki, string effectiveLoadName = "")
    {
        var fp = new AttachedParameters();
        new EquipmentParameterFormatter().FparmSet(sKiki, fp, effectiveLoadName);
        return fp;
    }

    [Fact]
    public void 負荷容量_kWは1000倍しW単位でセットする()
    {
        // 【C原典】"3.7KW" → f=3.7, K により ×1000 → 3700, "%07.0f" → "0003700", 単位 'W'。
        var fp = Fparm(new EquipmentTableEntry { LoadCapacity = "3.7KW" });
        Assert.Equal("0003700", fp.LoadCapacity);
        Assert.Equal('W', fp.LoadUnitKind);
    }

    [Fact]
    public void 負荷容量_VAはそのまま数値でV単位をセットする()
    {
        var fp = Fparm(new EquipmentTableEntry { LoadCapacity = "500VA" });
        Assert.Equal("0000500", fp.LoadCapacity);
        Assert.Equal('V', fp.LoadUnitKind);
    }

    [Fact]
    public void 負荷電圧はDLVから3バイトで切り詰める()
    {
        var sKiki = new EquipmentTableEntry();
        sKiki.LoadVoltage[0] = "200";
        sKiki.LoadVoltage[1] = "100";
        var fp = Fparm(sKiki);
        Assert.Equal("200", fp.LoadVoltage[0]);
        Assert.Equal("100", fp.LoadVoltage[1]);
    }

    [Fact]
    public void 負荷名称は有効負荷名称からセットする()
    {
        var fp = Fparm(new EquipmentTableEntry(), "モータ");
        Assert.Equal("モータ", fp.LoadName[0]);
    }

    [Fact]
    public void コメント_品名_メーカーをセットする()
    {
        var sKiki = new EquipmentTableEntry
        {
            Comment = "コメント",
            ItemName = "品名ABC",
            Maker = "MT",
        };
        var fp = Fparm(sKiki);
        Assert.Equal("コメント", fp.Comment);
        Assert.Equal("品名ABC", fp.ItemName);
        Assert.Equal("MT", fp.MakerCode);
    }

    [Fact]
    public void 寸法はDSPをアスタリスクで分割し4桁化する()
    {
        // 【C原典】DSP="100*200*300" → fpah="0100" fpaw="0200" fpad="0300"。
        var fp = Fparm(new EquipmentTableEntry { SpecialDimension = "100*200*300" });
        Assert.Equal("0100", fp.DimensionHeight);
        Assert.Equal("0200", fp.DimensionWidth);
        Assert.Equal("0300", fp.DimensionDepth);
    }

    [Theory]
    [InlineData((short)12, (short)0, 'G')]
    [InlineData((short)13, (short)0, 'H')]
    [InlineData((short)14, (short)0, 'K')]
    [InlineData((short)15, (short)0, 'S')]
    [InlineData((short)16, (short)0, 'M')]
    public void 括弧区分は区分値に応じて対応区分をセットする(short k1, short k2, char expected)
    {
        var fp = Fparm(new EquipmentTableEntry { Kakko1 = k1, Kakko2 = k2 });
        char actual = expected switch
        {
            'G' => fp.ExternalMountKind,
            'H' => fp.SealKind,
            'K' => fp.PartitionKind,
            'S' => fp.SuppliedKind,
            'M' => fp.MeterSealKind,
            _ => '\0',
        };
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void 制御電源番号はC_Flgが1のとき2桁ゼロ埋めでセットする()
    {
        var sKiki = new EquipmentTableEntry
        {
            PowerSourceFlag = '1',
            PowerSourceNumber = 5,
        };
        var fp = Fparm(sKiki);
        Assert.Equal("05", fp.ControlPowerNumber);
    }

    [Fact]
    public void 制御電源番号はC_Flgが1以外のときセットしない()
    {
        var sKiki = new EquipmentTableEntry
        {
            PowerSourceFlag = ' ',
            PowerSourceNumber = 5,
        };
        var fp = Fparm(sKiki);
        Assert.Equal(string.Empty, fp.ControlPowerNumber);
    }

    [Fact]
    public void SP区分はSP_Flgをそのままセットする()
    {
        var fp = Fparm(new EquipmentTableEntry { SpecialFlag = '1' });
        Assert.Equal('1', fp.SpFutureMountKind);
    }
}
