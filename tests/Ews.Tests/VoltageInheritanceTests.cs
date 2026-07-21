using Ews.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 電圧値の変換・整列ユーティリティの検証。
/// 【C原典】toku/sekkei/src/Fyss14.c(Volt_Conv / Max_Volt / Right_Volt / Left_Volt)。
/// </summary>
public sealed class VoltageInheritanceTests
{
    [Theory]
    // 【C原典】varr テーブルの各行(変換前 → 変換後)。
    [InlineData(0, 0, 105, 0, 0, 100)]
    [InlineData(0, 0, 210, 0, 0, 200)]
    [InlineData(0, 210, 105, 0, 200, 100)]
    [InlineData(0, 0, 410, 0, 0, 400)]
    [InlineData(210, 210, 105, 200, 200, 100)]
    [InlineData(0, 0, 380, 0, 0, 380)]
    public void ConvertVoltage_テーブル一致で変換後電圧を返す(
        short k0, short k1, short k2, short e0, short e1, short e2)
    {
        short[] kv = [k0, k1, k2];
        short[] v = [0, 0, 0];

        short[] result = VoltageInheritance.ConvertVoltage(kv, v);

        Assert.Same(v, result);
        Assert.Equal([e0, e1, e2], result);
    }

    [Fact]
    public void ConvertVoltage_テーブル不一致なら出力配列は変更しない()
    {
        short[] kv = [1, 2, 3];              // どの行にも一致しない
        short[] v = [99, 88, 77];

        short[] result = VoltageInheritance.ConvertVoltage(kv, v);

        Assert.Equal([(short)99, (short)88, (short)77], result);
    }

    [Theory]
    [InlineData(210, 100, 0, 210)]          // 最大は先頭
    [InlineData(0, 200, 105, 200)]          // 最大は中間
    [InlineData(0, 0, 400, 400)]            // 最大は末尾
    public void MaxVoltage_最大値を先頭にし残りを0にする(
        short k0, short k1, short k2, short expectedMax)
    {
        short[] kv = [k0, k1, k2];

        VoltageInheritance.MaxVoltage(kv);

        Assert.Equal([expectedMax, (short)0, (short)0], kv);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]          // 全ゼロ
    [InlineData(105, 0, 0, 0, 0, 105)]      // 先頭のみ非ゼロ → 右端へ
    [InlineData(210, 105, 0, 0, 210, 105)]  // 先頭2要素が非ゼロ → 右詰め
    [InlineData(0, 0, 100, 0, 0, 100)]      // 既に右詰め → 変更なし
    [InlineData(0, 210, 100, 0, 210, 100)]  // 末尾非ゼロを含む → 変更なし
    public void RightAlignVoltage_非ゼロを右詰めする(
        short k0, short k1, short k2, short e0, short e1, short e2)
    {
        short[] kv = [k0, k1, k2];

        VoltageInheritance.RightAlignVoltage(kv);

        Assert.Equal([e0, e1, e2], kv);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]          // 全ゼロ
    [InlineData(0, 0, 105, 105, 0, 0)]      // 末尾のみ → 左端へ
    [InlineData(0, 210, 105, 210, 105, 0)]  // 先頭ゼロ → 左詰め
    [InlineData(0, 0, 100, 100, 0, 0)]      // 末尾のみ → 左端へ
    [InlineData(210, 0, 105, 210, 105, 0)]  // 中間ゼロ → 詰める
    [InlineData(105, 210, 100, 105, 210, 100)] // 既に左詰め → 変更なし
    public void LeftAlignVoltage_非ゼロを左詰めする(
        short k0, short k1, short k2, short e0, short e1, short e2)
    {
        short[] kv = [k0, k1, k2];

        VoltageInheritance.LeftAlignVoltage(kv);

        Assert.Equal([e0, e1, e2], kv);
    }
}
