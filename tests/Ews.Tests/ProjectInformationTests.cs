using Ews.Domain.Common;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 物件情報(FYDF801 物件共通情報)固定長レコードのデコード検証。
/// 【C原典】struct FYDF801 (toku/include/common/fydf801.h, レコード長 1200)。
/// 実データ未配置のため、既知値を各オフセットへ配置した合成レコードで
/// オフセット/幅の正しさを検証する(実データ検証はエクスポート受領後に追加)。
/// </summary>
public sealed class ProjectInformationTests
{
    [Fact]
    public void RecordLength_は1200バイト()
    {
        Assert.Equal(1200, ProjectInformation.RecordLength);
    }

    [Fact]
    public void FromFixedRecord_主要フィールドを正しいオフセットで読む()
    {
        byte[] rec = new byte[ProjectInformation.RecordLength];
        rec.AsSpan().Fill((byte)' '); // CHAR フィールド既定(半角空白)

        // 各モデル化フィールドを既知値で配置(WriteText は offset/width へ CP932・右空白pad)。
        FixedFieldCodec.WriteText(rec, 0, 7, "AF01234");        // 依頼番号
        FixedFieldCodec.WriteText(rec, 7, 2, "01");             // 明細番号
        FixedFieldCodec.WriteText(rec, 9, 10, "AF1234-01A");    // 図番(上10桁)
        FixedFieldCodec.WriteText(rec, 19, 5, "12345");         // 図番(下5桁)
        FixedFieldCodec.WriteText(rec, 55, 30, "名古屋営業所"); // 営業所名(全角含む)
        FixedFieldCodec.WriteText(rec, 85, 14, "担当太郎");     // 担当者名
        FixedFieldCodec.WriteText(rec, 99, 30, "件名その１");   // 件名1
        FixedFieldCodec.WriteText(rec, 129, 30, "件名その２");  // 件名2
        FixedFieldCodec.WriteText(rec, 159, 2, "02");           // 製作仕様区分
        FixedFieldCodec.WriteText(rec, 161, 34, "建設省準拠仕様"); // 仕様名称
        FixedFieldCodec.WriteText(rec, 195, 1, "1");            // 図面種別
        FixedFieldCodec.WriteText(rec, 196, 1, "2");            // 図面ランク
        FixedFieldCodec.WriteText(rec, 199, 1, "3");            // 周波数区分

        var info = ProjectInformation.FromFixedRecord(rec);

        Assert.Equal("AF01234", info.RequestNumber);
        Assert.Equal("01", info.DetailNumber);
        Assert.Equal("AF1234-01A", info.DrawingNumberUpper);
        Assert.Equal("12345", info.DrawingNumberLower);
        Assert.Equal("名古屋営業所", info.SalesOfficeName);
        Assert.Equal("担当太郎", info.StaffName);
        Assert.Equal("件名その１", info.ProjectName1);
        Assert.Equal("件名その２", info.ProjectName2);
        Assert.Equal("02", info.ManufacturingSpecKind);
        Assert.Equal("建設省準拠仕様", info.SpecificationName);
        Assert.Equal("1", info.DrawingKind);
        Assert.Equal("2", info.DrawingRank);
        Assert.Equal("3", info.FrequencyKind);
    }

    [Fact]
    public void FromFixedRecord_エンジン参照フィールドを専用オフセットで読む()
    {
        // 回路解析エンジンが参照する周波数区分(hzkbn @199)と製作仕様区分(sshiykbn @159-160)。
        // 近傍を非空で埋めても該当バイトのみ読み取れることを確認する。
        byte[] rec = new byte[ProjectInformation.RecordLength];
        rec.AsSpan().Fill((byte)'X');

        FixedFieldCodec.WriteText(rec, 159, 2, "07"); // 製作仕様区分(公団)
        FixedFieldCodec.WriteText(rec, 199, 1, "2");  // 周波数区分(60Hz)

        var info = ProjectInformation.FromFixedRecord(rec);

        Assert.Equal("07", info.ManufacturingSpecKind);
        Assert.Equal("2", info.FrequencyKind);
    }

    [Fact]
    public void FromFixedRecord_空白フィールドは空文字列になる()
    {
        // 物件共通情報レコードは明細番号ブランク。全角/半角空白は Trim される。
        byte[] rec = new byte[ProjectInformation.RecordLength];
        rec.AsSpan().Fill((byte)' ');

        var info = ProjectInformation.FromFixedRecord(rec);

        Assert.Equal(string.Empty, info.DetailNumber);
        Assert.Equal(string.Empty, info.FrequencyKind);
        Assert.Equal(string.Empty, info.ManufacturingSpecKind);
        Assert.Equal(string.Empty, info.ProjectName1);
    }
}
