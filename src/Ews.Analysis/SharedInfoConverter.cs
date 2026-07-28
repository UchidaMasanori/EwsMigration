using Ews.Domain.Analysis;
using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// 直近上下位共用情報の数値変換。
/// 【C原典】Fysk01_Change_Chokin(toku/sekkei/src/Fysk01.c:4232)。
///
/// 直近上下位ファイルデータ(<see cref="NearestRankReference"/>)の共用情報部
/// (文字列)を数値化し、定格値チェック(Fysk02_Check_Teikakuchi)が参照する
/// <see cref="NumericSharedInfo"/>(=kyoyojg_s)を得る。
/// </summary>
public static class SharedInfoConverter
{
    /// <summary>【C原典】TOL=0.001(fyrt808.h)。</summary>
    private const double Tolerance = 0.001;

    /// <summary>
    /// 共用情報部を数値化する。
    /// 【C原典】Fysk01_Change_Chokin(cd, scd)。
    /// </summary>
    public static NumericSharedInfo Convert(NearestRankReference reference)
    {
        NearestRankSharedInfo jg = reference.SharedInfo;

        // 感度電流(MA): 原典は 4 枠読むが数値構造体 km_s.kyomad は 3 枠。
        // 4 枠目は隣接領域へ溢れて直後に上書きされ破棄されるため、3 個のみ保持する。
        var sensitivity = new double[3];
        for (int i = 0; i < 3; i++)
        {
            sensitivity[i] = Stof(Value(jg.SensitivityCurrents, i), 4);
        }

        // 区分は原典で以下のように複写される(コピー由来の仕様):
        //   kv2k1 = kv1k1 / kvck1 = kv1k1 / kvck2 = kv2k2 / kvck3 = kv2k3
        char kv1k1 = Kind(jg.PrimaryVoltageKinds, 0);
        char kv1k2 = Kind(jg.PrimaryVoltageKinds, 1);
        char kv2k2 = Kind(jg.SecondaryVoltageKinds, 1);
        char kv2k3 = Kind(jg.SecondaryVoltageKinds, 2);

        double vcfrom = Stof(reference.ControlVoltageRangeFrom, 3) / 100.0;
        if (Math.Abs(vcfrom) < Tolerance)
        {
            vcfrom = 1.0;
        }

        double vcto = Stof(reference.ControlVoltageRangeTo, 3) / 100.0;
        if (Math.Abs(vcto) < Tolerance)
        {
            vcto = 1.0;
        }

        return new NumericSharedInfo
        {
            MainPowerSharedAcDc = jg.MainPowerSharedAcDc,
            ControlPowerSharedAcDc = jg.ControlPowerSharedAcDc,
            SensitivityCurrents = sensitivity,
            PrimaryVoltageValues =
            [
                Stof(Value(jg.PrimaryVoltageValues, 0), 3),
                Stof(Value(jg.PrimaryVoltageValues, 1), 3),
                Stof(Value(jg.PrimaryVoltageValues, 2), 3),
            ],
            PrimaryVoltageKinds = [kv1k1, kv1k2],
            SecondaryVoltageValues =
            [
                Stof(Value(jg.SecondaryVoltageValues, 0), 3),
                Stof(Value(jg.SecondaryVoltageValues, 1), 3),
                Stof(Value(jg.SecondaryVoltageValues, 2), 3),
                Stof(Value(jg.SecondaryVoltageValues, 3), 3),
            ],
            // kv2k1 は一次電圧区分(kv1k1)から複写される(原典の仕様)。
            SecondaryVoltageKinds = [kv1k1, kv2k2, kv2k3],
            ControlVoltageValues =
            [
                Stof(Value(jg.ControlVoltageValues, 0), 3),
                Stof(Value(jg.ControlVoltageValues, 1), 3),
                Stof(Value(jg.ControlVoltageValues, 2), 3),
                Stof(Value(jg.ControlVoltageValues, 3), 3),
            ],
            // kvck1=kv1k1 / kvck2=kv2k2 / kvck3=kv2k3(制御電圧自身の区分は使われない)。
            ControlVoltageKinds = [kv1k1, kv2k2, kv2k3],
            ControlVoltageRangeFrom = vcfrom,
            ControlVoltageRangeTo = vcto,
        };
    }

    private static string Value(IReadOnlyList<string> values, int index)
        => index >= 0 && index < values.Count ? values[index] : string.Empty;

    private static char Kind(IReadOnlyList<char> kinds, int index)
        => index >= 0 && index < kinds.Count ? kinds[index] : ' ';

    private static double Stof(string? value, int size) => EquipmentParameterFormatter.Stof(value, size);
}
