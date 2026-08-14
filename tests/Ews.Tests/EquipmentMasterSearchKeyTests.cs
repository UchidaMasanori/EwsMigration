using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 機器マスタ基本検索キー(<see cref="EquipmentMasterSearchKey"/>)の検証。
/// 【C原典】Fysk01_Kiki_Read(toku/sekkei/src/Fysk01.c:2821, 改訂&lt;19&gt;の HL 除去)。
/// </summary>
public sealed class EquipmentMasterSearchKeyTests
{
    private const int Len = EquipmentMasterSearchKey.ParameterTypeLength;

    /// <summary>7 スロット(各 7 バイト)を連結して 49 バイトのパラメータタイプを作る。</summary>
    private static string Ptype(params string[] slots)
    {
        var chars = new char[Len];
        Array.Fill(chars, ' ');
        for (int i = 0; i < slots.Length && i < EquipmentMasterSearchKey.ParameterTypeSlotCount; i++)
        {
            string s = slots[i].Length > 7 ? slots[i][..7] : slots[i];
            for (int j = 0; j < s.Length; j++)
            {
                chars[(i * 7) + j] = s[j];
            }
        }
        return new string(chars);
    }

    [Fact]
    public void 正規化結果は常に49バイト固定幅になる()
    {
        Assert.Equal(Len, EquipmentMasterSearchKey.NormalizeParameterType("AT").Length);
        Assert.Equal(Len, EquipmentMasterSearchKey.NormalizeParameterType(string.Empty).Length);
        Assert.Equal(Len, EquipmentMasterSearchKey.NormalizeParameterType(new string('X', 80)).Length);
    }

    [Fact]
    public void 先頭がHLのスロットは先頭2バイトのみ空白化される()
    {
        string result = EquipmentMasterSearchKey.NormalizeParameterType(Ptype("HLABC"));

        // スロット0の "HL" が空白化され、残り "ABC" は保持される。
        Assert.Equal("  ABC", result[..5]);
        Assert.Equal(Ptype("  ABC"), result);
    }

    [Fact]
    public void HLのみのスロットは空白7バイトになる()
    {
        string result = EquipmentMasterSearchKey.NormalizeParameterType(Ptype("HL"));
        Assert.Equal(new string(' ', Len), result);
    }

    [Fact]
    public void 複数スロットのHLが個別に除去される()
    {
        string result = EquipmentMasterSearchKey.NormalizeParameterType(Ptype("HL", "AT100", "HLXY"));

        Assert.Equal(Ptype("", "AT100", "  XY"), result);
    }

    [Fact]
    public void HLで始まらないスロットは変更されない()
    {
        string source = Ptype("AT100", "H", "LH", "AHL");
        Assert.Equal(source, EquipmentMasterSearchKey.NormalizeParameterType(source));
    }

    [Fact]
    public void パラメータタイプが49バイトを超える入力は先頭へ切り詰められる()
    {
        string source = Ptype("HLABC") + "EXTRA";
        string result = EquipmentMasterSearchKey.NormalizeParameterType(source);

        Assert.Equal(Len, result.Length);
        Assert.Equal(Ptype("  ABC"), result);
    }
}
