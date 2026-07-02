using System.Text;
using Ews.Domain.Common;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 固定長 Shift-JIS コーデックの検証。
/// 【C原典】cmnchar.c / CpyNullStop の挙動(全角空白パディング・前後トリム)を確認する。
/// </summary>
public sealed class FixedFieldCodecTests
{
    [Fact]
    public void ReadText_全角空白パディングをトリムする()
    {
        // "北海道支店" を全角空白で右パディングした 20 バイト(全角10文字)を再現。
        Encoding sjis = FixedFieldCodec.ShiftJis;
        byte[] field = sjis.GetBytes("北海道支店" + new string('\u3000', 5));

        string text = FixedFieldCodec.ReadText(field, 0, field.Length);

        Assert.Equal("北海道支店", text);
    }

    [Fact]
    public void WriteText_then_ReadText_ラウンドトリップする()
    {
        var buffer = new byte[15];
        FixedFieldCodec.WriteText(buffer, 0, 15, "AB12345");

        string text = FixedFieldCodec.ReadText(buffer, 0, 15);

        Assert.Equal("AB12345", text);
    }

    [Fact]
    public void EquipmentMaster_FromFixedRecord_主要フィールドを抽出する()
    {
        // FYDM805 のレイアウト(yoyaku=0, mkcd=8, hinban=140, hinmei=155, pstring=180)に沿った
        // 最小レコードを構築して抽出を確認する。
        var record = new byte[EquipmentMaster.RecordLength];
        record.AsSpan().Fill((byte)' ');
        FixedFieldCodec.WriteText(record, 0, 8, "RESV");      // pkey.yoyaku
        FixedFieldCodec.WriteText(record, 8, 3, "CAN");       // pkey.mkcd
        FixedFieldCodec.WriteText(record, 140, 15, "AB12345"); // hinban
        FixedFieldCodec.WriteText(record, 155, 25, "ブレーカ"); // hinmei

        EquipmentMaster master = EquipmentMaster.FromFixedRecord(record);

        Assert.Equal("RESV", master.ReservedWord);
        Assert.Equal("CAN", master.MakerCode);
        Assert.Equal("AB12345", master.PartNumber);
        Assert.Equal("ブレーカ", master.PartName);
    }
}
