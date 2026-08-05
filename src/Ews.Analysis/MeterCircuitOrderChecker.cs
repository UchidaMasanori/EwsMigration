using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 計器回路(行種コード PM)機器の並びをチェックし、CT/WH の行種を同一機器認識番号で相互補完する。
/// 【C原典】toku/sekkei/src/Fyss14.c <c>Keiki_Kairo_Check</c>(5480)。
///
/// 主回路設計エリアを走査し、計器回路機器の予約語妥当性(FY-648E)・AS/VS の回路要素区分
/// (FY-645E)・SC の位置(FY-656E)を検証する。異常時は <see cref="CircuitParseError"/> を返す
/// (C 原典の return(2) 相当)。正常時は null。あわせて同一機器認識番号の CT/WH に行種を複写する。
/// </summary>
public static class MeterCircuitOrderChecker
{
    /// <summary>データ追番・記述行/桁のフィールド幅。</summary>
    private const int FieldWidth = 3;

    /// <summary>
    /// 計器回路機器の並びをチェックする。【C原典】Keiki_Kairo_Check(Fyss14.c:5480)。
    /// </summary>
    /// <param name="mains">主回路レコード列。CT/WH の行種は同一機器認識番号で in-place 複写される。【C原典】*Pmaina(件数 *Pmainc)。</param>
    /// <returns>異常時は <see cref="CircuitParseError"/>(=C の return(2))、正常時は null(=return(0))。</returns>
    public static CircuitParseError? Check(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;

            // 【C原典】行種コード PM の計器回路機器の予約語妥当性チェック。
            // 大きな AND 条件は改訂<35>で実質「予約語 CR かつ tokkbn 空」に縮退する(C 原典の論理をそのまま移植)。
            if (d.LineTypeCode == "PM")
            {
                if (d.ReservedWord != "CT" && d.ReservedWord != "AS" &&
                    d.ReservedWord != "WH" && d.ReservedWord != "AM" &&
                    d.ReservedWord != "VM" && d.ReservedWord != "VT" &&
                    d.ReservedWord != "VS" && d.ReservedWord != "THR" &&
                    d.ReservedWord != "F" && d.ReservedWord != "WL" &&
                    d.ReservedWord != "OL" && d.ReservedWord != "RL" &&
                    d.ReservedWord != "GL" && d.ReservedWord != "FL" &&
                    d.ReservedWord != "BL" && d.ReservedWord != "PLTR" &&
                    d.ReservedWord != "LA" && d.ReservedWord != "MCB" &&
                    d.ReservedWord != "SC" &&
                    (d.ReservedWord == "CR" && d.SpecialReservedWordKind == ' ') &&   // 改訂<35>
                    d.ReservedWord != "HM")
                {
                    return new CircuitParseError(
                        "FY-648E",
                        EquipmentParameterFormatter.Stoi(d.DescriptionRow, FieldWidth),
                        EquipmentParameterFormatter.Stoi(d.DescriptionColumn, FieldWidth),   // 改訂<20>
                        "FYMEE80");
                }
            }

            // 【C原典】CT(kiryoso='2')の後続で同一機器認識番号の CT へ自身の行種を複写。
            if (d.ReservedWord == "CT" && d.CircuitElement == '2')
            {
                for (int j = i + 1; j < mains.Count; j++)
                {
                    MainCircuitData dj = mains[j].Data;
                    if (dj.ReservedWord == "CT" && dj.IdentityNumber == d.IdentityNumber)
                    {
                        dj.LineTypeGroupNumber = d.LineTypeGroupNumber;
                        dj.LineTypeCode = d.LineTypeCode;
                        break;
                    }
                }
            }

            // 【C原典】WH(kiryoso='1')の後続で同一機器認識番号の WH の行種を自身へ複写。
            if (d.ReservedWord == "WH" && d.CircuitElement == '1')
            {
                for (int j = i + 1; j < mains.Count; j++)
                {
                    MainCircuitData dj = mains[j].Data;
                    if (dj.ReservedWord == "WH" && dj.IdentityNumber == d.IdentityNumber)
                    {
                        d.LineTypeGroupNumber = dj.LineTypeGroupNumber;
                        d.LineTypeCode = dj.LineTypeCode;
                        break;
                    }
                }
            }

            // 【C原典】AS は回路要素区分='2'、VS は '3' か '4'、SC(PM)は自身を親とする後続機器が無いこと。
            if (d.ReservedWord == "AS")
            {
                if (d.CircuitElement != '2')
                {
                    return new CircuitParseError(
                        "FY-645E",
                        EquipmentParameterFormatter.Stoi(d.DescriptionRow, FieldWidth),
                        EquipmentParameterFormatter.Stoi(d.DescriptionColumn, FieldWidth),
                        "FYMEE80");
                }
            }
            else if (d.ReservedWord == "VS")
            {
                if (d.CircuitElement != '3' && d.CircuitElement != '4')
                {
                    return new CircuitParseError(
                        "FY-645E",
                        EquipmentParameterFormatter.Stoi(d.DescriptionRow, FieldWidth),
                        EquipmentParameterFormatter.Stoi(d.DescriptionColumn, FieldWidth),
                        "FYMEE80");
                }
            }
            else if (d.ReservedWord == "SC")
            {
                if (d.LineTypeCode == "PM")
                {
                    // 【C原典】(maina+i)->dt.kiryoso == '3'; は無効式(原典のまま/副作用なし)。
                    for (int j = i + 1; j < mains.Count; j++)
                    {
                        // 【C原典】内側 if(gyocd=="PM") は maina[i] を参照するため常に真(else break は到達しない)。
                        if (mains[j].Data.ParentSequenceNumber == mains[i].SequenceNumber)
                        {
                            return new CircuitParseError(
                                "FY-656E",
                                EquipmentParameterFormatter.Stoi(d.DescriptionRow, FieldWidth),
                                EquipmentParameterFormatter.Stoi(d.DescriptionColumn, FieldWidth),
                                "FYMEE80");
                        }
                    }
                }
            }
        }

        return null;
    }
}
