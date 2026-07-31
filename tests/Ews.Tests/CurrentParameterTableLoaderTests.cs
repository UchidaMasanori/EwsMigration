using Ews.Data.Seeding;
using Ews.Domain.Analysis;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// <see cref="CurrentParameterTableLoader"/>(【C原典】Fyss3G_CnsPrmtpRead /
/// CnsSQsetRead / CnsA2setRead / CnsA1setRead)の解析処理の単体テスト。
/// </summary>
public class CurrentParameterTableLoaderTests
{
    // 各サンプルは先頭 2 行がコメント。改行は '\n' 明示(ローダーは '\r' を除去する)。
    private const string PrmtpSample =
        "/* amp001.cns  パラメータ設定タイプ */\n" +
        "/* 予約語| seq| prm_tp| cod[10] */\n" +
        "     MCB,    1,    1,    1, 1, 0, 0, 0,     1, 1, 0, 0, 0\n" +
        "     ELB,    2,    2,    1, 1, 1, 0, 0,     1, 1, 1, 0, 0\n";

    private const string SqsetSample =
        "/* amp002.cns  電線サイズ */\n" +
        "/* sq| 許容電流| 選定 */\n" +
        "     1.25,      10.0,         0\n" +
        "     2.00,      15.0,         1\n";

    // M(1文字), S(1文字), HA(2文字)。HA は幅2指定で係数が直前 S 行(1.1)を持ち越す。
    private const string A2setSample =
        "/* amp003.cns  定格電流２ */\n" +
        "/* 負荷種類| 定格電流２| 回路圧| 回路相数 */\n" +
        "      M,        1.0,        999,  3    \n" +
        "      S,        1.1,        999,  0    \n" +
        "     HA,        1.4,        999,  0    \n";

    // 3 行目のコメント "/*    3.0   */" は先頭 2 行のみ読み飛ばすため数値行として扱われる。
    private const string A1setSample =
        "/* amp004.cns  定格電流１ */\n" +
        "/* 定格電流1 */\n" +
        "/*    3.0   */\n" +
        "      5.0\n" +
        "     10.0\n";

    [Fact]
    public void ParseParameterSettingTypes_予約語と設定タイプと設定フラグ10個を取り込む()
    {
        var list = CurrentParameterTableLoader.ParseParameterSettingTypes(PrmtpSample);

        Assert.Equal(2, list.Count);

        Assert.Equal("MCB", list[0].ReservedWord);
        Assert.Equal(1, list[0].SequenceNumber);
        Assert.Equal(1, list[0].SettingType);
        Assert.Equal(new[] { 1, 1, 0, 0, 0, 1, 1, 0, 0, 0 }, list[0].SettingFlags);

        Assert.Equal("ELB", list[1].ReservedWord);
        Assert.Equal(2, list[1].SequenceNumber);
        Assert.Equal(2, list[1].SettingType);
        Assert.Equal(new[] { 1, 1, 1, 0, 0, 1, 1, 1, 0, 0 }, list[1].SettingFlags);
    }

    [Fact]
    public void ParseWireSizeSettings_電線サイズと許容電流と選定フラグを取り込む()
    {
        var list = CurrentParameterTableLoader.ParseWireSizeSettings(SqsetSample);

        Assert.Equal(2, list.Count);
        Assert.Equal(1.25, list[0].WireSize);
        Assert.Equal(10.0, list[0].AllowableCurrent);
        Assert.Equal(0, list[0].SelectionFlag);
        Assert.Equal(2.00, list[1].WireSize);
        Assert.Equal(15.0, list[1].AllowableCurrent);
        Assert.Equal(1, list[1].SelectionFlag);
    }

    [Fact]
    public void ParseRatedCurrent2Settings_1文字負荷種類は全項目を取り込む()
    {
        var list = CurrentParameterTableLoader.ParseRatedCurrent2Settings(A2setSample);

        Assert.Equal(3, list.Count);

        Assert.Equal("M", list[0].LoadKind);
        Assert.Equal('3', list[0].CircuitPhase);
        Assert.Equal(999, list[0].CircuitVoltage);
        Assert.Equal(1.0, list[0].Coefficient);

        Assert.Equal("S", list[1].LoadKind);
        Assert.Equal('0', list[1].CircuitPhase);
        Assert.Equal(999, list[1].CircuitVoltage);
        Assert.Equal(1.1, list[1].Coefficient);
    }

    [Fact]
    public void ParseRatedCurrent2Settings_2文字負荷種類は係数を直前行から持ち越す_sscanf桁落ち再現()
    {
        var list = CurrentParameterTableLoader.ParseRatedCurrent2Settings(A2setSample);

        // HA は負荷種類のみ更新され、係数/電圧/相数は直前 S 行(1.1/999/'0')を持ち越す。
        // ファイル上の 1.4 ではなく 1.1 になるのが C 原典の忠実な挙動(【C原典】sscanf 幅2の桁落ち)。
        RatedCurrent2Setting ha = list[2];
        Assert.Equal("HA", ha.LoadKind);
        Assert.Equal(1.1, ha.Coefficient);
        Assert.Equal(999, ha.CircuitVoltage);
        Assert.Equal('0', ha.CircuitPhase);
    }

    [Fact]
    public void ParseRatedCurrent1Settings_3行目コメントが数値行化し先頭が0になる_sscanf失敗持ち越し再現()
    {
        var list = CurrentParameterTableLoader.ParseRatedCurrent1Settings(A1setSample);

        // 先頭 2 行のみコメント読み飛ばしのため、3 行目 "/*    3.0   */" は変換失敗→持ち越し(初期 0)。
        Assert.Equal(3, list.Count);
        Assert.Equal(0.0, list[0].RatedCurrent);
        Assert.Equal(5.0, list[1].RatedCurrent);
        Assert.Equal(10.0, list[2].RatedCurrent);
    }

    [Fact]
    public void DataLines_末尾改行の有無に関わらずデータ件数は同じ()
    {
        // 末尾改行ありは末尾の空要素のみ落とすためデータ 2 件。末尾改行なしも同数。
        int withNewline = CurrentParameterTableLoader.ParseWireSizeSettings(SqsetSample).Count;
        int withoutNewline = CurrentParameterTableLoader.ParseWireSizeSettings(SqsetSample.TrimEnd('\n')).Count;

        Assert.Equal(2, withNewline);
        Assert.Equal(2, withoutNewline);
    }
}
