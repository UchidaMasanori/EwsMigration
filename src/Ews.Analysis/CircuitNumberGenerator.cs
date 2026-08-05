using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 主回路要素へ生成回路番号を割り振る採番器。
/// 【C原典】toku/sekkei/src/Fyss14.c <c>Find_Kairo_Bangou</c> と静的グローバル
/// <c>struct BANGOU Kbangoua</c>(136)・<c>PM_flg</c>(147)。
///
/// C 原典では Main_Rank_Set が処理開始時に Kbangoua を 0 クリアするため、
/// 本移植では 1 系統(1 回の走査)につき本クラスを 1 インスタンス生成して用いる。
/// 各要素の並び替え区分(narakbn)が '3'/'4'(新規)のときにカウンタを進め、
/// それ以外(既存参照)では現在値を返す。回路分類(kairobun)と回路相(sou)で
/// 1 相系/3 相系の別カウンタを使い分ける。
/// </summary>
public sealed class CircuitNumberGenerator
{
    private short _mno1;   // 回路番号 M1
    private short _bno1;   // 回路番号 B1
    private short _ono1;   // 回路番号 O1
    private short _nno1;   // 回路番号  1
    private short _bno3;   // 回路番号 B3(M3 は C 原典の struct に存在するが Find では未使用のため省略)
    private short _ono3;   // 回路番号 O3
    private short _nno3;   // 回路番号  3
    private short _sno;    // 回路番号 S
    private short _pmFlag; // PM_flg: 直前の M 採番が行種 PM だったか

    /// <summary>
    /// 1 要素の生成回路番号を求める。【C原典】Find_Kairo_Bangou(Fyss14.c:4508)。
    /// narakbn が '3'/'4' のときは該当カウンタを進め、それ以外は現在値を返す。
    /// </summary>
    /// <param name="data">対象要素。gyocd/kairobun/narakbn/yoyaku/fp.fpac を参照する。</param>
    /// <param name="sou">回路相(P 行種の kpaph)。'1' で 1 相系、それ以外で 3 相系。</param>
    public short Find(MainCircuitData data, char sou)
    {
        ArgumentNullException.ThrowIfNull(data);

        // gyosyu = gyocd の先頭 3 バイト(空白詰め)。
        string gyosyu = (data.LineTypeCode ?? string.Empty).PadRight(3)[..3];
        char bunrui = data.CircuitClass;

        if (data.SortKind == '3' || data.SortKind == '4')
        {
            // 回路分類 M は 1 相/3 相を区別せず Mno1 を採番する(950531)。
            if (bunrui == 'M')
            {
                _mno1++;
                // 行種 "PM" のときは Mno1 を元へ戻す(PM の F/WL と M の ELB を同番号にするため、96.01.18)。
                if (gyosyu == "PM ")
                {
                    _pmFlag = 1;
                    return _mno1--;
                }
                _pmFlag = 0;
                return _mno1;
            }

            if (sou == '1')
            {
                if (bunrui == 'B')
                {
                    _bno1++;
                    return _bno1;
                }
                if (bunrui == 'O')
                {
                    _ono1++;
                    return _ono1;
                }
                if (bunrui == 'S')
                {
                    _sno++;
                    return _sno;
                }
                if (bunrui == ' ')
                {
                    if (gyosyu == "B  ")
                    {
                        // 制御電源の F(ヒューズ)はカウントアップしない(96.01.18)。
                        if (IsControlPowerFuse(data))
                        {
                            _nno1++;
                            return _nno1--;
                        }
                        _nno1++;
                        return _nno1;
                    }
                    return 0;
                }
                return 0;
            }
            else
            {
                if (bunrui == 'B')
                {
                    _bno3++;
                    return _bno3;
                }
                if (bunrui == 'O')
                {
                    _ono3++;
                    return _ono3;
                }
                if (bunrui == 'S')
                {
                    _sno++;
                    return _sno;
                }
                if (bunrui == ' ')
                {
                    if (gyosyu == "B  ")
                    {
                        if (IsControlPowerFuse(data))
                        {
                            _nno3++;
                            return _nno3--;
                        }
                        _nno3++;
                        return _nno3;
                    }
                    return 0;
                }
                return 0;
            }
        }
        else
        {
            // 既存参照(カウントアップなし)。
            if (bunrui == 'M')
            {
                // 直前が PM 採番なら次の番号(PM_flg==1、96.01.18)。
                return _pmFlag == 1 ? (short)(_mno1 + 1) : _mno1;
            }

            if (sou == '1')
            {
                if (bunrui == 'B')
                {
                    return _bno1;
                }
                if (bunrui == 'O')
                {
                    return _ono1;
                }
                if (bunrui == 'S')
                {
                    return _sno;
                }
                if (bunrui == ' ')
                {
                    return gyosyu == "B  " ? _nno1 : (short)0;
                }
                return 0;
            }
            else
            {
                if (bunrui == 'B')
                {
                    return _bno3;
                }
                if (bunrui == 'O')
                {
                    return _ono3;
                }
                if (bunrui == 'S')
                {
                    return _sno;
                }
                if (bunrui == ' ')
                {
                    return gyosyu == "B  " ? _nno3 : (short)0;
                }
                return 0;
            }
        }
    }

    /// <summary>制御電源の F(ヒューズ)判定。【C原典】yoyaku=="F" かつ fp.fpac!="  "(96.01.18)。</summary>
    private static bool IsControlPowerFuse(MainCircuitData data)
    {
        string fpac = (data.AttachedParameter.ControlPowerNumber ?? string.Empty).PadRight(2)[..2];
        return data.ReservedWord == "F" && fpac != "  ";
    }
}
