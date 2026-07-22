using System;
using System.Collections.Generic;
using System.Text;
using Ews.Domain.Analysis;
using Ews.Domain.Common;

namespace Ews.Analysis;

/// <summary>
/// 代入文(予約語文の「(TAG=値)」)チェックの移植。
/// 【C原典】toku/sekkei/src/Fyss1b.c
///   - Check_KikiMeisyou() の代入文 while ループ … 予約語部の後に続く代入文を順に取り出す。
///   - Check_Dainyuu()  … 代入文 1 件を検証し、機器テーブル(KIKITABLE)へ値を格納する。
///   - kikitable_add()(Fyss11.c) … タグ("MK"/"CM"/"LW"/"LN"/"LV0"/"UP"/"NO"/"HAI"/"B"/…)ごとに
///                                  KIKITABLE の該当フィールド(DMK/DCM/DLW/DLN/DLV/DUP/DNO/…)へ設定する。
///
/// C原典の文字ポインタ走査(ph/nh/yh と parmstring/namestring/yoyakustring、iskanji による
/// 全角2バイト処理)は <see cref="ByteCursor"/> により CP932 バイト列上で忠実に再現する。
///
/// 【保留(段階移植)】
///   - Check_IT(FYDF817 機器マスター品名索引 ISAM)・Check_MK(FYDM801 メーカー名称マスター ISAM)
///     の妥当性検証は ISAM 依存のため未実施(値の格納のみ行う)。
///   - Check_LW の Keisan_LW(負荷容量の加算計算・正規化)は未移植(形式検証のみ、DLW は生値)。
///   - Check_NO のハイフン連番展開(PropGetKnoStruct/PropDevelopKno)・Qrespo 全角(CheckQrespoZenkaku1z)
///     は未移植(カンマ連結・範囲'>'展開のみ、ハイフンは非展開でそのまま格納)。
///   - Check_Dainyuu 末尾の Check_Haifunn('S')(分岐配置 -C/-SP)・PULASU(型式展開 DTYPE, Fysk01 依存)は未移植。
/// </summary>
public sealed partial class CircuitStringChecker
{
    /// <summary>代入文シンボル。【C原典】Fyss1b.c nextyoyasugo() の戻り(sym_XX)。</summary>
    private enum AssignSymbol
    {
        Other, End,
        MK, IT, CM, SP, LN, HAI, LW, LV, UP, NO, B, WHAI, BK, BKO, CNCT, Atmark, Pulasu,
    }

    /// <summary>代入文値のシンボル。【C原典】nextname/nextdelimetor/nextunit/nextkigou の戻り。</summary>
    private enum ValueSymbol
    {
        Other, End, RKakko,
        Aster, Point, Greater, Kanma, Hyphen,
        V, W, KW, VA, KVA,
        M, HA, H, S, FL, NA, TR, YA, YS,
    }

    /// <summary>
    /// 文字列取り出しカーソル(CP932 バイト単位)。
    /// 【C原典】ph/ph2・nh/nh2・yh/yh2 と parmstring/namestring/yoyakustring。
    /// <see cref="Cur"/> が現在バイト(=ph/nh/yh)、<see cref="Pos"/> が次に読む位置(=各 *string ポインタ)。
    /// </summary>
    private sealed class ByteCursor
    {
        private readonly byte[] _buf;
        private int _pos;

        public ByteCursor(byte[] buffer)
        {
            _buf = buffer;
            _pos = 0;
            Cur = (byte)' ';
        }

        /// <summary>現在バイト。【C原典】ph/nh/yh。</summary>
        public byte Cur { get; private set; }

        /// <summary>現在バイトが全角のときの第2バイト。【C原典】ph2/nh2/yh2。</summary>
        public byte Cur2 { get; private set; }

        /// <summary>次に読む位置。【C原典】parmstring/namestring/yoyakustring。</summary>
        public int Pos => _pos;

        /// <summary>現在バイトの明示設定。【C原典】yh='(' / yh='+' の復元。</summary>
        public void SetCur(byte value) => Cur = value;

        /// <summary>1 文字読み進める(全角は2バイト)。【C原典】nextphar/nextnhar/nextyhar。</summary>
        public byte Advance()
        {
            if (_pos < _buf.Length && _buf[_pos] != 0)
            {
                Cur = _buf[_pos++];
                Cur2 = 0;
                if (IsKanjiLead(Cur) && _pos < _buf.Length)
                {
                    Cur2 = _buf[_pos++];
                }
            }
            else
            {
                Cur = 0;
                Cur2 = 0;
            }

            return Cur;
        }

        /// <summary>指定オフセットのバイト値(範囲外は 0)。</summary>
        public byte PeekAt(int offset) => (offset >= 0 && offset < _buf.Length) ? _buf[offset] : (byte)0;

        /// <summary>指定オフセットから ASCII 文字列に一致するか。【C原典】memcmp(ptr, "…", len)==0。</summary>
        public bool MatchAt(int offset, string ascii)
        {
            if (offset < 0 || offset + ascii.Length > _buf.Length)
            {
                return false;
            }

            for (int i = 0; i < ascii.Length; i++)
            {
                if (_buf[offset + i] != (byte)ascii[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    // ============================================================
    //  代入文ループ(Check_KikiMeisyou の while 部分)
    // ============================================================

    /// <summary>
    /// 予約語部の後に続く代入文を順に取り出して検証・格納する。
    /// 【C原典】Check_KikiMeisyou(Fyss1b.c) の while(findparm!=sym_END){ Check_Dainyuu } ループ。
    /// 予約語部(先頭)は既に <see cref="CheckKikiMeisyou"/> で処理済みのため、最初の
    /// Find_KikiMeisyou は予約語部を読み飛ばして最初の代入文シンボルを得る用途で呼ぶ。
    /// </summary>
    /// <summary>
    /// 予約語文から予約語部(最初の代入文「(TAG=」/「+(」の直前まで)を取り出す。
    /// 【C原典】Check_KikiMeisyou(Fyss1b.c) の最初の Find_KikiMeisyou が返す yoyakugo。
    /// 電気パラメータ・予約語番号の判定は代入文を含まないこの部分のみを対象とし、
    /// 代入文「(LW=…)」等の '=' を電気パラメータの '=' と誤認しないようにする。
    /// 予約語 "PT(" のように代入語でない '(' は予約語部に残る(nextyoyasugo と同一挙動)。
    /// </summary>
    private string ExtractReservedClause(string control)
    {
        var yc = new ByteCursor(ToShiftJis(control));
        yc.SetCur((byte)' '); // 【C原典】yoyakustart(control): yh=' '
        FindKikiMeisyou(yc, out string reserved);
        return reserved;
    }

    private void ProcessAssignmentStatements(EquipmentTableEntry kiki, string control, int lineNumber, CircuitParseResult result)
    {
        var yc = new ByteCursor(ToShiftJis(control));
        yc.SetCur((byte)' '); // 【C原典】yoyakustart(control): yh=' '

        // 予約語部を読み飛ばし、最初の代入文シンボルを得る。【C原典】Find_KikiMeisyou(yoyakugo)
        AssignSymbol findparm = FindKikiMeisyou(yc, out _);

        while (findparm != AssignSymbol.End)
        {
            if (IsAssignmentSymbol(findparm))
            {
                AssignSymbol oldparm = findparm; // 【C原典】oldparm = findparm
                findparm = FindKikiMeisyou(yc, out string param); // 代入文を取り出す
                short ret = CheckDainyuu(oldparm, param, kiki, out string errorCode);
                if (ret != 0)
                {
                    result.Errors.Add(new CircuitParseError(errorCode, lineNumber, 1, "FYMEE80"));
                    return;
                }
            }
            else
            {
                // 【C原典】上記の代入文以外はエラー(return FALSE)。
                result.Errors.Add(new CircuitParseError("FY-613E", lineNumber, 1, "FYMEE80"));
                return;
            }
        }
    }

    /// <summary>代入文として処理するシンボルか。【C原典】Check_KikiMeisyou の if 条件。</summary>
    private static bool IsAssignmentSymbol(AssignSymbol sym) => sym is
        AssignSymbol.MK or AssignSymbol.IT or AssignSymbol.CM or AssignSymbol.SP or
        AssignSymbol.LV or AssignSymbol.LW or AssignSymbol.LN or AssignSymbol.NO or
        AssignSymbol.UP or AssignSymbol.Atmark or AssignSymbol.HAI or AssignSymbol.B or
        AssignSymbol.WHAI or AssignSymbol.BK or AssignSymbol.BKO or AssignSymbol.CNCT or
        AssignSymbol.Pulasu;

    // ============================================================
    //  Check_Dainyuu 本体
    // ============================================================

    /// <summary>
    /// 代入文 1 件を検証し、機器テーブルへ値を格納する。【C原典】Check_Dainyuu(Fyss1b.c)。
    /// 戻り値は 0=正常、!=0 はエラーコード(C原典のリターンコードと同義)。
    /// </summary>
    private short CheckDainyuu(AssignSymbol findsym, string param, EquipmentTableEntry kiki, out string errorCode)
    {
        errorCode = string.Empty;

        // 【C原典】namestart(&param[1]); Check_Kakko(dainyuu)
        var nc = new ByteCursor(ToShiftJisSkipFirst(param));
        nc.SetCur((byte)' ');
        CheckKakko(nc, out string dainyuu);
        // 【C原典】Fyss11_TSU_mojichk(TSU パターン番号回避)は未移植。

        switch (findsym)
        {
            case AssignSymbol.B: // 【C原典】(B=W/L/R) 特注盤分岐配列
                if (dainyuu is "W" or "L" or "R")
                {
                    ApplyAssignmentTag("B", dainyuu, kiki);
                }
                else
                {
                    errorCode = "FY-647E";
                    return 647;
                }

                break;

            case AssignSymbol.WHAI: // 【C原典】(WHAI=L/R) WH配置指定
                if (dainyuu is "L" or "R")
                {
                    ApplyAssignmentTag("WHAI", dainyuu, kiki);
                }
                else
                {
                    errorCode = "FY-647E";
                    return 647;
                }

                break;

            case AssignSymbol.BK: // 【C原典】(BK=…) 備考 MAX16 (32byte+'$'数)
                {
                    int lmt = 32;
                    foreach (char c in dainyuu)
                    {
                        if (c == '$')
                        {
                            lmt++;
                        }
                    }

                    // 【C原典】PropChkMojiExcept(禁則文字チェック)は未移植。
                    if (ShiftJisLength(dainyuu) <= lmt)
                    {
                        ApplyAssignmentTag("BK", dainyuu, kiki);
                    }
                    else
                    {
                        errorCode = "FY-647E";
                        return 647;
                    }
                }

                break;

            case AssignSymbol.BKO: // 【C原典】(BKO=…) "出線サイズ$…sq" 編集
                {
                    string shu = ShiftJisLength(dainyuu) <= 6 ? $"出線サイズ${dainyuu}sq" : dainyuu;
                    int lmt = 32;
                    foreach (char c in shu)
                    {
                        if (c == '$')
                        {
                            lmt++;
                        }
                    }

                    if (ShiftJisLength(shu) <= lmt)
                    {
                        ApplyAssignmentTag("BK", shu, kiki);
                    }
                    else
                    {
                        errorCode = "FY-647E";
                        return 647;
                    }
                }

                break;

            case AssignSymbol.MK: // 【C原典】(MK=…) メーカー
                ApplyAssignmentTag("MK", dainyuu, kiki);
                // 【C原典】Check_MK(FYDM801 ISAM)は未実施(保留)。
                break;

            case AssignSymbol.IT: // 【C原典】(IT=…) 品名
                ApplyAssignmentTag("IT", dainyuu, kiki);
                // 【C原典】Check_IT(FYDF817 ISAM)は未実施(保留)。
                break;

            case AssignSymbol.CM: // 【C原典】(CM=…) コメント
                ApplyAssignmentTag("CM", dainyuu, kiki);
                if (!CheckCm(dainyuu))
                {
                    errorCode = "FY-637E";
                    return 637;
                }

                break;

            case AssignSymbol.SP: // 【C原典】(SP=…) 寸法 数値*数値*数値
                ApplyAssignmentTag("SP", dainyuu, kiki);
                if (!CheckSp(dainyuu))
                {
                    errorCode = "FY-638E";
                    return 638;
                }

                break;

            case AssignSymbol.LW: // 【C原典】(LW=…) 負荷容量
                ApplyAssignmentTag("LW", dainyuu, kiki);
                if (!CheckLw(ref dainyuu))
                {
                    errorCode = "FY-639E";
                    return 639;
                }

                // 【C原典】memset(DLW); strcpy(DLW, dainyuu)。Keisan_LW 正規化は保留のため生値を再設定。
                kiki.LoadCapacity = dainyuu;
                break;

            case AssignSymbol.HAI: // 【C原典】(HAI=L/C/T/O/D) 特注送り配置
                if (!CheckHai(dainyuu))
                {
                    errorCode = "FY-647E";
                    return 647;
                }

                ApplyAssignmentTag("HAI", dainyuu, kiki);
                break;

            case AssignSymbol.LN: // 【C原典】(LN=…) 負荷名称
                // 【C原典】PropChkMojiExcept(禁則文字チェック)は未移植。
                if (!CheckLn(ref dainyuu))
                {
                    errorCode = "FY-640E";
                    return 640;
                }

                ApplyAssignmentTag("LN", dainyuu, kiki);
                break;

            case AssignSymbol.LV: // 【C原典】(LV=…V) 負荷電圧
                ApplyAssignmentTag("LV0", dainyuu, kiki);
                if (!CheckLv(dainyuu))
                {
                    errorCode = "FY-641E";
                    return 641;
                }

                break;

            case AssignSymbol.UP: // 【C原典】(UP=…) 有電圧電源
                ApplyAssignmentTag("UP", dainyuu, kiki);
                if (!CheckUp(dainyuu))
                {
                    errorCode = "FY-642E";
                    return 642;
                }

                break;

            case AssignSymbol.NO: // 【C原典】(NO=…) 回路番号
                if (!CheckNo(dainyuu, out string newdainyuu))
                {
                    errorCode = "FY-643E";
                    return 643;
                }

                // 【C原典】kikitable_add("NO", &newdainyuu[1], …)。先頭の区切り','を除く。
                ApplyAssignmentTag("NO", newdainyuu.Length > 0 ? newdainyuu[1..] : string.Empty, kiki);
                break;

            case AssignSymbol.Atmark: // 【C原典】(@=…) ノーチェック
                break;

            case AssignSymbol.CNCT: // 【C原典】(CNCT=POW) 下部出線
                if (dainyuu == "POW")
                {
                    ApplyAssignmentTag("CNCT", dainyuu, kiki);
                }
                else
                {
                    errorCode = "FY-647E";
                    return 647;
                }

                break;

            case AssignSymbol.Pulasu: // 【C原典】+(…) 型式(DTYPE)。型式展開(Fysk01)依存のため保留。
                break;

            default:
                errorCode = "FY-647E";
                return 647;
        }

        // 【C原典】Find_Name(')')=sym_RKAKKO else FY-612E
        ValueSymbol findname = FindName(nc, out _);
        if (findname != ValueSymbol.RKakko)
        {
            errorCode = "FY-612E";
            return 612;
        }

        findname = FindName(nc, out string tail);
        if (findname == ValueSymbol.End && tail.Length == 0)
        {
            return 0;
        }

        // 【C原典】Check_Haifunn(dainyuu, 'S')(分岐配置 -C/-SP)は未移植。
        // 残余がある場合は従来通り FY-613E とする。
        if (findname != ValueSymbol.End || !IsNullString(tail))
        {
            errorCode = "FY-613E";
            return 613;
        }

        return 0;
    }

    // ============================================================
    //  予約語文スキャナ(yoyaku cursor)
    // ============================================================

    /// <summary>区切り文字まで取り出す。【C原典】Find_KikiMeisyou(Fyss1b.c)。</summary>
    private static AssignSymbol FindKikiMeisyou(ByteCursor yc, out string result)
    {
        var bytes = new List<byte>();
        AssignSymbol sym = NextYoyasugo(yc);
        while (sym == AssignSymbol.Other)
        {
            bytes.Add(yc.Cur);
            if (IsKanjiLead(yc.Cur))
            {
                bytes.Add(yc.Cur2);
            }

            yc.Advance();
            sym = NextYoyasugo(yc);
        }

        result = DecodeBytes(bytes);
        return sym;
    }

    /// <summary>目下のシンボル判定。【C原典】nextyoyasugo(Fyss1b.c)。</summary>
    private static AssignSymbol NextYoyasugo(ByteCursor yc)
    {
        if (yc.Cur == (byte)'(')
        {
            while (yc.PeekAt(yc.Pos) == (byte)' ')
            {
                yc.Advance();
            }

            yc.SetCur((byte)'('); // 【C原典】yh='('

            if (yc.MatchAt(yc.Pos, "MK=")) { AdvanceN(yc, 3); return AssignSymbol.MK; }
            if (yc.MatchAt(yc.Pos, "IT=")) { AdvanceN(yc, 3); return AssignSymbol.IT; }
            if (yc.MatchAt(yc.Pos, "CM=")) { AdvanceN(yc, 3); return AssignSymbol.CM; }
            if (yc.MatchAt(yc.Pos, "SP=")) { AdvanceN(yc, 3); return AssignSymbol.SP; }
            if (yc.MatchAt(yc.Pos, "LN=")) { AdvanceN(yc, 3); return AssignSymbol.LN; }
            if (yc.MatchAt(yc.Pos, "HAI=")) { AdvanceN(yc, 4); return AssignSymbol.HAI; }
            if (yc.MatchAt(yc.Pos, "LW=")) { AdvanceN(yc, 3); return AssignSymbol.LW; }
            if (yc.MatchAt(yc.Pos, "LV=")) { AdvanceN(yc, 3); return AssignSymbol.LV; }
            if (yc.MatchAt(yc.Pos, "UP=")) { AdvanceN(yc, 3); return AssignSymbol.UP; }
            if (yc.MatchAt(yc.Pos, "NO=")) { AdvanceN(yc, 3); return AssignSymbol.NO; }
            if (yc.MatchAt(yc.Pos, "B=")) { AdvanceN(yc, 2); return AssignSymbol.B; }
            if (yc.MatchAt(yc.Pos, "WHAI=")) { AdvanceN(yc, 5); return AssignSymbol.WHAI; }
            if (yc.MatchAt(yc.Pos, "BK=")) { AdvanceN(yc, 3); return AssignSymbol.BK; }
            if (yc.MatchAt(yc.Pos, "BKO=")) { AdvanceN(yc, 4); return AssignSymbol.BKO; }
            if (yc.MatchAt(yc.Pos, "CNCT=")) { AdvanceN(yc, 5); return AssignSymbol.CNCT; }
            if (yc.MatchAt(yc.Pos, "@=")) { AdvanceN(yc, 2); return AssignSymbol.Atmark; }
            return AssignSymbol.Other;
        }

        if (yc.Cur == (byte)'+')
        {
            while (yc.PeekAt(yc.Pos) == (byte)' ')
            {
                yc.Advance();
            }

            yc.SetCur((byte)'+'); // 【C原典】yh='+'
            if (yc.MatchAt(yc.Pos, "("))
            {
                yc.Advance();
                return AssignSymbol.Pulasu;
            }

            return AssignSymbol.Other;
        }

        if (yc.Cur == 0)
        {
            return AssignSymbol.End;
        }

        return AssignSymbol.Other;
    }

    // ============================================================
    //  代入文値スキャナ(name cursor)
    // ============================================================

    /// <summary>括弧のバランス箇所まで抽出。【C原典】Check_Kakko(Fyss1b.c)。</summary>
    private static bool CheckKakko(ByteCursor nc, out string result)
    {
        var bytes = new List<byte>();
        int kakko = 1; // 前括弧なしなので 1
        nc.Advance();
        while (nc.Cur != 0)
        {
            if (nc.Cur == (byte)'(')
            {
                kakko++;
            }
            else if (nc.Cur == (byte)')')
            {
                kakko--;
            }

            if (kakko <= 0)
            {
                break;
            }

            bytes.Add(nc.Cur);
            if (IsKanjiLead(nc.Cur))
            {
                bytes.Add(nc.Cur2);
            }

            nc.Advance();
        }

        result = DecodeBytes(bytes);
        return kakko == 0;
    }

    /// <summary>区切り文字')'まで抽出。【C原典】Find_Name(Fyss1b.c)。</summary>
    private static ValueSymbol FindName(ByteCursor nc, out string result)
    {
        var bytes = new List<byte>();
        ValueSymbol sym = NextName(nc);
        while (sym == ValueSymbol.Other)
        {
            bytes.Add(nc.Cur);
            if (IsKanjiLead(nc.Cur))
            {
                bytes.Add(nc.Cur2);
            }

            nc.Advance();
            sym = NextName(nc);
        }

        result = DecodeBytes(bytes);
        return sym;
    }

    /// <summary>目下のシンボル判定。【C原典】nextname(Fyss1b.c)。</summary>
    private static ValueSymbol NextName(ByteCursor nc)
    {
        while (nc.Cur == (byte)' ')
        {
            nc.Advance();
        }

        if (nc.Cur == (byte)')')
        {
            nc.Advance();
            return ValueSymbol.RKakko;
        }

        if (nc.Cur == 0)
        {
            return ValueSymbol.End;
        }

        return ValueSymbol.Other;
    }

    // ============================================================
    //  値形式スキャナ(parm cursor)
    // ============================================================

    /// <summary>'*''.''>'',''-'まで抽出。【C原典】Find_Delimetor(Fyss1b.c)。</summary>
    private static ValueSymbol FindDelimetor(ByteCursor pc, out string result)
    {
        var bytes = new List<byte>();
        ValueSymbol sym = NextDelimetor(pc);
        while (sym == ValueSymbol.Other)
        {
            bytes.Add(pc.Cur);
            if (IsKanjiLead(pc.Cur))
            {
                bytes.Add(pc.Cur2);
            }

            pc.Advance();
            sym = NextDelimetor(pc);
        }

        result = DecodeBytes(bytes);
        return sym;
    }

    /// <summary>数値区切り判定。【C原典】nextdelimetor(Fyss1b.c)。</summary>
    private static ValueSymbol NextDelimetor(ByteCursor pc)
    {
        while (pc.Cur == (byte)' ')
        {
            pc.Advance();
        }

        switch ((char)pc.Cur)
        {
            case '*': pc.Advance(); return ValueSymbol.Aster;
            case '.': pc.Advance(); return ValueSymbol.Point;
            case '>': pc.Advance(); return ValueSymbol.Greater;
            case ',': pc.Advance(); return ValueSymbol.Kanma;
            case '-': pc.Advance(); return ValueSymbol.Hyphen;
        }

        if (pc.Cur == 0)
        {
            return ValueSymbol.End;
        }

        return ValueSymbol.Other;
    }

    /// <summary>'V''W''KW''VA''KVA'まで抽出。【C原典】Find_Unit(Fyss1b.c)。</summary>
    private static ValueSymbol FindUnit(ByteCursor pc, out string result)
    {
        var bytes = new List<byte>();
        ValueSymbol sym = NextUnit(pc);
        while (sym == ValueSymbol.Other)
        {
            bytes.Add(pc.Cur);
            if (IsKanjiLead(pc.Cur))
            {
                bytes.Add(pc.Cur2);
            }

            pc.Advance();
            sym = NextUnit(pc);
        }

        result = DecodeBytes(bytes);
        return sym;
    }

    /// <summary>単位区切り判定。【C原典】nextunit(Fyss1b.c)。</summary>
    private static ValueSymbol NextUnit(ByteCursor pc)
    {
        while (pc.Cur == (byte)' ')
        {
            pc.Advance();
        }

        if (pc.MatchAt(pc.Pos - 1, "KVA")) { AdvanceN(pc, 3); return ValueSymbol.KVA; }
        if (pc.MatchAt(pc.Pos - 1, "VA")) { AdvanceN(pc, 2); return ValueSymbol.VA; }
        if (pc.MatchAt(pc.Pos - 1, "V")) { AdvanceN(pc, 1); return ValueSymbol.V; }
        if (pc.MatchAt(pc.Pos - 1, "KW")) { AdvanceN(pc, 2); return ValueSymbol.KW; }
        if (pc.MatchAt(pc.Pos - 1, "W")) { AdvanceN(pc, 1); return ValueSymbol.W; }
        if (pc.Cur == 0)
        {
            return ValueSymbol.End;
        }

        return ValueSymbol.Other;
    }

    /// <summary>負荷記号(M/HA/H/S/W/FL/NA/TR/YA/YS)判定。【C原典】nextkigou(Fyss1b.c)。</summary>
    private static ValueSymbol NextKigou(ByteCursor pc)
    {
        while (pc.Cur == (byte)' ')
        {
            pc.Advance();
        }

        if (pc.Cur == (byte)'K')
        {
            pc.Advance();
        }

        if (pc.MatchAt(pc.Pos - 1, "M")) { AdvanceN(pc, 1); return ValueSymbol.M; }
        if (pc.MatchAt(pc.Pos - 1, "HA")) { AdvanceN(pc, 2); return ValueSymbol.HA; }
        if (pc.MatchAt(pc.Pos - 1, "H")) { AdvanceN(pc, 1); return ValueSymbol.H; }
        if (pc.MatchAt(pc.Pos - 1, "S")) { AdvanceN(pc, 1); return ValueSymbol.S; }
        if (pc.MatchAt(pc.Pos - 1, "W")) { AdvanceN(pc, 1); return ValueSymbol.W; }
        if (pc.MatchAt(pc.Pos - 1, "FL")) { AdvanceN(pc, 2); return ValueSymbol.FL; }
        if (pc.MatchAt(pc.Pos - 1, "NA")) { AdvanceN(pc, 2); return ValueSymbol.NA; }
        if (pc.MatchAt(pc.Pos - 1, "TR")) { AdvanceN(pc, 2); return ValueSymbol.TR; }
        if (pc.MatchAt(pc.Pos - 1, "YA")) { AdvanceN(pc, 2); return ValueSymbol.YA; }
        if (pc.MatchAt(pc.Pos - 1, "YS")) { AdvanceN(pc, 2); return ValueSymbol.YS; }
        if (pc.Cur == 0)
        {
            return ValueSymbol.End;
        }

        return ValueSymbol.Other;
    }

    // ============================================================
    //  バリデータ
    // ============================================================

    /// <summary>コメント検証(20byte 以内)。【C原典】Check_CM(Fyss1b.c)。</summary>
    private static bool CheckCm(string dainyuu) => ShiftJisLength(dainyuu) <= 20;

    /// <summary>寸法検証(数値*数値*数値)。【C原典】Check_SP(Fyss1b.c)。</summary>
    private static bool CheckSp(string dainyuu)
    {
        var pc = new ByteCursor(ToShiftJis(dainyuu));
        pc.SetCur((byte)' ');

        ValueSymbol f = FindDelimetor(pc, out string numeric);
        if (f != ValueSymbol.Aster || !CheckNumericC(numeric))
        {
            return false;
        }

        f = FindDelimetor(pc, out numeric);
        if (f != ValueSymbol.Aster || !CheckNumericC(numeric))
        {
            return false;
        }

        f = FindDelimetor(pc, out numeric);
        if (f != ValueSymbol.End)
        {
            return false;
        }

        return CheckNumericC(numeric);
    }

    /// <summary>
    /// 負荷容量検証(記号+数値+単位 VA/W/KW/KVA)。【C原典】Check_LW(Fyss1b.c)。
    /// Keisan_LW(容量計算・正規化)は未移植のため形式検証のみ。
    /// </summary>
    private static bool CheckLw(ref string dainyuu)
    {
        // 【C原典】改訂<45>: 前後に括弧がある場合は無視して判定する。
        if (dainyuu.Length >= 2 && dainyuu[0] == '(' && dainyuu[^1] == ')')
        {
            dainyuu = dainyuu.Substring(1, dainyuu.Length - 2);
        }

        var pc = new ByteCursor(ToShiftJis(dainyuu));
        pc.SetCur((byte)' ');

        ValueSymbol findkigou = NextKigou(pc);
        ValueSymbol findparm = FindUnit(pc, out string parametor);

        bool goPoint = false;
        bool goEnd = false;

        switch (findparm)
        {
            case ValueSymbol.VA:
                if (findkigou == ValueSymbol.M)
                {
                    return false;
                }

                if (!CheckNumericC(parametor))
                {
                    goPoint = true;
                }
                else
                {
                    goEnd = true;
                }

                break;

            case ValueSymbol.W:
                if (IsWattKigouRejected(findkigou))
                {
                    return false;
                }

                if (!CheckNumericC(parametor))
                {
                    return false;
                }

                goEnd = true;
                break;

            case ValueSymbol.KW:
                if (IsWattKigouRejected(findkigou))
                {
                    return false;
                }

                if (!CheckNumericC(parametor))
                {
                    goPoint = true;
                }
                else
                {
                    goEnd = true;
                }

                break;

            case ValueSymbol.KVA:
                if (findkigou == ValueSymbol.M)
                {
                    return false;
                }

                if (!CheckNumericC(parametor))
                {
                    goPoint = true;
                }
                else
                {
                    goEnd = true;
                }

                break;

            default:
                return false;
        }

        ByteCursor endCursor = pc;

        if (goPoint)
        {
            // 【C原典】POINT_LW_PROC: 999.99KW 形式。
            var pc2 = new ByteCursor(ToShiftJis(parametor));
            pc2.SetCur((byte)' ');

            ValueSymbol fp = FindDelimetor(pc2, out string numeric);
            if (!CheckNumericC(numeric) || fp != ValueSymbol.Point)
            {
                return false;
            }

            fp = FindUnit(pc2, out numeric);
            if (fp != ValueSymbol.End || !CheckNumericC(numeric))
            {
                return false;
            }

            endCursor = pc2;
            goEnd = true;
        }

        if (goEnd)
        {
            // 【C原典】END_LW_PROC: 数値の後ろが空なら Keisan_LW(保留)→形式 OK。
            ValueSymbol fp = FindDelimetor(endCursor, out string numeric);
            return fp == ValueSymbol.End && IsNullString(numeric);
        }

        return false;
    }

    /// <summary>W/KW で拒否される負荷記号か。【C原典】Check_LW の W/KW 分岐。</summary>
    private static bool IsWattKigouRejected(ValueSymbol kigou) => kigou is
        ValueSymbol.YS or ValueSymbol.HA or ValueSymbol.S or ValueSymbol.W or
        ValueSymbol.FL or ValueSymbol.NA or ValueSymbol.TR or ValueSymbol.YA;

    /// <summary>特注送り配置検証(L/C/T/O/D)。【C原典】Check_HAI(Fyss1b.c)。</summary>
    private static bool CheckHai(string dainyuu) => dainyuu is "L" or "C" or "T" or "O" or "D";

    /// <summary>
    /// 負荷名称検証(20byte 超は全角分断を防いで切り詰め、常に TRUE)。【C原典】Check_LN(Fyss1b.c)。
    /// </summary>
    private static bool CheckLn(ref string dainyuu)
    {
        byte[] b = ToShiftJis(dainyuu);
        if (b.Length > 20)
        {
            // 【C原典】先頭 20byte を全角/半角に分解し、全角が 20byte 目に跨るなら 19byte で切る。
            int i = 0;
            int consumed = 0;
            while (i < 20)
            {
                if (IsKanjiLead(b[i]))
                {
                    i += 2;
                }
                else
                {
                    i += 1;
                }

                consumed = i;
            }

            int cut = consumed != 20 ? 19 : 20;
            dainyuu = FixedFieldCodec.ShiftJis.GetString(b, 0, cut);
        }

        return true;
    }

    /// <summary>負荷電圧検証(数値+V)。【C原典】Check_LV(Fyss1b.c)。</summary>
    private static bool CheckLv(string dainyuu)
    {
        var pc = new ByteCursor(ToShiftJis(dainyuu));
        pc.SetCur((byte)' ');

        ValueSymbol f = FindUnit(pc, out string numeric);
        if (f != ValueSymbol.V || !CheckNumericC(numeric))
        {
            return false;
        }

        f = FindDelimetor(pc, out numeric);
        if (f == ValueSymbol.End)
        {
            return IsNullString(numeric);
        }

        return true;
    }

    /// <summary>有電圧電源検証(電源種テーブル照合)。【C原典】Check_UP(Fyss1b.c, dengen_syu_table)。</summary>
    private static bool CheckUp(string dainyuu)
    {
        string parametor = Blankless(dainyuu);
        foreach ((string Description, string _) in PowerKindTable)
        {
            if (Description.Length == 0)
            {
                continue;
            }

            // 【C原典】strncmp(dengen_syu_table[i], parametor, MAX(len,len))==0 → 序数完全一致。
            if (string.Equals(Description, parametor, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 回路番号検証・展開。【C原典】Check_NO(Fyss1b.c)。
    /// カンマ連結・範囲'>'展開のみ移植。ハイフン'-'連番(PropGetKnoStruct/PropDevelopKno)は非展開。
    /// </summary>
    private static bool CheckNo(string dainyuu, out string newdainyuu)
    {
        var pc = new ByteCursor(ToShiftJis(dainyuu));
        pc.SetCur((byte)' ');

        ValueSymbol oldparm = ValueSymbol.Kanma;
        ValueSymbol findparm = FindDelimetor(pc, out string parametor);
        var sb = new StringBuilder();
        long num1 = 0;

        while (findparm != ValueSymbol.End)
        {
            if (findparm == ValueSymbol.Kanma)
            {
                if (oldparm == ValueSymbol.Kanma)
                {
                    if (!CheckAlphaNumericC(parametor) && !CheckZenkaku1z(parametor))
                    {
                        newdainyuu = string.Empty;
                        return false;
                    }

                    sb.Append(',').Append(parametor);
                }
                else if (oldparm == ValueSymbol.Greater)
                {
                    if (!CheckNumericC(parametor))
                    {
                        newdainyuu = string.Empty;
                        return false;
                    }

                    long num2 = AtolC(parametor);
                    if (num1 >= num2 || !Renketu(sb, num1, num2))
                    {
                        newdainyuu = string.Empty;
                        return false;
                    }
                }
            }
            else if (findparm == ValueSymbol.Hyphen)
            {
                // 【C原典】改訂<11>/<18>: PropGetKnoStruct(連番構成解析)は未移植。
                // 繰返し形式以外と同様にそのまま格納する。
                newdainyuu = "," + dainyuu;
                return true;
            }
            else if (findparm == ValueSymbol.Greater)
            {
                if (oldparm == ValueSymbol.Kanma)
                {
                    if (!CheckNumericC(parametor))
                    {
                        newdainyuu = string.Empty;
                        return false;
                    }

                    num1 = AtolC(parametor);
                }
                else if (oldparm == ValueSymbol.Greater)
                {
                    newdainyuu = string.Empty;
                    return false;
                }
            }
            else
            {
                newdainyuu = string.Empty;
                return false;
            }

            oldparm = findparm;
            findparm = FindDelimetor(pc, out parametor);
        }

        if (oldparm == ValueSymbol.Kanma)
        {
            if (!CheckAlphaNumericC(parametor) && !CheckZenkaku1z(parametor))
            {
                newdainyuu = string.Empty;
                return false;
            }

            sb.Append(',').Append(parametor);
            newdainyuu = sb.ToString();
            return true;
        }

        if (oldparm == ValueSymbol.Hyphen)
        {
            // 【C原典】改訂<20>: (NO=-) 対応。
            if (dainyuu == "-")
            {
                newdainyuu = ",-";
                return true;
            }

            // 【C原典】PropDevelopKno(連番展開)は未移植。そのまま格納する。
            newdainyuu = "," + dainyuu;
            return true;
        }

        if (oldparm == ValueSymbol.Greater)
        {
            if (!CheckNumericC(parametor))
            {
                newdainyuu = string.Empty;
                return false;
            }

            long num2 = AtolC(parametor);
            if (num1 >= num2 || !Renketu(sb, num1, num2))
            {
                newdainyuu = string.Empty;
                return false;
            }

            newdainyuu = sb.ToString();
            return true;
        }

        newdainyuu = string.Empty;
        return false;
    }

    /// <summary>数値範囲を ',n' 連結。【C原典】renketu(Fyss1b.c)。</summary>
    private static bool Renketu(StringBuilder sb, long nums, long nume)
    {
        int num = (int)(nume - nums);
        if (num >= 99)
        {
            return false;
        }

        for (int i = 0; i <= num; i++)
        {
            sb.Append(',').Append(nums + i);
        }

        return true;
    }

    // ============================================================
    //  数値・全角判定(Fysscommon.c)
    // ============================================================

    /// <summary>数字のみ判定(前後空白許容、空は TRUE)。【C原典】CheckNumeric(Fysscommon.c)。</summary>
    private static bool CheckNumericC(string s)
    {
        byte[] b = ToShiftJis(s);
        int i = 0;
        while (i < b.Length && b[i] == (byte)' ')
        {
            i++;
        }

        while (i < b.Length && b[i] != (byte)' ' && b[i] != 0)
        {
            if (!IsAsciiDigit(b[i]))
            {
                return false;
            }

            i++;
        }

        while (i < b.Length && b[i] == (byte)' ')
        {
            i++;
        }

        return i >= b.Length || b[i] == 0;
    }

    /// <summary>英数字・半角カナ判定。【C原典】CheckAlphaNumeric(Fysscommon.c)。</summary>
    private static bool CheckAlphaNumericC(string s)
    {
        byte[] b = ToShiftJis(s);
        int i = 0;
        while (i < b.Length && b[i] == (byte)' ')
        {
            i++;
        }

        while (i < b.Length && b[i] != (byte)' ' && b[i] != 0)
        {
            byte c = b[i];
            if (!IsAsciiAlpha(c) && !IsAsciiDigit(c) && !IsKatakana(c))
            {
                return false;
            }

            i++;
        }

        while (i < b.Length && b[i] == (byte)' ')
        {
            i++;
        }

        return i >= b.Length || b[i] == 0;
    }

    /// <summary>全角1文字(2byte)・全角2文字(4byte)判定。【C原典】CheckZenkaku1z(Fysscommon.c)。</summary>
    private static bool CheckZenkaku1z(string s)
    {
        byte[] b = ToShiftJis(s);
        if (b.Length == 2)
        {
            return IsZenkaku(b[0]);
        }

        if (b.Length == 4)
        {
            return IsZenkaku(b[0]) && IsZenkaku(b[2]);
        }

        return false;
    }

    // ============================================================
    //  kikitable_add タグ適用
    // ============================================================

    /// <summary>
    /// タグ付きで機器テーブルへ値を格納。【C原典】kikitable_add(kubun, yoyakugo, …)(Fyss11.c)。
    /// 代入文由来のタグ("MK"/"CM"/"SP"/"LW"/"HAI"/"LN"/"LV0"/"UP"/"NO"/"IT"/"B"/"WHAI"/"BK"/"CNCT")のみ。
    /// </summary>
    private static void ApplyAssignmentTag(string tag, string value, EquipmentTableEntry kiki)
    {
        switch (tag)
        {
            case "MK":
                kiki.Maker = Blankless(value);
                break;

            case "CM":
                if (kiki.Comment.Length == 0)
                {
                    kiki.Comment = Truncate(value, 20); // 【C原典】strncpy(DCM, …, 21)
                }
                else if (kiki.Comment2.Length == 0)
                {
                    kiki.Comment2 = Truncate(value, 20);
                }

                break;

            case "SP":
                kiki.SpecialDimension = Blankless(value);
                break;

            case "LW":
                {
                    string ns = Blankless(value);
                    kiki.LoadCapacity = ShiftJisLength(ns) > 10 ? Truncate(ns, 10) : ns;
                }

                break;

            case "HAI":
                kiki.SendPlacement = value.Length > 0 ? value[0] : ' '; // 【C原典】HAI = yoyakugo[0]
                break;

            case "LN":
                kiki.LoadName = Truncate(value, 20); // 【C原典】strncpy(DLN, …, 21)
                break;

            case "LV0":
                kiki.LoadVoltage[0] = Blankless(value);
                break;

            case "UP":
                kiki.PowerVoltage = Blankless(value);
                break;

            case "NO":
                if (IsNullString(kiki.CircuitNumberText))
                {
                    // 【C原典】改訂<19>: (NO=) は全角空白表示。
                    kiki.CircuitNumberText = value.Length > 0 ? Blankless(value) : "　";
                }
                else
                {
                    kiki.GroupCircuitNumberText = Blankless(value);
                }

                break;

            case "IT":
                kiki.ItemName = Truncate(value, 25); // 【C原典】strncpy(DIT, …, 25)
                break;

            case "B":
                kiki.BranchArrangement = value.Length > 0 ? value[0] : ' ';
                break;

            case "WHAI":
                kiki.WhPlacement = value.Length > 0 ? value[0] : ' ';
                break;

            case "BK":
                kiki.Remark = Truncate(value, 33); // 【C原典】memcpy(BIKO, …, 34)
                break;

            case "CNCT":
                if (value == "POW")
                {
                    kiki.BottomOutgoing = 'P'; // 【C原典】太陽光結線
                }

                break;
        }
    }

    // ============================================================
    //  補助
    // ============================================================

    /// <summary>指定回数 <see cref="ByteCursor.Advance"/> する。【C原典】nextphar/nextyhar の連続コール。</summary>
    private static void AdvanceN(ByteCursor cursor, int count)
    {
        for (int i = 0; i < count; i++)
        {
            cursor.Advance();
        }
    }

    /// <summary>CP932 バイト列へ変換。</summary>
    private static byte[] ToShiftJis(string value) => FixedFieldCodec.ShiftJis.GetBytes(value ?? string.Empty);

    /// <summary>先頭1バイトを除いた CP932 バイト列。【C原典】&amp;param[1]。</summary>
    private static byte[] ToShiftJisSkipFirst(string value)
    {
        byte[] b = ToShiftJis(value);
        return b.Length <= 1 ? Array.Empty<byte>() : b[1..];
    }

    /// <summary>CP932 バイト列から文字列へ復元。</summary>
    private static string DecodeBytes(List<byte> bytes)
        => bytes.Count == 0 ? string.Empty : FixedFieldCodec.ShiftJis.GetString(bytes.ToArray());

    /// <summary>CP932 バイト長。【C原典】strlen。</summary>
    private static int ShiftJisLength(string value) => ToShiftJis(value).Length;

    /// <summary>先頭数値部を long 変換。【C原典】atol(CheckNumeric 済み前提)。</summary>
    private static long AtolC(string value) => long.TryParse(value.Trim(), out long v) ? v : 0;

    /// <summary>全角境界を保って指定 byte 数以内に切り詰め。【C原典】strncpy(全角分断回避)。</summary>
    private static string Truncate(string value, int maxBytes)
    {
        byte[] b = ToShiftJis(value);
        if (b.Length <= maxBytes)
        {
            return value;
        }

        int i = 0;
        int cut = 0;
        while (i < maxBytes)
        {
            if (IsKanjiLead(b[i]))
            {
                if (i + 2 > maxBytes)
                {
                    break;
                }

                i += 2;
            }
            else
            {
                i += 1;
            }

            cut = i;
        }

        return FixedFieldCodec.ShiftJis.GetString(b, 0, cut);
    }

    /// <summary>Shift-JIS 全角第1バイト判定。【C原典】iskanji(Fysscommon.c)。</summary>
    private static bool IsKanjiLead(byte c) => (c >= 0x81 && c <= 0x9F) || (c >= 0xE0 && c <= 0xFC);

    /// <summary>半角カナ判定(0xB0='ｰ'を除く)。【C原典】iskatakana(Fysscommon.c)。</summary>
    private static bool IsKatakana(byte c) => c != 0xB0 && c >= 0xA6 && c <= 0xDD;

    /// <summary>全角判定。【C原典】iszenkaku(Fysscommon.c)。</summary>
    private static bool IsZenkaku(byte c) => IsKanjiLead(c) || IsKatakana(c);

    /// <summary>半角英字判定。【C原典】isalpha。</summary>
    private static bool IsAsciiAlpha(byte c) => (c >= (byte)'A' && c <= (byte)'Z') || (c >= (byte)'a' && c <= (byte)'z');

    /// <summary>半角数字判定。【C原典】isdigit。</summary>
    private static bool IsAsciiDigit(byte c) => c >= (byte)'0' && c <= (byte)'9';
}
