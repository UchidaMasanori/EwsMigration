using System.Text;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Ews.Domain.Common;
using Ews.Domain.Projects;

namespace Ews.Analysis;

/// <summary>
/// 系統文字列チェック(回路記述→系統/行種/仕様/機器テーブル化)。
///
/// 【C原典】toku/sekkei/src/Fyss11.c
///   - 入口  : Fyss11_Mojiretu_Check()  … 回路設計エリア(FYDF805)を行単位に走査し、
///             継続行(行種ブランク)を直前行へ結合しながら 1記述単位ごとに解析する。
///   - 中核  : Fyss11_Check_Main()      … 行種(gyosyu)に応じて各チェック関数へ振り分ける。
///   - 出力  : 系統(KEITOU)/行種(GYOSYU)/仕様(SPEC)/機器(KIKITABLE)テーブル + エラー(FYRT805)。
///
/// 本クラスでは <b>外側ループ(行集約)と行種ディスパッチを忠実移植</b>している。
/// 各行種の詳細チェック(Check_NP/Check_BN/Check_P/Check_C/Mojiretu_Find 等)および
/// テーブル生成本体(keitou/gyosyu/spec_table_set, kikitable_set/add)は、
/// Fyss11 配下のリーフ関数として<b>段階移植</b>するためのスタブを用意している。
///
/// 代入文(Check_Dainyuu, Fyss1b.c)の移植は部分クラス
/// <see cref="CircuitStringChecker"/>(CircuitStringChecker.Assignment.cs)に分離している。
/// </summary>
public sealed partial class CircuitStringChecker
{
    /// <summary>行種(gyosyu)定数。【C原典】Fyss11.c の #define 群。</summary>
    private static class LineTypes
    {
        public const string Nameplate = "NP";   // 盤タイトル銘板文
        public const string BoardName = "BN";   // 盤名称文
        public const string Incoming = "P";     // 入線文
        public const string IncomingBranch = "PS"; // 入線分岐文
        public const string Powered = "UP";     // 有電源文
        public const string Control = "C";      // コントロール文
        public const string Tm = "TM";          // 予約語(変圧器主)
        public const string M = "M";            // 予約語(主)
        public const string Sm = "SM";          // 予約語(変圧器従)
        public const string B = "B";            // 予約語
        public const string Bo = "BO";          // 予約語
        public const string O = "O";            // 予約語
        public const string Pm = "PM";          // 予約語
        public const string Mp = "MP";          // 予約語
        public const string S = "S";            // 予約語
        public const string Sp = "SP";          // 予約語
        public const string Separator = "SEP";  // セパレーター文
        public const string Percent = "%";      // ％文
        public const string Hash = "#";         // ＃文
        public const string Comment = "CM";     // ＣＭ文
        public const string End = "END";        // 終了
    }

    /// <summary>
    /// 系統種別判定テーブル。【C原典】Fyss11.c の kei_chk_tbl[]/syu_tbl[]。
    ///   kei_chk_tbl = {"P","SP","MP","UP"}  … 系統を起こす行種(入線/SP系/MP系/有電源)
    ///   syu_tbl     = {"1","2","3","4","5"} … 対応する系統種別文字。
    /// 行種が完全一致(長さも一致)したときのみ新しい系統(KEITOU)を生成し、
    /// syu をその種別文字に更新する。一致しない行種は直前の系統に属する。
    /// 既定値 '5'(= syu_tbl[4], どの行種にも一致しないときの静的初期値)。
    /// </summary>
    private static readonly (string LineType, char Kind)[] SystemKindTable =
    [
        (LineTypes.Incoming, '1'), // "P"  → syu_tbl[0]
        (LineTypes.Sp,       '2'), // "SP" → syu_tbl[1]
        (LineTypes.Mp,       '3'), // "MP" → syu_tbl[2]
        (LineTypes.Powered,  '4'), // "UP" → syu_tbl[3]
    ];

    /// <summary>
    /// 電源記述文と定格値コードの対応表。【C原典】dengen_kijyutu_table[]/cp_kijyutu_table[]。
    /// 入線(P)・入線分岐(PS)文の先頭で電源記述("1P2W105V" 等)を照合し、
    /// 対応する定格値コード("12A  105" 等)を f811 前半(P_F:p/w/fv/v)へ複写する。
    /// コード先頭桁が相数(KAIROSOU)、6桁目が電圧3桁目(KAIRODEN)。
    /// </summary>
    private static readonly (string Description, string Code)[] PowerDescriptionTable =
    [
        ("1P2W105V",          "12A  105"),
        ("1P2W210V",          "12A  210"),
        ("1P3W210/105V",      "13AA 210105"),
        ("3P3W105V",          "33A  105"),
        ("3P3W210V",          "33A  210"),
        ("3P3W400V",          "33A  400"),
        ("3P3W410V",          "33A  410"),
        ("3P3W420V",          "33A  420"),
        ("3P3W440V",          "33A  440"),
        ("3P4W173/100V",      "34AA 173100"),
        ("3P4W380/220V",      "34AA 380220"),
        ("3P4W415/240V",      "34AA 415240"),
        ("3P4W210V-210/105V", "34AAA210210105"),
        ("DC24V",             "  D  024"),
        ("DC48V",             "  D  048"),
        ("DC100V",            "  D  100"),
        ("3P3W380V",          "33A  380"),
    ];

    /// <summary>
    /// 有電源(UP)文の電源種別と定格値コードの対応表。【C原典】dengen_syu_table[]/cp_dengen_table[]。
    /// </summary>
    private static readonly (string Description, string Code)[] PowerKindTable =
    [
        ("AC24V",  "A24"),
        ("AC100V", "A100"),
        ("AC200V", "A200"),
        ("DC6V",   "D6"),
        ("DC12V",  "D12"),
        ("DC24V",  "D24"),
        ("DC48V",  "D48"),
        ("DC100V", "D100"),
    ];

    /// <summary>電線サイズ表。【C原典】dinf_size_table[](末尾 "SQ" を除いた 2 文字前まで採用)。</summary>
    private static readonly string[] WireSizeTable =
    [
        "2SQ", "2.0SQ", "3.5SQ", "5.5SQ", "8SQ", "8.0SQ", "14SQ", "22SQ",
        "38SQ", "60SQ", "100SQ", "150SQ", "200SQ", "250SQ", "325SQ",
    ];

    /// <summary>電線芯数表。【C原典】dinf_sins_table[](末尾 "C" を除く)。</summary>
    private static readonly string[] WireCoreTable = ["1C", "2C", "3C", "4C"];

    /// <summary>電線本数表。【C原典】dinf_hons_table[]。</summary>
    private static readonly string[] WireCountTable = ["1", "2"];

    /// <summary>
    /// 予約語マスタ(正規予約語一覧)。【C原典】fyak_tbl[](toku/include/sekkei/fyrt810.h)。
    /// 予約語文の英字部を本表へ照合し、機器を確定する(Yoyaku_Check_Main)。
    /// 各エントリの型式展開情報(ft_* / タイプフラグ)は Fyss1d の段階移植で反映する。
    /// </summary>
    private static readonly string[] ReservedWordMaster =
    [
        "MCB", "ELB", "MMCB", "ELMB", "SB", "RMCB", "RELB", "RMMCB", "RELMB",
        "MC", "THR", "MG", "SC", "NT", "WH", "VM", "AM", "VT", "CT", "VS", "AS",
        "TB", "CON", "TR", "ZCT", "LGR", "ELR", "HPSB", "HSB", "RRY", "RTR", "MCDT",
        "F", "LA", "DCPW", "CR", "TM", "TS", "G", "G1", "G2", "G3", "G4",
        "GI", "GP", "GPN", "GL", "RL", "OL", "BL", "WL", "COS", "PBS", "SSW", "TSW",
        "BZ", "BEL", "CP", "RSW", "EE", "HM", "2ERY", "3ERY", "4ERY", "CKS", "CSDT",
        "CU", "TU", "NHMB", "APN", "SL23", "SL32", "SL42", "SL43", "LGT", "BLTR",
        "PLTR", "FL", "LSW", "DSW", "SV", "MV", "KPRY", "THSW", "L", "IDF", "HDF",
        "MDF", "TVZ", "TVB", "TVH", "TVK", "WDP", "MCFR", "MGFR", "MCSD", "MGSD",
        "MGLD", "MGCS", "INV", "FLT1", "FLT2", "FLT3", "FLT4", "FLTI", "DCSIR",
        "DCNI", "MCFRSD", "MGFRSD", "STM", "SIR", "C", "R", "D", "NICA", "RE",
        "VVVF", "SPACE", "PT", "BP", "TSU", "SSWU", "PBSU", "COSU", "2COSU", "OLU",
        "SMTKP", "SMTSS", "SMTRY", "AL",
    ];

    /// <summary>
    /// 特殊キー(接尾数字を削らずに前方一致で照合する予約語)。
    /// 【C原典】tokusyu_key[](Fyss1d.c)。
    /// </summary>
    private static readonly string[] SpecialReservedKeys =
    [
        "G1", "G2", "G3", "G4",
        "SL23", "SL32", "SL42", "SL43",
        "27A", "27B", "27C", // 【C原典】改訂<8>(2024/05/21 27対応): tokusyu_key に追加。
        "FLT1", "FLT2", "FLT3", "FLT4",
    ];

    /// <summary>
    /// 電気パラメータ(定格キー)チェッカ。【C原典】Fyss1d.c Parm_Check_Main / key_check_&lt;TYPE&gt;。
    /// 予約語文の電気パラメータ部を解析し、定格値(key_tbl)を機器テーブルへ格納する際に用いる。
    /// </summary>
    private readonly ElectricalParameterChecker _electricalParameterChecker = new();

    /// <summary>
    /// 系統文字列チェックを実行する。
    /// 【C原典】Fyss11_Mojiretu_Check()。
    /// </summary>
    /// <param name="project">物件明細エリア(共通)。【C原典】bukken1。</param>
    /// <param name="projectDetail">物件明細エリア(明細)。【C原典】bukken2。</param>
    /// <param name="lines">回路設計エリア(正規化済)。【C原典】imagea/imagec。</param>
    public CircuitParseResult Check(
        ProjectInfo project,
        ProjectInfo projectDetail,
        IReadOnlyList<CircuitDescriptionLine> lines)
    {
        var result = new CircuitParseResult();

        // 集約中の1記述単位(直前行)。【C原典】ogyono/ogyosyu/okairoar。
        string outLineNumber = string.Empty;
        string outLineType = string.Empty;
        string outCircuitText = string.Empty;

        int j = 0;                 // 確定済み記述単位の数。【C原典】j。
        bool commentFlag = false;  // ダッシュコメント継続中。【C原典】comentflg。

        foreach (CircuitDescriptionLine line in lines)
        {
            // 【C原典】cmd == 'D' は削除行のためスキップ。
            if (line.Command == 'D')
            {
                continue;
            }

            string rawLineType = line.LineType;       // gyosyu(整形前)
            string circuitText = line.CircuitText;    // kairoar

            // 行種・回路記述ともに空ならスキップ(改訂<16>)。
            if (rawLineType.Length == 0 && circuitText.Length == 0)
            {
                continue;
            }

            // コメント行(# @ \ CM %)はスキップ。【C原典】memcmp で固定5バイト比較。
            if (IsCommentLineType(rawLineType))
            {
                continue;
            }

            // 行種に "-" を含む場合はダッシュコメント開始(950131)。
            if (rawLineType.Contains('-'))
            {
                commentFlag = true;
                continue;
            }

            // ダッシュコメント中で行種が空白のみなら継続スキップ。
            if (commentFlag && rawLineType.Length == 0)
            {
                continue;
            }

            commentFlag = false;

            // 空白除去(Blankless)。【C原典】Blankless(string, tgyosyu)。
            string lineType = Blankless(rawLineType);
            string lineNumber = Blankless(line.LineNumber.ToString("D3"));
            string text = circuitText;

            // 行種先頭3文字が "END" なら終了。【C原典】strncmp(tgyosyu, END, 3)。
            if (lineType.StartsWith(LineTypes.End, StringComparison.Ordinal))
            {
                break;
            }

            if (j == 0 && IsNullString(lineType))
            {
                // 先頭行が継続行(行種なし)＝記述不正。【C原典】FY-004E。
                result.Errors.Add(new CircuitParseError("FY-004E", line.LineNumber, 0, "FYMEE80"));
                return result;
            }

            if (j != 0 && IsNullString(lineType))
            {
                // 継続行: 直前単位の回路記述へ連結。【C原典】strcat(okairoar, tkairoar)。
                outCircuitText += text;
            }
            else if (j == 0 && !IsNullString(lineType))
            {
                // 最初の記述単位を開始。
                outLineNumber = lineNumber;
                outLineType = lineType;
                outCircuitText = text;
            }
            else
            {
                // 新しい行種が来たので、直前単位を確定して解析する。
                string flushLineNumber = outLineNumber;
                string flushLineType = outLineType;
                string flushCircuitText = outCircuitText;

                // 直前単位を現在行で置き換え。
                outLineNumber = lineNumber;
                outLineType = lineType;
                outCircuitText = text;

                // 行種からアルファベットのみ抽出。【C原典】Find_Alphabetto1(string, gyosyu)。
                string cleanedLineType = ExtractAlpha(flushLineType);

                TableSet(flushLineNumber, cleanedLineType, flushLineType, flushCircuitText, result);
                CheckMain(flushLineNumber, cleanedLineType, flushCircuitText, project, projectDetail, result);
            }

            j++;
        }

        if (j == 0)
        {
            return result;
        }

        // 最後の記述単位を確定して解析。
        string lastCleaned = ExtractAlpha(outLineType);
        TableSet(outLineNumber, lastCleaned, outLineType, outCircuitText, result);
        CheckMain(outLineNumber, lastCleaned, outCircuitText, project, projectDetail, result);

        return result;
    }

    /// <summary>
    /// 系統文字列チェックメイン(行種ディスパッチ)。
    /// 【C原典】Fyss11_Check_Main()。
    /// </summary>
    private void CheckMain(
        string lineNumber,
        string lineType,
        string circuitText,
        ProjectInfo project,
        ProjectInfo projectDetail,
        CircuitParseResult result)
    {
        int gyonoi = ParseLineNumber(lineNumber);

        switch (lineType)
        {
            case LineTypes.Nameplate: // NP: 盤タイトル銘板文チェック
                CheckNameplate(gyonoi, circuitText, projectDetail, result);
                break;

            case LineTypes.BoardName: // BN: 盤名称文チェック → ban を確定
                result.CurrentBan = CheckBoardName(gyonoi, circuitText, result);
                break;

            case LineTypes.Incoming: // P: 入線文チェック
                CheckIncoming(gyonoi, circuitText, result);
                break;

            case LineTypes.IncomingBranch: // PS: 入線分岐文チェック
                CheckIncomingBranch(gyonoi, circuitText, result);
                break;

            case LineTypes.Powered: // UP: 有電源文チェック
                CheckPowered(gyonoi, circuitText, result);
                break;

            case LineTypes.Control: // C: コントロール文チェック
                CheckControl(gyonoi, circuitText, project, projectDetail, result);
                break;

            // 予約語文チェック TM,M,SM,B,BO,PM,O,S
            case LineTypes.Tm:
            case LineTypes.M:
            case LineTypes.Sm:
            case LineTypes.B:
            case LineTypes.Bo:
            case LineTypes.Pm:
            case LineTypes.O:
            case LineTypes.S:
                CheckReservedWord(gyonoi, lineType, circuitText, project, projectDetail, result);
                break;

            // 予約語文チェック SP,MP
            case LineTypes.Sp:
            case LineTypes.Mp:
                CheckSpMp(gyonoi, lineType, circuitText, result);
                break;

            case LineTypes.Separator: // SEP: セパレーター文チェック
                CheckSeparator(gyonoi, circuitText, result);
                break;

            case LineTypes.Percent: // %: 何もしない
            case LineTypes.Hash:    // #: 何もしない
            case LineTypes.Comment: // CM: 何もしない
                break;

            default:
                // 未知の行種。【C原典】Error_Proc("FY-605E", ...)。
                result.Errors.Add(new CircuitParseError("FY-605E", gyonoi, 0, "FYMEE80"));
                break;
        }
    }

    /// <summary>
    /// ワークテーブル作成(系統/行種/仕様)。
    /// 【C原典】Fyss11_Table_Set() → keitou/gyosyu/spec_table_set。
    /// 系統(KEITOU)は行種が kei_chk_tbl(P/SP/MP/UP)に完全一致したときのみ生成し、
    /// syu を更新する。行種(GYOSYU)・仕様(SPEC)は現在の系統番号を参照して常に生成する。
    /// </summary>
    private void TableSet(
        string lineNumber,
        string cleanedLineType,
        string rawLineType,
        string circuitText,
        CircuitParseResult result)
    {
        // 系統テーブルセット。【C原典】kei_chk_tbl を完全一致(長さも一致)で検索し、
        // 一致したときのみ keitou_table_set を呼ぶ。
        foreach ((string type, char kind) in SystemKindTable)
        {
            if (cleanedLineType.Length == type.Length && cleanedLineType == type)
            {
                // 【C原典】syu = syu_tbl[i][0]; keitou_table_set(...)
                result.CurrentSystemKind = kind;
                result.Systems.Add(new SystemTableEntry
                {
                    SystemNumber = (short)(result.Systems.Count + 1), // 【C原典】K_No = ++i_Keitouc
                    SystemKind = kind,                                // 【C原典】Kind = syu
                    LineType = cleanedLineType,                       // 【C原典】gyosyu
                });
                result.LineTypeSequence = 0; // 【C原典】i_Gyo_Ren = 0(新系統で連番クリア)
                break;
            }
        }

        // 現在の系統番号。【C原典】i_Keitouc(直近に生成された系統。未生成なら 0)。
        short currentSystemNumber = (short)result.Systems.Count;

        // 行種テーブルセット。【C原典】gyosyu_table_set。
        result.LineTypeSequence++;                              // 【C原典】i_Gyo_Ren++
        short lineTypeCount = (short)(result.LineTypes.Count + 1); // 【C原典】++i_Gyosyuc

        // 行種区分と回路番号を採番する。【C原典】bunrui=Kairo_Bunrui_Set(); bangou=Kairo_Bangou_Set()。
        char classification = ClassifyCircuit(cleanedLineType);                 // 【C原典】G_kind
        int circuitNumber = AssignCircuitNumber(cleanedLineType, classification, result);

        // 相と電圧(3桁目)。【C原典】souden[0]=KAIROSOU; if('3') souden[1]=KAIRODEN。
        var phaseVoltage = new StringBuilder();
        phaseVoltage.Append(result.CircuitPhase);
        if (result.CircuitPhase == '3')
        {
            phaseVoltage.Append(result.CircuitVoltageDigit);
        }

        result.LineTypes.Add(new LineTypeTableEntry
        {
            SystemNumber = currentSystemNumber,          // 【C原典】K_No = i_Keitouc
            GroupNumber = (short)(10 * lineTypeCount),   // 【C原典】G_No = 10 * i_Gyosyuc
            DescriptionRow = lineNumber,                 // 【C原典】K_Gyo
            DescriptionColumn = "0",                     // 【C原典】K_Ket = "0"
            LineTypeRaw = rawLineType,                   // 【C原典】Gyosyu
            LineType = cleanedLineType,                  // 【C原典】gyosyu
            Sequence = result.LineTypeSequence,          // 【C原典】G_ren
            CircuitClass = classification,               // 【C原典】G_kind = bunrui
            CircuitNumber = $"{circuitNumber:D3}",       // 【C原典】sprintf(kairono, "%03d", bangou)
            DescriptionKind = result.CurrentSystemKind,  // 【C原典】K_kind = syu
            PhaseVoltage = phaseVoltage.ToString(),      // 【C原典】souden
            PhaseWires = result.CircuitPhaseWires,       // 【C原典】sousen = KAIROSOUSEN
        });

        // 仕様文字列テーブルセット。【C原典】spec_table_set(kairoar は空でも生成する)。
        short specNumber = (short)(result.Specs.Count + 1); // 【C原典】++i_Specc
        result.Specs.Add(new SpecTableEntry
        {
            SystemNumber = currentSystemNumber,          // 【C原典】K_No = i_Keitouc
            GroupNumber = (short)(10 * lineTypeCount),   // 【C原典】G_No = 10 * i_Gyosyuc
            SpecNumber = specNumber,                     // 【C原典】S_No = i_Specc
            DescriptionRow = lineNumber,                 // 【C原典】K_Gyo
            DescriptionColumn = "1",                     // 【C原典】K_Ket = "1"
            Length = (short)circuitText.Length,          // 【C原典】Len = strlen(kairoar)
            Prefix = '\0',                               // 【C原典】Pref = '\0'
            Text = circuitText,                          // 【C原典】Stg = kairoar
        });
    }

    /// <summary>
    /// 行種区分の判定。【C原典】Kairo_Bunrui_Set(P_CHAR gyosyu)。
    /// TM/M/SM→'M', S→'S', O→'O', BO→'B', B/PM→' ', その他→'P'。
    /// </summary>
    private static char ClassifyCircuit(string lineType) => lineType switch
    {
        LineTypes.Tm => 'M',
        LineTypes.M => 'M',
        LineTypes.Sm => 'M',
        LineTypes.S => 'S',
        LineTypes.O => 'O',
        LineTypes.B => ' ',
        LineTypes.Bo => 'B',
        LineTypes.Pm => ' ',
        _ => 'P',
    };

    /// <summary>
    /// 回路番号の採番。【C原典】Kairo_Bangou_Set(P_CHAR gyosyu, CHAR bunrui)。
    /// 相数(KAIROSOU=='1' か否か)と行種区分に応じて通し番号を進める。
    /// 各カウンタは初回に 0→1 へ底上げし、採番は後置インクリメント(採番値=進める前の値)。
    /// PM 行(区分 ' ')は採番せず 0 を返す。
    /// </summary>
    private static int AssignCircuitNumber(string lineType, char classification, CircuitParseResult result)
    {
        CircuitNumberCounters c = result.CircuitNumbers;

        // 【C原典】各カウンタが 0 なら 1 へ底上げ(1 始まりにする)。
        if (c.MainSingle == 0) c.MainSingle++;
        if (c.BranchSingle == 0) c.BranchSingle++;
        if (c.NumberSingle == 0) c.NumberSingle++;
        if (c.OutgoingSingle == 0) c.OutgoingSingle++;
        if (c.Switch == 0) c.Switch++;
        if (c.MainThree == 0) c.MainThree++;
        if (c.BranchThree == 0) c.BranchThree++;
        if (c.OutgoingThree == 0) c.OutgoingThree++;
        if (c.NumberThree == 0) c.NumberThree++;

        if (result.CircuitPhase == '1')
        {
            // 単相系(KAIROSOU == '1')。
            return classification switch
            {
                'M' => c.MainSingle++,
                'B' => c.BranchSingle++,
                'O' => c.OutgoingSingle++,
                'S' => c.Switch++,
                ' ' => lineType != LineTypes.Pm ? c.NumberSingle++ : 0, // PM は採番しない
                _ => 0,
            };
        }

        // 三相系(KAIROSOU != '1')。
        return classification switch
        {
            'M' => c.MainThree++,
            'B' => c.BranchThree++,
            'O' => c.OutgoingThree++,
            'S' => c.Switch++,
            ' ' => lineType != LineTypes.Pm ? c.NumberThree++ : 0, // PM は採番しない
            _ => 0,
        };
    }

    // ====================================================================
    //  以下、行種別の詳細チェック(リーフ関数)。
    //  Fyss11.c の各 Fyss11_Check_* / Fyss11_Mojiretu_Find を段階移植する。
    //  現状は機器テーブルの最小生成のみ行い、定格値編集等は未移植(TODO)。
    // ====================================================================

    /// <summary>盤タイトル銘板文チェック。【C原典】Fyss11_Check_NP。</summary>
    private void CheckNameplate(int lineNumber, string circuitText, ProjectInfo projectDetail, CircuitParseResult result)
    {
        // TODO: 銘板文の妥当性検証・bukken2->com.mei.bantmhnm 設定を移植する。
    }

    /// <summary>
    /// 盤名称文チェック。【C原典】Fyss11_Check_BN(戻り値=ban)。
    /// 盤タイトル銘板名称(BUN/HIK/KAI/SYU/SEI/KEI/BOX/NAI)を取り出し BanKind を確定する。
    /// 名称の後に余分なデータがあれば FY-611E、名称無しなら分岐盤(ban_BUN)、
    /// 不正データなら FY-620E。
    /// </summary>
    private BanKind CheckBoardName(int lineNumber, string circuitText, CircuitParseResult result)
    {
        var scanner = new KairoScanner();
        scanner.Start(circuitText); // 【C原典】kairostart(kairoar)

        // 盤タイトル銘板名称を取り出す。【C原典】findban = Find_BN(&colm)
        BanKind findban = FindBoardWord(scanner, out int column);
        if (findban is BanKind.Branch or BanKind.Incoming or BanKind.Switch or BanKind.Main
            or BanKind.Control or BanKind.Meter or BanKind.Box or BanKind.Internal)
        {
            // 盤名称の後は終端でなければならない。【C原典】findend = Find_BN(&colm)
            BanKind findend = FindBoardWord(scanner, out int column2);
            if (findend == BanKind.End)
            {
                return findban;
            }

            // 盤名称の後に不正データが有る。【C原典】Error_Proc("FY-611E", gyono, colm+1, ...)
            result.Errors.Add(new CircuitParseError("FY-611E", lineNumber, column2 + 1, "FYMEE80"));
            return BanKind.Error;
        }

        // 盤名称無し → 分岐盤。【C原典】return(ban_BUN)
        if (findban == BanKind.End)
        {
            return BanKind.Branch;
        }

        // 不正データが有る。【C原典】Error_Proc("FY-620E", gyono, colm+1, ...)
        result.Errors.Add(new CircuitParseError("FY-620E", lineNumber, column + 1, "FYMEE80"));
        return BanKind.Error;
    }

    /// <summary>
    /// 盤ワードの取り込み。【C原典】Find_BN(SHORT *keta)。
    /// 先頭の空白を読み飛ばし、現在位置の盤名称キーワードに区分(BanKind)を与える。
    /// 一致しなければ ban_END(終端)または ban_ERR(不正)。
    /// </summary>
    private static BanKind FindBoardWord(KairoScanner scanner, out int column)
    {
        // 盤タイトル銘板名称に区分を与える。【C原典】while( kh == ' ' ) nextkhar();
        while (scanner.Kh == ' ')
        {
            scanner.NextKhar();
        }

        column = scanner.Column; // 【C原典】(*keta) = kairostring - startkairo + 1

        // 【C原典】memcmp(kairostring-1, "…", n) の順序を厳守(HIKI/HIKK を HIK より先に)。
        if (scanner.MatchesAt("BUN")) { scanner.Advance(3); return BanKind.Branch; }    // ban_BUN
        if (scanner.MatchesAt("HIKI")) { scanner.Advance(4); return BanKind.Incoming; } // ban_HIK
        if (scanner.MatchesAt("HIKK")) { scanner.Advance(4); return BanKind.Incoming; } // ban_HIK(1995.08.02 add)
        if (scanner.MatchesAt("HIK")) { scanner.Advance(3); return BanKind.Incoming; }  // ban_HIK
        if (scanner.MatchesAt("KAI")) { scanner.Advance(3); return BanKind.Switch; }    // ban_KAI
        if (scanner.MatchesAt("SYU")) { scanner.Advance(3); return BanKind.Main; }      // ban_SYU
        if (scanner.MatchesAt("SHU")) { scanner.Advance(3); return BanKind.Main; }      // ban_SYU
        if (scanner.MatchesAt("SEI")) { scanner.Advance(3); return BanKind.Control; }   // ban_SEI
        if (scanner.MatchesAt("KEI")) { scanner.Advance(3); return BanKind.Meter; }     // ban_KEI
        if (scanner.MatchesAt("BOX")) { scanner.Advance(3); return BanKind.Box; }       // ban_BOX
        if (scanner.MatchesAt("NAI")) { scanner.Advance(3); return BanKind.Internal; }  // ban_NAI
        if (scanner.Kh == '\0') { return BanKind.End; }                                  // ban_END

        return BanKind.Error; // ban_ERR
    }

    /// <summary>
    /// 入線文チェック。【C原典】Fyss11_Check_P + kikitable_set/add。
    /// 電源記述("1P2W105V"等)を照合して系統の相数(KAIROSOU)・電圧(KAIRODEN)・相線数(KAIROSOUSEN)を確定し、
    /// 電線種類・サイズ・芯数・本数・アース線サイズ・コマンド(CM=)を解析して機器テーブルへ登録する。
    /// 定格値は f811(P_F) 相当の各要素を機器属性("f811.*")として保持する。
    /// </summary>
    private void CheckIncoming(int lineNumber, string circuitText, CircuitParseResult result)
    {
        // 【C原典】P 行で盤区分が特定盤でなければ分岐盤(ban_BUN)にする(Check_Main 内)。
        if (result.CurrentBan is not (BanKind.Incoming or BanKind.Switch or BanKind.Main
            or BanKind.Control or BanKind.Meter or BanKind.Box or BanKind.Internal))
        {
            result.CurrentBan = BanKind.Branch;
        }

        var cursor = new TextCursor(circuitText); // 【C原典】kairostart(kairoar)
        var rating = new Dictionary<string, string>(StringComparer.Ordinal); // 【C原典】union fyrt811 f811(P_F)
        string wireKind = string.Empty;           // 【C原典】dkind

        PowerParseOutcome outcome = ParseIncomingRating(cursor, rating, out wireKind, lineNumber, result);
        if (outcome == PowerParseOutcome.Error)
        {
            return;
        }

        string command = string.Empty; // 【C原典】cmdat
        if (outcome == PowerParseOutcome.Command && !ProcessCommand(cursor, lineNumber, out command, result))
        {
            return;
        }

        // 機器テーブルを作成/追加。【C原典】kikitable_set(1,...,"P",kairoar,...) + kikitable_add。
        EquipmentTableEntry kiki = KikitableSet(1, 0, lineNumber, 0, LineTypes.Incoming, circuitText, result);
        kiki.Attributes["0"] = ((short)result.CurrentBan).ToString("D3"); // 【C原典】kikitable_add("0", bans)
        kiki.Attributes["11"] = "P";                                      // 【C原典】kikitable_add("11", "P", ..., &f811)
        StoreRating(kiki, rating);
        kiki.Attributes["CM"] = command;                                  // 【C原典】kikitable_add("CM", cmdat)
        kiki.Attributes["LN"] = wireKind;                                 // 【C原典】kikitable_add("LN", dkind)
    }

    /// <summary>
    /// 入線文の電源記述・電線情報を解析し f811(P_F) 相当を組み立てる。
    /// 【C原典】Fyss11_Check_P 本体。戻り値で終了種別(終端/コマンド処理/エラー)を返す。
    /// </summary>
    private PowerParseOutcome ParseIncomingRating(
        TextCursor cursor, IDictionary<string, string> rating, out string wireKind,
        int lineNumber, CircuitParseResult result)
    {
        wireKind = string.Empty;

        cursor.SkipSpaces();
        if (cursor.AtEnd)
        {
            // 【C原典】colm = kairostring - kairostring + 1 = 1
            result.Errors.Add(new CircuitParseError("FY-623E", lineNumber, 1, "FYMEE80"));
            return PowerParseOutcome.Error;
        }

        // 電源記述文 check。【C原典】dengen_kijyutu_table 照合。
        if (!MatchTable(cursor, PowerDescriptionTable, out int powerIndex, out string powerDesc))
        {
            result.Errors.Add(new CircuitParseError("FY-650E", lineNumber, cursor.Column, "FYMEE80"));
            return PowerParseOutcome.Error;
        }

        string powerCode = PowerDescriptionTable[powerIndex].Code;
        rating["cp"] = powerCode; // 【C原典】strncpy(&P_F811->p.p, cp_kijyutu_table[i], len)

        // 系統の相数と電圧(3桁目)/相線数をセット。【C原典】KAIROSOU/KAIRODEN/KAIROSOUSEN。
        result.CircuitPhase = powerCode.Length > 0 && powerCode[0] != ' ' ? powerCode[0] : '1';
        result.CircuitVoltageDigit = powerCode.Length > 5 ? powerCode[5] : '0';
        result.CircuitPhaseWires = powerDesc.Length >= 4 ? powerDesc[..4] : powerDesc;

        cursor.SkipSpaces();
        if (cursor.AtEnd) { return PowerParseOutcome.Finalize; }
        if (cursor.Current == '(') { return PowerParseOutcome.Command; }

        // 電線種類の抜き出し(英字, 最大10文字)。【C原典】while(isalpha) dkind[icnt++]。
        var kind = new StringBuilder();
        while (char.IsAsciiLetter(cursor.Current))
        {
            if (kind.Length < 10) { kind.Append(cursor.Current); }
            cursor.Skip();
        }
        wireKind = kind.ToString();

        // 電線情報 サイズ check。【C原典】dinf_size_table 照合(末尾 "SQ" を除去)。
        if (!MatchTable(cursor, WireSizeTable, out _, out string sizeMatched))
        {
            result.Errors.Add(new CircuitParseError("FY-651E", lineNumber, cursor.Column, "FYMEE80"));
            return PowerParseOutcome.Error;
        }
        rating["sq"] = TrimSuffix(sizeMatched, 2);

        cursor.SkipSpaces();
        if (cursor.AtEnd) { return PowerParseOutcome.Finalize; }
        if (cursor.Current == '(') { return PowerParseOutcome.Command; }

        // 電線情報 芯数 check。【C原典】'-' の後 dinf_sins_table(末尾 "C" を除去)。
        if (cursor.Current == '-')
        {
            cursor.Skip();
            if (!MatchTable(cursor, WireCoreTable, out _, out string coreMatched))
            {
                result.Errors.Add(new CircuitParseError("FY-652E", lineNumber, cursor.Column, "FYMEE80"));
                return PowerParseOutcome.Error;
            }
            rating["c"] = TrimSuffix(coreMatched, 1);
            cursor.SkipSpaces();
            if (cursor.AtEnd) { return PowerParseOutcome.Finalize; }
        }

        // 電線情報 本数 check。【C原典】'*' の後 dinf_hons_table。
        if (cursor.Current == '*')
        {
            cursor.Skip();
            if (!MatchTable(cursor, WireCountTable, out _, out string countMatched))
            {
                result.Errors.Add(new CircuitParseError("FY-653E", lineNumber, cursor.Column, "FYMEE80"));
                return PowerParseOutcome.Error;
            }
            rating["k"] = countMatched;
            cursor.SkipSpaces();
            if (cursor.AtEnd) { return PowerParseOutcome.Finalize; }
        }

        // アース部電線サイズ。【C原典】改訂<18> ",E" の後 dinf_size_table。
        if (cursor.MatchesAt(",E"))
        {
            cursor.Advance(2);
            if (!MatchTable(cursor, WireSizeTable, out _, out string earthMatched))
            {
                result.Errors.Add(new CircuitParseError("FY-651E", lineNumber, cursor.Column, "FYMEE80"));
                return PowerParseOutcome.Error;
            }
            rating["esq"] = TrimSuffix(earthMatched, 2);
            cursor.SkipSpaces();
            if (cursor.AtEnd) { return PowerParseOutcome.Finalize; }
            if (cursor.Current == '(') { return PowerParseOutcome.Command; }
        }

        if (cursor.Current == '(') { return PowerParseOutcome.Command; }

        // 【C原典】'(' 以外は FY-625E。
        result.Errors.Add(new CircuitParseError("FY-625E", lineNumber, cursor.Column, "FYMEE80"));
        return PowerParseOutcome.Error;
    }

    /// <summary>
    /// 入線分岐文チェック。【C原典】Fyss11_Check_PS + kikitable_set/add。
    /// 電源記述を照合し(相数等は更新しない)、コマンド(CM=)を解析して機器テーブルへ登録する。
    /// </summary>
    private void CheckIncomingBranch(int lineNumber, string circuitText, CircuitParseResult result)
    {
        var cursor = new TextCursor(circuitText); // 【C原典】kairostart(kairoar)
        var rating = new Dictionary<string, string>(StringComparer.Ordinal);

        cursor.SkipSpaces();
        if (cursor.AtEnd)
        {
            result.Errors.Add(new CircuitParseError("FY-623E", lineNumber, 1, "FYMEE80"));
            return;
        }

        // 電源記述文 check。【C原典】dengen_kijyutu_table 照合(KAIROSOU 等は更新しない)。
        if (!MatchTable(cursor, PowerDescriptionTable, out int powerIndex, out _))
        {
            result.Errors.Add(new CircuitParseError("FY-650E", lineNumber, cursor.Column, "FYMEE80"));
            return;
        }
        rating["cp"] = PowerDescriptionTable[powerIndex].Code; // 【C原典】strncpy(&P_F811->ps.p, ...)

        cursor.SkipSpaces();
        string command = string.Empty;
        if (!cursor.AtEnd)
        {
            if (cursor.Current != '(')
            {
                // 【C原典】else FY-654E
                result.Errors.Add(new CircuitParseError("FY-654E", lineNumber, cursor.Column, "FYMEE80"));
                return;
            }
            if (!ProcessCommand(cursor, lineNumber, out command, result))
            {
                return;
            }
        }

        EquipmentTableEntry kiki = KikitableSet(1, 0, lineNumber, 0, LineTypes.IncomingBranch, circuitText, result);
        kiki.Attributes["0"] = ((short)result.CurrentBan).ToString("D3"); // 【C原典】kikitable_add("0", bans)
        kiki.Attributes["11"] = "PS";                                     // 【C原典】kikitable_add("11", "PS", ..., &f811)
        StoreRating(kiki, rating);
        kiki.Attributes["CM"] = command;                                  // 【C原典】kikitable_add("CM", cmdat)
    }

    /// <summary>
    /// 有電源文チェック。【C原典】Fyss11_Check_UP + kikitable_set/add。
    /// 電源種別("AC100V"等)を照合し、コマンド(CM=)を解析して機器テーブルへ登録する。
    /// </summary>
    private void CheckPowered(int lineNumber, string circuitText, CircuitParseResult result)
    {
        var cursor = new TextCursor(circuitText); // 【C原典】kairostring = kairoar
        var rating = new Dictionary<string, string>(StringComparer.Ordinal);

        cursor.SkipSpaces();
        if (cursor.AtEnd)
        {
            result.Errors.Add(new CircuitParseError("FY-623E", lineNumber, 1, "FYMEE80"));
            return;
        }

        // 電源記述文 check。【C原典】dengen_syu_table 照合。
        if (!MatchTable(cursor, PowerKindTable, out int kindIndex, out _))
        {
            // 【C原典】FY-656E
            result.Errors.Add(new CircuitParseError("FY-656E", lineNumber, cursor.Column, "FYMEE80"));
            return;
        }
        rating["fv"] = PowerKindTable[kindIndex].Code; // 【C原典】strncpy(&P_F811->up.fv, cp_dengen_table[i], len)

        cursor.SkipSpaces();
        string command = string.Empty;
        if (!cursor.AtEnd)
        {
            if (cursor.Current != '(')
            {
                result.Errors.Add(new CircuitParseError("FY-654E", lineNumber, cursor.Column, "FYMEE80"));
                return;
            }
            if (!ProcessCommand(cursor, lineNumber, out command, result))
            {
                return;
            }
        }

        EquipmentTableEntry kiki = KikitableSet(1, 0, lineNumber, 0, LineTypes.Powered, circuitText, result);
        kiki.Attributes["0"] = ((short)result.CurrentBan).ToString("D3"); // 【C原典】kikitable_add("0", bans)
        kiki.Attributes["11"] = "UP";                                     // 【C原典】kikitable_add("11", "UP", ..., &f811)
        StoreRating(kiki, rating);
        kiki.Attributes["CM"] = command;                                  // 【C原典】kikitable_add("CM", cmdat)
    }

    /// <summary>
    /// 括弧付きコマンド(CM=)の解析。【C原典】Fyss11_Check_P/PS/UP の KAKKO_PROC。
    /// "( CM= &lt;文字列&gt; )" を解析し、閉じ括弧の後に余分なデータがあれば FY-613E。
    /// </summary>
    private bool ProcessCommand(TextCursor cursor, int lineNumber, out string command, CircuitParseResult result)
    {
        command = string.Empty;
        cursor.Skip(); // 【C原典】'(' を消費(kairostring++)

        int mark = cursor.Column; // 【C原典】skairo
        cursor.SkipSpaces();
        if (cursor.AtEnd)
        {
            result.Errors.Add(new CircuitParseError("FY-654E", lineNumber, mark, "FYMEE80"));
            return false;
        }

        mark = cursor.Column;
        if (cursor.MatchesAt("CM="))
        {
            cursor.Advance(3);
        }
        else
        {
            result.Errors.Add(new CircuitParseError("FY-655E", lineNumber, mark, "FYMEE80"));
            return false;
        }

        mark = cursor.Column;
        var buffer = new StringBuilder();
        while (cursor.Current != ')' && cursor.Current != '(' && !cursor.AtEnd)
        {
            char ch = cursor.Current;
            if (ch != ')' && ch != '(' && ch != ' ')
            {
                if (buffer.Length < 20) { buffer.Append(ch); } // 【C原典】icom < 20
            }
            cursor.Skip();
        }

        if (cursor.AtEnd)
        {
            result.Errors.Add(new CircuitParseError("FY-612E", lineNumber, mark, "FYMEE80"));
            return false;
        }
        if (cursor.Current == '(')
        {
            result.Errors.Add(new CircuitParseError("FY-614E", lineNumber, cursor.Column, "FYMEE80"));
            return false;
        }

        // 【C原典】do{ kairostring++; }while(' '); 閉じ括弧を消費し後続空白を読み飛ばす。
        cursor.Skip();
        cursor.SkipSpaces();

        command = buffer.ToString();
        if (cursor.AtEnd)
        {
            return true;
        }

        // 閉じ括弧の後に余分なデータ。【C原典】FY-613E
        result.Errors.Add(new CircuitParseError("FY-613E", lineNumber, cursor.Column, "FYMEE80"));
        return false;
    }

    /// <summary>解析した定格値(f811)要素を機器属性("f811.*")として保持する。</summary>
    private static void StoreRating(EquipmentTableEntry kiki, IDictionary<string, string> rating)
    {
        foreach (KeyValuePair<string, string> item in rating)
        {
            kiki.Attributes["f811." + item.Key] = item.Value;
        }
    }

    /// <summary>電源記述文/電源種別の照合結果種別。</summary>
    private enum PowerParseOutcome
    {
        Finalize, // 終端に到達(コマンド無し)
        Command,  // '(' に到達(CM= 処理へ)
        Error,    // エラー記録済
    }

    /// <summary>
    /// 文字列テーブルとカーソル現在位置を前方一致で照合し、一致したらその分だけ進める。
    /// 【C原典】for(i){ n=strlen(table[i]); if(strncmp(table[i],kairostring,n)==0){ kairostring+=n; } }。
    /// </summary>
    private static bool MatchTable(TextCursor cursor, string[] table, out int index, out string matched)
    {
        for (int i = 0; i < table.Length; i++)
        {
            string entry = table[i];
            if (entry.Length == 0) { continue; }
            if (cursor.MatchesAt(entry))
            {
                cursor.Advance(entry.Length);
                index = i;
                matched = entry;
                return true;
            }
        }

        index = -1;
        matched = string.Empty;
        return false;
    }

    /// <summary>電源記述表(記述→コード)の照合。記述側で前方一致する。</summary>
    private static bool MatchTable(
        TextCursor cursor, (string Description, string Code)[] table, out int index, out string matched)
    {
        for (int i = 0; i < table.Length; i++)
        {
            string entry = table[i].Description;
            if (entry.Length == 0) { continue; }
            if (cursor.MatchesAt(entry))
            {
                cursor.Advance(entry.Length);
                index = i;
                matched = entry;
                return true;
            }
        }

        index = -1;
        matched = string.Empty;
        return false;
    }

    /// <summary>末尾 <paramref name="suffixLength"/> 文字を除いた部分文字列。【C原典】strncpy(dst, src, len-2) 等。</summary>
    private static string TrimSuffix(string value, int suffixLength)
        => value.Length > suffixLength ? value[..^suffixLength] : string.Empty;



    /// <summary>コントロール文チェック。【C原典】SPACE_CHK + Fyss11_Check_C。</summary>
    private void CheckControl(int lineNumber, string circuitText, ProjectInfo project, ProjectInfo projectDetail, CircuitParseResult result)
    {
        if (!CheckNoEmbeddedSpace(lineNumber, circuitText, result))
        {
            return;
        }

        // TODO: Fyss11_Check_C(制御仕様/制御機器テーブル生成)を移植する。
    }

    /// <summary>予約語文チェック(TM/M/SM/B/BO/PM/O/S)。【C原典】ElmVolt_CHK + SPACE_CHK + Fyss11_Mojiretu_Find。</summary>
    private void CheckReservedWord(int lineNumber, string lineType, string circuitText, ProjectInfo project, ProjectInfo projectDetail, CircuitParseResult result)
    {
        if (!CheckNoEmbeddedSpace(lineNumber, circuitText, result))
        {
            return;
        }

        // TODO: ElmVolt_CHK(素子電圧)を移植する。
        // 分岐を分解し、各サブ文を機器テーブルへ展開する。【C原典】Fyss11_Mojiretu_Find。
        MojiretuFind(lineNumber, lineType, circuitText, project, projectDetail, result);
    }

    /// <summary>
    /// 予約語文の分岐分解。【C原典】Fyss11_Mojiretu_Find()。
    /// 回路記述を分岐シンボル(sym_BUNKI '/' 系 / sym_BUNKIUKE '--')で分割し、
    /// 各サブ文(分岐番号 Bun_No 付き)を Mojiretu_Check へ渡す。
    ///  - 先頭が終端 → FY-623E
    ///  - 先頭が分岐シンボル → FY-624E
    ///  - 分岐受け('--')の後は sym_BUNKI が続く限り繰り返す
    /// </summary>
    private void MojiretuFind(int lineNumber, string lineType, string circuitText, ProjectInfo project, ProjectInfo projectDetail, CircuitParseResult result)
    {
        var scanner = new KairoScanner();
        scanner.Start(circuitText); // 【C原典】kairostart(kairoar)

        // 文の先頭のシンボルを求める。【C原典】findkairo = firstkairo()
        KairoSymbol findkairo = scanner.FirstKairo();
        switch (findkairo)
        {
            case KairoSymbol.End:
                // 【C原典】case sym_END: Error_Proc("FY-623E", gyono, 1, ...)
                result.Errors.Add(new CircuitParseError("FY-623E", lineNumber, 1, "FYMEE80"));
                return;

            case KairoSymbol.Other:
                // 文を取り出す。【C原典】findkairo = Find_Kairo(control)
                findkairo = scanner.FindKairo(out string firstControl);
                if (IsNullString(firstControl) && lineType != LineTypes.M)
                {
                    // 空文は行種 "M" のみ許容。【C原典】memcmp(gyosyu,"M",strlen(gyosyu))
                    result.Errors.Add(new CircuitParseError("FY-623E", lineNumber, 1, "FYMEE80"));
                    return;
                }

                // 文のチェックをする(分岐番号=1)。【C原典】Bun_No=1; Mojiretu_Check(...)
                MojiretuCheck(1, lineNumber, lineType, firstControl, result);

                // 終端なら完了、そうでなければ分岐処理へ。【C原典】if(sym_END)return 0; else goto BUNKI_PROC
                if (findkairo == KairoSymbol.End)
                {
                    return;
                }

                break;

            default:
                // 【C原典】default: Error_Proc("FY-624E", gyono, colm+1, ...)
                result.Errors.Add(new CircuitParseError("FY-624E", lineNumber, scanner.Column + 1, "FYMEE80"));
                return;
        }

        // 分岐処理。【C原典】BUNKI_PROC。
        switch (findkairo)
        {
            case KairoSymbol.BranchReceiver: // sym_BUNKIUKE('--')
                findkairo = scanner.FindKairo(out string receiverControl);
                if (IsNullString(receiverControl))
                {
                    result.Errors.Add(new CircuitParseError("FY-623E", lineNumber, scanner.Column + 1, "FYMEE80"));
                    return;
                }

                int bunNo = 2; // 【C原典】Bun_No = 2
                MojiretuCheck(bunNo, lineNumber, lineType, receiverControl, result);

                // 文の先頭シンボルが終了するまで処理する。【C原典】while(findkairo != sym_END)
                while (findkairo != KairoSymbol.End)
                {
                    if (findkairo != KairoSymbol.Branch)
                    {
                        result.Errors.Add(new CircuitParseError("FY-624E", lineNumber, scanner.Column + 1, "FYMEE80"));
                        return;
                    }

                    findkairo = scanner.FindKairo(out string branchControl);
                    if (IsNullString(branchControl))
                    {
                        result.Errors.Add(new CircuitParseError("FY-623E", lineNumber, scanner.Column + 1, "FYMEE80"));
                        return;
                    }

                    bunNo++; // 【C原典】Bun_No += 1
                    MojiretuCheck(bunNo, lineNumber, lineType, branchControl, result);
                }

                break;

            default:
                // sym_BUNKI / sym_OTHER / その他はすべて構文エラー。【C原典】FY-624E
                result.Errors.Add(new CircuitParseError("FY-624E", lineNumber, scanner.Column + 1, "FYMEE80"));
                return;
        }
    }

    /// <summary>
    /// 予約語文(分岐サブ文)チェック。【C原典】Mojiretu_Check()。
    /// 複合予約語文をカンマ(sym_KANMA)と括弧のバランスで分解し、
    ///  - 複合予約語(G/H/K/MH/S/'(') は括弧グループ+接尾語を抽出し接尾語を検証(FY-617E/FY-613E)、
    ///  - 単純予約語はカンマまで抽出して機器テーブル(KIKITABLE)を 1 件生成する。
    /// 生成した機器には予約語(yoyaku)と予約語番号(ysno)を分解して設定する
    /// (kikitable_add("1", ...) 相当)。
    /// 機器名称のマスタ照合(Check_KikiMeisyou→Fyss1c)・代入文(Check_Dainyuu)・
    /// 定格値編集(f811)は未移植(TODO)。
    /// </summary>
    private void MojiretuCheck(int branchNumber, int lineNumber, string lineType, string controlText, CircuitParseResult result)
    {
        var scanner = new ControlScanner();
        scanner.Start(controlText); // 【C原典】controlstart(kairoar)

        // 複合予約語文の先頭シンボルを求める。【C原典】findcontrol = firstcontrol()
        ControlSymbol findcontrol = scanner.FirstControl();
        int groupNo = 0; // 【C原典】GroupNo

        while (findcontrol != ControlSymbol.End)
        {
            if (findcontrol is ControlSymbol.H or ControlSymbol.G or ControlSymbol.K
                or ControlSymbol.MH or ControlSymbol.LeftParen or ControlSymbol.S)
            {
                // ---- 複合予約語文 ----
                if (findcontrol == ControlSymbol.LeftParen)
                {
                    groupNo++; // 【C原典】if(sym_LKAKKO) GroupNo++
                }

                // 括弧のバランス箇所まで抽出。【C原典】Select_Control(control)
                if (!scanner.SelectControl(out string groupText))
                {
                    // 括弧非平衡。【C原典】Error_Proc("FY-617E", ...)
                    result.Errors.Add(new CircuitParseError("FY-617E", lineNumber, scanner.Column + 1, "FYMEE80"));
                    return;
                }

                // 括弧の後の接尾語を抽出。【C原典】Find_Control(buff); Blankless→trailer
                scanner.FindControl(out string buff);
                string trailer = Blankless(buff);

                // 接尾語の妥当性チェック。【C原典】(LN=/(LW=/(BK=/(BKO= のみ許容 → FY-613E
                if (!IsNullString(trailer) && findcontrol != ControlSymbol.LeftParen)
                {
                    if (!trailer.StartsWith("(LN=", StringComparison.Ordinal)
                        && !trailer.StartsWith("(LW=", StringComparison.Ordinal)
                        && !trailer.StartsWith("(BK=", StringComparison.Ordinal)
                        && !trailer.StartsWith("(BKO=", StringComparison.Ordinal))
                    {
                        result.Errors.Add(new CircuitParseError("FY-613E", lineNumber, scanner.Column + 1, "FYMEE80"));
                        return;
                    }
                }

                // 次階層の複合予約語文。【C原典】Mojiretu_Check_Main(...)
                MojiretuCheckMain(branchNumber, groupNo, findcontrol, lineNumber, lineType, groupText, trailer, result);
            }
            else
            {
                // ---- 単純予約語文 ----
                // カンマまで抽出。【C原典】Find_Control(control)
                scanner.FindControl(out string control);

                // 機器テーブルを作成。【C原典】kikitable_set(bunno,0,0,0,gyono,colm+1,gyosyu,control,...)
                EquipmentTableEntry kiki = KikitableSet(
                    branchNumber, 0, lineNumber, scanner.Column + 1, lineType, control, result);

                // 予約語文をチェック(予約語・予約語番号の分解)。【C原典】Check_KikiMeisyou → kikitable_add("1", ...)
                CheckKikiMeisyou(kiki, control, lineNumber, result);
                // TODO: 機器名称のマスタ照合(Fyss1c_Mojiretu_Check)・代入文(Check_Dainyuu)・f811 を移植する。
            }

            findcontrol = scanner.FirstControl(); // 【C原典】findcontrol = firstcontrol()
        }
    }

    /// <summary>
    /// 次階層の複合予約語文チェック。【C原典】Mojiretu_Check_Main()。
    /// 現状は括弧グループを 1 件の機器として記録し、接尾語(trailer)を属性に保持するのみ。
    /// グループ内のネスト展開・制御機器生成の詳細は未移植(TODO)。
    /// </summary>
    private void MojiretuCheckMain(
        int branchNumber, int groupNo, ControlSymbol controlType,
        int lineNumber, string lineType, string groupText, string trailer, CircuitParseResult result)
    {
        EquipmentTableEntry kiki = KikitableSet(
            branchNumber, groupNo, lineNumber, 0, lineType, groupText, result);
        kiki.ReservedWord = ControlSymbolText(controlType); // G/H/K/MH/S/( の区分
        if (!IsNullString(trailer))
        {
            kiki.Attributes["trailer"] = trailer; // 【C原典】接尾語(LN=/LW=/BK=/BKO=)
        }
        // TODO: Mojiretu_Check_Main 本体(グループ内ネスト展開・制御機器)を移植する。
    }

    /// <summary>
    /// 機器テーブルエントリ生成。【C原典】kikitable_set()。
    /// K_No=系統番号, G_No=10*行種数, S_No=仕様番号, B_No=分岐番号, D_No=機器番号を採番する。
    /// </summary>
    private static EquipmentTableEntry KikitableSet(
        int branchNumber, int groupNo, int lineNumber, int column,
        string lineType, string circuitText, CircuitParseResult result)
    {
        var kiki = new EquipmentTableEntry
        {
            SystemNumber = (short)result.Systems.Count,          // 【C原典】K_No = i_Keitouc
            GroupNumber = (short)(10 * result.LineTypes.Count),  // 【C原典】G_No = 10 * i_Gyosyuc
            SpecNumber = (short)result.Specs.Count,              // 【C原典】S_No = i_Specc
            StringSequence = (short)branchNumber,                // 【C原典】B_No = bunno
            EquipmentNumber = (short)(result.MainEquipment.Count + 1), // 【C原典】D_No = ++i_Kikic
            LineNumber = (short)lineNumber,                      // 【C原典】K_Gyo
            Column = (short)column,                              // 【C原典】K_Ket
            ControlGroupNumber = (short)groupNo,                 // 【C原典】GroupNo
            LineType = lineType,                                 // 【C原典】gyosyu
            CircuitText = circuitText,                           // 【C原典】control
            Ban = result.CurrentBan,                             // 【C原典】現在の盤区分
        };
        result.MainEquipment.Add(kiki);
        return kiki;
    }

    /// <summary>
    /// 予約語・予約語番号の分解。【C原典】kikitable_add("1", yoyakugo, ...)。
    /// 特定の予約語(G1?G4/SL*/FLT*)はそのまま、それ以外は英字部を予約語、
    /// 後続数値を予約語番号(2桁)として設定する。
    /// </summary>
    private static void ApplyReservedWord(EquipmentTableEntry kiki, string reservedText)
    {
        if (reservedText.Length == 0)
        {
            return;
        }

        // 【C原典】固定予約語はそのまま yoyaku へ。
        if (StartsWithAny(reservedText, "G1", "G2", "G3", "G4")
            || StartsWithAny(reservedText, "SL23", "SL32", "SL42", "SL43")
            || StartsWithAny(reservedText, "FLT1", "FLT2", "FLT3", "FLT4"))
        {
            kiki.ReservedWord = reservedText;
            return;
        }

        // 【C原典】"PT(" 以外は Find_Bangou で予約語番号を抽出。
        if (!reservedText.StartsWith("PT(", StringComparison.Ordinal))
        {
            if (FindBangou(reservedText, out int number))
            {
                kiki.ReservedWordNumber = number.ToString("D2"); // 【C原典】sprintf("%02d")
            }
        }

        // 【C原典】Find_Alphabetto で英字部(先頭数字含む)を予約語へ。
        kiki.ReservedWord = FindAlphabetto(reservedText);
    }

    // C-origin: Check_KikiMeisyou (Fyss1b.c) -> Fyss1c_Mojiretu_Check / Check_KikimeiC / Yoyaku_Check_Main (Fyss1d.c).
    // Blankless -> empty:FY-628E; ApplyReservedWord (kikitable_add "1"); reserved-word master (fyak_tbl) match -> ProductName (s_yoyaku/kikimei); no match:FY-879E.
    private void CheckKikiMeisyou(EquipmentTableEntry kiki, string control, int lineNumber, CircuitParseResult result)
    {
        // 【C原典】Check_KikiMeisyou(Fyss1b.c): Find_KikiMeisyou で予約語部(代入文'('以前)を
        // 取り出し、電気パラメータ・予約語番号の判定は予約語部のみを対象とする。代入文
        // 「(LW=…)」等の '=' を電気パラメータの '=' と誤認しないよう本体と分離する。
        string reservedClause = ExtractReservedClause(control);
        string kikimeisyou = Blankless(reservedClause);
        if (IsNullString(kikimeisyou))
        {
            result.Errors.Add(new CircuitParseError("FY-628E", lineNumber, 1, "FYMEE80"));
            return;
        }

        ApplyReservedWord(kiki, kikimeisyou);

        string yoyaku = SplitReservedToken(kikimeisyou);
        bool resolvedOk = ResolveReservedWord(yoyaku, out string resolved);
        if (resolvedOk)
        {
            kiki.ProductName = resolved; // s_yoyaku -> kikimei
        }
        else
        {
            result.Errors.Add(new CircuitParseError("FY-879E", lineNumber, 1, "FYMEE80"));
        }

        // 【C原典】改訂<42>/<46>(Fyss1b.c): 予約語27A/27B/27CはCRとして扱う。
        // (Yoyaku_Check_Main の tokusyu_key 一致で FY-879E は回避され、ここで機器名称=CR に上書きする)
        if (control.StartsWith("27", StringComparison.Ordinal))
        {
            // 【C原典】改訂<46>: 行種B(gyosyu=="B")は入力不可。
            if (string.Equals(kiki.LineType, "B", StringComparison.Ordinal))
            {
                result.Errors.Add(new CircuitParseError("FY-760E", lineNumber, 1, "FYMEE80"));
                return;
            }

            kiki.ProductName = "CR"; // 【C原典】改訂<42>: kikimei = "CR"
        }

        // 【C原典】Check_Kikimei()(Fyss1d.c): 予約語確定後、電気パラメータ(d_parm)があれば
        // Parm_Check_Main() で定格値(key_tbl)を検証・格納する。Yoyaku_Check_Main が失敗
        // (irc!=0)した場合は Parm_Check_Main を呼ばないため、resolvedOk のときのみ実施する。
        if (resolvedOk)
        {
            PopulateRatingValues(kiki, kikimeisyou, resolved, lineNumber, result);

            // 【C原典】Check_KikiMeisyou(Fyss1b.c) の代入文 while ループ(Check_Dainyuu)。
            // 機器名称が解決できた(Fyss1c_Mojiretu_Check が ret==0)場合のみ、予約語部に続く
            // 代入文「(TAG=値)」を検証し機器テーブル(DMK/DCM/DLW/DLN/DLV/DUP/DNO/…)へ格納する。
            ProcessAssignmentStatements(kiki, control, lineNumber, result);
        }

        // TODO: Fyss1e S_key_check_main (制御機器の代入文, deferred).
        // TODO: 改訂<36>G_TYPE_ET / 改訂<48>G_TB_800A / 改訂<47>G_TYPE_6A / 改訂<39>G_YOYAKU_MGSH は
        //       電気パラメータ/タイプ設定エンジン(未移植)向けフラグのため未対応。
    }

    /// <summary>
    /// 電気パラメータ文字列(d_parm)を解析し、定格値(key_tbl)を機器テーブルへ格納する。
    /// 【C原典】Check_Kikimei()→Parm_Check_Main()(Fyss1d.c)、および結果 f811 を機器へ
    /// 反映する kikitable_add("2", electron, S_Kiki, &amp;f811)(Fyss1c.c Check_KikimeiC)の配線。
    /// </summary>
    private void PopulateRatingValues(EquipmentTableEntry kiki, string kikimeisyou, string reservedWord, int lineNumber, CircuitParseResult result)
    {
        string electron = ExtractElectricalParameter(kikimeisyou);
        if (IsNullString(electron))
        {
            return; // 【C原典】NULLSTRING(d_parm) → Parm_Check_Main を呼ばない
        }

        // 【C原典】Parm_Check_Main(d_parm, iNo, ErrNo)。iNo は Yoyaku_Check_Main が返す
        // fyak_tbl 添字(=s_yoyaku=reservedWord)。対象外予約語は irc=0(空値)で読み飛ばす。
        short irc = _electricalParameterChecker.CheckParameters(reservedWord, electron, out RatingValues values, out string errorCode);
        if (irc == -1)
        {
            result.Errors.Add(new CircuitParseError(errorCode, lineNumber, 1, "FYMEE80"));
            return;
        }

        kiki.RatingValues = values; // 【C原典】*P_F811 = key_tbl → kikitable_add("2", ..., &f811)
    }

    /// <summary>
    /// 予約語文から電気パラメータ部(d_parm/electron)を取り出す。
    /// 【C原典】Check_KikimeiC(Fyss1c.c)の Find_Delimetor/Find_Name による d_parm 抽出。
    /// '=' があればその後ろ(sym_EQUAL 分岐)、無ければ予約語(英字+番号)を除いた残り
    /// (else 分岐)を電気パラメータとする。'*'(数量区切り)以降は対象外。
    /// </summary>
    private static string ExtractElectricalParameter(string kikimeisyou)
    {
        // 【C原典】'*' は数量(kosu)区切り。電気パラメータ抽出の対象外。
        int cut = kikimeisyou.IndexOf('*');
        string head = cut >= 0 ? kikimeisyou[..cut] : kikimeisyou;

        int eq = head.IndexOf('=');
        if (eq >= 0)
        {
            // 【C原典】sym_EQUAL 分岐: '=' の後ろが電気パラメータ(electron)。
            return head[(eq + 1)..];
        }

        // 【C原典】else 分岐(Fyss1c.c Check_KikimeiC): 先頭が英字か数字かで予約語(yoyakugo)の
        // 切り出しを変える。nextname は数字を sym_DIGIT、それ以外(英字・記号)を sym_OTHER とみなし、
        // Find_Name(stop) は stop 種別が来るまで文字を収集する。
        int i = 0;
        if (i < head.Length && char.IsAsciiDigit(head[i]))
        {
            // 数字始まり(27A/2ERY 等の先頭数字予約語):
            // 【C原典】Find_Name(sym_OTHER, yoyakugo) で先頭数字部、続けて
            //          Find_Name(sym_DIGIT, yoyakunum) で続く英字部を収集し予約語に連結。
            while (i < head.Length && char.IsAsciiDigit(head[i])) i++;   // 先頭数字部
            while (i < head.Length && !char.IsAsciiDigit(head[i])) i++;  // 続く英字部
        }
        else
        {
            // 英字始まり: 最初の数字までが予約語(数字以降は electron)。
            // 【C原典】Find_Name(sym_DIGIT, yoyakugo)。
            while (i < head.Length && !char.IsAsciiDigit(head[i])) i++;
        }
        string electron = head[i..];

        // 【C原典】CheckNumeric(electron): electron が空か数値のみなら予約語番号側へ吸収し空にする
        // (CheckNumeric は空文字列/空白のみでも TRUE を返す)。
        if (electron.Length == 0 || electron.All(char.IsAsciiDigit))
        {
            electron = string.Empty;
        }

        return electron;
    }

    // C-origin: Find_Delimetor (Fyss1c.c). Reserved token = text before '=' (electrical param) or '*' (quantity).
    private static string SplitReservedToken(string kikimeisyou)
    {
        int cut = kikimeisyou.Length;
        int eq = kikimeisyou.IndexOf('=');
        if (eq >= 0 && eq < cut) cut = eq;
        int star = kikimeisyou.IndexOf('*');
        if (star >= 0 && star < cut) cut = star;
        return kikimeisyou[..cut];
    }

    // C-origin: Yoyaku_Check_Main (Fyss1d.c). tokusyu_key prefix -> that length; else skip first char, cut at first digit (strip suffix number); search fyak_tbl by length + prefix match.
    private static bool ResolveReservedWord(string yoyaku, out string resolved)
    {
        resolved = string.Empty;
        if (yoyaku.Length == 0)
        {
            return false;
        }

        // 【C原典】tokusyu_key 一致時は irc=0(成功)を先に確定させる(iok==0)。
        // fyak_tbl 未一致でも FY-879E にはしない(27A/27B/27C 等の 改訂<8> 対応)。
        bool isSpecialKey = false;
        int length = -1;
        foreach (string key in SpecialReservedKeys)
        {
            if (yoyaku.StartsWith(key, StringComparison.Ordinal))
            {
                length = key.Length;
                isSpecialKey = true;
                break;
            }
        }

        if (length == -1)
        {
            int digitIndex = -1;
            for (int i = 1; i < yoyaku.Length; i++)
            {
                if (char.IsAsciiDigit(yoyaku[i]))
                {
                    digitIndex = i;
                    break;
                }
            }

            length = digitIndex >= 0 ? digitIndex : yoyaku.Length;
        }

        // 【C原典】fyak_tbl 検索。一致すれば s_yoyaku(=品名)を取得する。
        foreach (string word in ReservedWordMaster)
        {
            if (word.Length == length && yoyaku.AsSpan(0, length).SequenceEqual(word))
            {
                resolved = word;
                return true;
            }
        }

        // 【C原典】tokusyu_key 一致(iok==0)は fyak_tbl 未一致でも成功(irc=0)。
        if (isSpecialKey)
        {
            resolved = yoyaku[..length];
            return true;
        }

        return false;
    }

    /// <summary>
    /// 語後の数値の取り出し。【C原典】Find_Bangou(Gyosyu, *number)。
    /// 先頭の数字列を読み飛ばし、続く非数字を読み飛ばした後の数字列を数値化する。
    /// </summary>
    private static bool FindBangou(string text, out int number)
    {
        int i = 0;
        while (i < text.Length && char.IsAsciiDigit(text[i])) i++;      // 先頭数字を読み飛ばす
        while (i < text.Length && !char.IsAsciiDigit(text[i])) i++;     // 非数字を読み飛ばす
        int start = i;
        while (i < text.Length && char.IsAsciiDigit(text[i])) i++;      // 後続数字を抽出
        string numeric = text[start..i];
        number = numeric.Length == 0 ? 0 : int.Parse(numeric);
        return numeric.Length != 0;
    }

    /// <summary>
    /// 語の英字部の取り出し。【C原典】Find_Alphabetto(Gyosyu, alphabetto)。
    /// 先頭の数字列 + 続く英字列を連結して返す。
    /// </summary>
    private static string FindAlphabetto(string text)
    {
        int i = 0;
        var sb = new StringBuilder();
        while (i < text.Length && char.IsAsciiDigit(text[i])) sb.Append(text[i++]);      // 先頭数字
        while (i < text.Length && char.IsAsciiLetter(text[i])) sb.Append(text[i++]);     // 続く英字
        return sb.ToString();
    }

    private static bool StartsWithAny(string text, params string[] prefixes)
    {
        foreach (string p in prefixes)
        {
            if (text.StartsWith(p, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>複合予約語シンボルの区分文字列。【C原典】sym_G/H/K/MH/S/LKAKKO。</summary>
    private static string ControlSymbolText(ControlSymbol symbol) => symbol switch
    {
        ControlSymbol.G => "G",
        ControlSymbol.H => "H",
        ControlSymbol.K => "K",
        ControlSymbol.MH => "MH",
        ControlSymbol.S => "S",
        ControlSymbol.LeftParen => "(",
        _ => string.Empty,
    };

    /// <summary>予約語文チェック(SP/MP)。【C原典】kikitable_set/add + Edit_SPACE + Mojiretu_Find。</summary>
    private void CheckSpMp(int lineNumber, string lineType, string circuitText, CircuitParseResult result)
    {
        EquipmentTableEntry kiki = CreateEquipment(lineNumber, lineType, circuitText, result.CurrentBan);
        kiki.Attributes["1"] = lineType;
        result.MainEquipment.Add(kiki);
        // TODO: Edit_SPACE と Fyss11_Mojiretu_Find(SP/MP)を移植する。
    }

    /// <summary>セパレーター文チェック。【C原典】Fyss11_Check_SEP。</summary>
    private void CheckSeparator(int lineNumber, string circuitText, CircuitParseResult result)
    {
        // TODO: Fyss11_Check_SEP を移植する。
    }

    // ---- ヘルパ ----

    /// <summary>
    /// 機器テーブルエントリの最小生成。
    /// 【C原典】kikitable_set(1,0,0,0, gyonoi, colm, gyosyu, kairoar, ...) + kikitable_add("0", "%03d"(ban))。
    /// </summary>
    private static EquipmentTableEntry CreateEquipment(int lineNumber, string lineType, string circuitText, BanKind ban)
    {
        var kiki = new EquipmentTableEntry
        {
            LineNumber = (short)lineNumber,
            LineType = lineType,
            CircuitText = circuitText,
            Ban = ban,
        };
        kiki.Attributes["0"] = ((short)ban).ToString("D3"); // 【C原典】sprintf(bans, "%03d", ban)
        return kiki;
    }

    /// <summary>
    /// 回路記述中の埋め込み空白チェック。
    /// 【C原典】SPACE_CHK(kairoar, ...) (詳細ルールは移植時に精緻化)。
    /// </summary>
    private static bool CheckNoEmbeddedSpace(int lineNumber, string circuitText, CircuitParseResult result)
    {
        // TODO: C原典 SPACE_CHK の詳細(全角/半角・括弧内除外等)を移植する。
        return true;
    }

    /// <summary>コメント行種(# @ \ CM %)判定。【C原典】memcmp で固定5バイト比較。</summary>
    private static bool IsCommentLineType(string rawLineType)
    {
        string t = rawLineType.Trim();
        return t is "#" or "@" or "\\" or "CM" or "%";
    }

    /// <summary>
    /// 空白除去(漢字2バイトは保持)。
    /// 【C原典】Blankless(toku/sekkei/src/Fyss1a.c)。
    /// </summary>
    private static string Blankless(string value)
        => value.Replace(" ", string.Empty);

    /// <summary>
    /// 空文字列判定。【C原典】NULLSTRING(string)(string[0]=='\0' なら TRUE)。
    /// </summary>
    private static bool IsNullString(string value)
        => value.Length == 0;

    /// <summary>
    /// アルファベットのみ抽出(行種の取り出し)。
    /// 【C原典】Find_Alphabetto1(out gyosyu, in string)。
    /// </summary>
    private static string ExtractAlpha(string value)
    {
        Span<char> buffer = value.Length <= 64 ? stackalloc char[value.Length] : new char[value.Length];
        int count = 0;
        foreach (char c in value)
        {
            if (char.IsLetter(c) && c < 128) // 半角英字のみ(C原典 isalpha)
            {
                buffer[count++] = c;
            }
        }

        return new string(buffer[..count]);
    }

    /// <summary>行番号文字列を数値化。【C原典】atoi(gyono)。</summary>
    private static int ParseLineNumber(string lineNumber)
        => int.TryParse(lineNumber, out int value) ? value : 0;

    /// <summary>
    /// 回路記述文字列の先頭シンボル種別。
    /// 【C原典】enum SYMBOL(sym_END / sym_OTHER / sym_BUNKI / sym_BUNKIUKE)。
    /// </summary>
    private enum KairoSymbol
    {
        End,            // sym_END(終端)
        Other,          // sym_OTHER(通常文字)
        Branch,         // sym_BUNKI(分岐)
        BranchReceiver, // sym_BUNKIUKE(分岐受け '--')
    }

    /// <summary>
    /// 回路記述文字列スキャナ。
    /// 【C原典】kairostart / nextkhar / firstkairo / Find_Kairo
    ///          (グローバル startkairo / kairostring / kh を用いた 1 文字走査)。
    /// C の SJIS 2バイト(kh2)処理は、復号済み string では 1 文字に集約されるため不要。
    /// 分岐マーカ('-','S','P','C',数字,'E','R','Y')は半角 ASCII のため、
    /// UTF-16 文字列上でもそのまま判定できる。
    /// </summary>
    private sealed class KairoScanner
    {
        private const char Eos = '\0'; // 【C原典】EOSCHAR
        private string _text = string.Empty; // 【C原典】startkairo
        private int _next;                    // 【C原典】kairostring - startkairo(次に読む位置)

        /// <summary>現在文字。【C原典】kh(== *(kairostring-1))。</summary>
        public char Kh { get; private set; } = ' ';

        /// <summary>現在位置(1 始まり相当)。【C原典】kairostring - startkairo + 1。</summary>
        public int Column => _next + 1;

        /// <summary>走査開始。【C原典】kairostart(kairoar)。</summary>
        public void Start(string text)
        {
            _text = text;
            _next = 0;
            Kh = ' ';
        }

        /// <summary>1 文字進める。【C原典】nextkhar()。</summary>
        public char NextKhar()
        {
            Kh = _next < _text.Length ? _text[_next++] : Eos;
            return Kh;
        }

        /// <summary>n 文字進める。【C原典】nextkhar() の連続呼び出し。</summary>
        public void Advance(int count)
        {
            for (int i = 0; i < count; i++)
            {
                NextKhar();
            }
        }

        /// <summary>先読み。Peek(0) = 現在文字の次(kairostring 相当)。</summary>
        private char Peek(int offset)
        {
            int index = _next + offset;
            return index >= 0 && index < _text.Length ? _text[index] : Eos;
        }

        /// <summary>
        /// 現在位置から word が一致するか。
        /// 【C原典】memcmp(kairostring-1, word, len)(kairostring-1 == &kh)。
        /// </summary>
        public bool MatchesAt(string word)
        {
            if (Kh != word[0])
            {
                return false;
            }

            for (int i = 1; i < word.Length; i++)
            {
                if (Peek(i - 1) != word[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>先頭シンボル判定。【C原典】firstkairo()。</summary>
        public KairoSymbol FirstKairo()
        {
            if (Kh == '-')
            {
                char n0 = Peek(0);

                // '--' → 分岐受け。【C原典】nextkhar();nextkhar(); return sym_BUNKIUKE
                if (n0 == '-')
                {
                    NextKhar();
                    NextKhar();
                    return KairoSymbol.BranchReceiver;
                }

                // '-SP' の後が英数 → 分岐。【C原典】'S'&&'P' && isalnum(...)
                if (n0 == 'S' && Peek(1) == 'P')
                {
                    char n2 = Peek(2);
                    return IsAsciiLetter(n2) || IsAsciiDigit(n2) ? KairoSymbol.Branch : KairoSymbol.Other;
                }

                // '-C' の後が英字 → 分岐。【C原典】'C' && isalpha(...)
                if (n0 == 'C')
                {
                    return IsAsciiLetter(Peek(1)) ? KairoSymbol.Branch : KairoSymbol.Other;
                }

                // '-{2,3,4}ERY' → 分岐。【C原典】数字 + "ERY"
                if (IsAsciiDigit(n0))
                {
                    if ((n0 == '2' || n0 == '3' || n0 == '4')
                        && Peek(1) == 'E' && Peek(2) == 'R' && Peek(3) == 'Y')
                    {
                        return KairoSymbol.Branch;
                    }

                    return KairoSymbol.Other;
                }

                // 単独 '-' → 分岐(次の 1 文字を読み進める)。【C原典】nextkhar(); return sym_BUNKI
                NextKhar();
                return KairoSymbol.Branch;
            }

            if (Kh == Eos)
            {
                return KairoSymbol.End;
            }

            return KairoSymbol.Other;
        }

        /// <summary>
        /// 1 項目(サブ文字列)取り出し。【C原典】Find_Kairo(P_CHAR string)。
        /// 括弧 '(' ')' のネストを尊重し、分岐シンボル or 終端まで文字を収集する。
        /// </summary>
        public KairoSymbol FindKairo(out string item)
        {
            var buffer = new StringBuilder();

            while (Kh == ' ')
            {
                NextKhar();
            }

            KairoSymbol sym = FirstKairo();
            while (sym == KairoSymbol.Other)
            {
                int depth = Kh == '(' ? 1 : 0; // 【C原典】kakko

                while (depth > 0 && Kh != Eos)
                {
                    buffer.Append(Kh);
                    NextKhar();
                    if (Kh == '(')
                    {
                        depth++;
                    }

                    if (Kh == ')')
                    {
                        depth--;
                    }
                }

                buffer.Append(Kh);
                NextKhar();
                sym = FirstKairo();
            }

            item = buffer.ToString();
            return sym;
        }

        private static bool IsAsciiLetter(char c) => (c is >= 'A' and <= 'Z') || (c is >= 'a' and <= 'z');

        private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';
    }

    /// <summary>
    /// 複合予約語文の先頭シンボル種別。
    /// 【C原典】enum SYMBOL(sym_END/sym_OTHER/sym_KANMA/sym_G/sym_H/sym_K/sym_MH/sym_S/sym_LKAKKO)。
    /// </summary>
    private enum ControlSymbol
    {
        End,        // sym_END(終端)
        Other,      // sym_OTHER(通常文字)
        Comma,      // sym_KANMA(',')
        G,          // sym_G   (G(...) 液面リレー)
        H,          // sym_H   (H(...))
        K,          // sym_K   (K(...))
        MH,         // sym_MH  (MH(...))
        S,          // sym_S   (S(...))
        LeftParen,  // sym_LKAKKO('(')
    }

    /// <summary>
    /// 複合予約語文スキャナ。
    /// 【C原典】controlstart / nextchar / firstcontrol / nextcontrol / Find_Control / Select_Control
    ///          (グローバル startcontrol / controlstring / ch を用いた 1 文字走査)。
    /// C の SJIS 2バイト(ch2)処理は、復号済み string では 1 文字に集約されるため不要。
    /// </summary>
    private sealed class ControlScanner
    {
        private const char Eos = '\0'; // 【C原典】EOSCHAR
        private string _text = string.Empty; // 【C原典】startcontrol
        private int _next;                    // 【C原典】controlstring - startcontrol

        /// <summary>現在文字。【C原典】ch(== *(controlstring-1))。</summary>
        public char Ch { get; private set; } = ' ';

        /// <summary>現在位置(1 始まり相当)。【C原典】controlstring - startcontrol + 1。</summary>
        public int Column => _next + 1;

        /// <summary>走査開始。【C原典】controlstart(control)。</summary>
        public void Start(string text)
        {
            _text = text;
            _next = 0;
            Ch = ' ';
        }

        /// <summary>1 文字進める。【C原典】nextchar()。</summary>
        public char NextChar()
        {
            Ch = _next < _text.Length ? _text[_next++] : Eos;
            return Ch;
        }

        /// <summary>先読み。Peek(0) = 現在文字の次(controlstring 相当)。</summary>
        private char Peek(int offset)
        {
            int index = _next + offset;
            return index >= 0 && index < _text.Length ? _text[index] : Eos;
        }

        /// <summary>
        /// 先頭シンボル判定。【C原典】firstcontrol()。
        /// G/S/H/K は '(' が続くとき複合予約語。MH は "MH(" のとき。'(' 単独は LKAKKO。
        /// (液面リレー/外部の判定 PropChkControlG は未移植のため常に sym_G 扱い。)
        /// </summary>
        public ControlSymbol FirstControl()
        {
            while (Ch == ' ')
            {
                NextChar();
            }

            if (Ch == 'G' && Peek(0) == '(')
            {
                // 【C原典】'(' && 0==PropChkControlG() → sym_G(PropChkControlG は未移植=0扱い)
                NextChar();
                NextChar();
                return ControlSymbol.G;
            }

            if (Ch == 'S' && Peek(0) == '(')
            {
                NextChar();
                NextChar();
                return ControlSymbol.S;
            }

            if (Ch == 'H' && Peek(0) == '(')
            {
                NextChar();
                NextChar();
                return ControlSymbol.H;
            }

            if (Ch == 'K' && Peek(0) == '(')
            {
                NextChar();
                NextChar();
                return ControlSymbol.K;
            }

            if (Ch == 'M' && Peek(0) == 'H' && Peek(1) == '(')
            {
                NextChar();
                NextChar();
                NextChar();
                return ControlSymbol.MH;
            }

            if (Ch == '(')
            {
                NextChar();
                return ControlSymbol.LeftParen;
            }

            if (Ch == Eos)
            {
                return ControlSymbol.End;
            }

            return ControlSymbol.Other;
        }

        /// <summary>目下のシンボル判定。【C原典】nextcontrol()(','→KANMA, EOS→END, 他→OTHER)。</summary>
        private ControlSymbol NextControl()
        {
            if (Ch == ',')
            {
                NextChar();
                return ControlSymbol.Comma;
            }

            return Ch == Eos ? ControlSymbol.End : ControlSymbol.Other;
        }

        /// <summary>
        /// 項目(カンマまで)の取出し。【C原典】Find_Control(P_CHAR string)。
        /// 括弧 '(' ')' のネストを尊重し、カンマ or 終端まで文字を収集する。
        /// </summary>
        public ControlSymbol FindControl(out string item)
        {
            var buffer = new StringBuilder();

            ControlSymbol sym = NextControl();
            while (sym == ControlSymbol.Other)
            {
                int depth = Ch == '(' ? 1 : 0; // 【C原典】kakko

                while (depth > 0 && Ch != Eos)
                {
                    buffer.Append(Ch);
                    NextChar();
                    if (Ch == '(')
                    {
                        depth++;
                    }

                    if (Ch == ')')
                    {
                        depth--;
                    }
                }

                buffer.Append(Ch);
                NextChar();
                sym = NextControl();
            }

            item = buffer.ToString();
            return sym;
        }

        /// <summary>
        /// 括弧のバランス箇所まで取出し。【C原典】Select_Control(P_CHAR string)。
        /// 直前に '(' が消費されている前提(kakko=1 開始)。閉じ括弧までの内容を収集し、
        /// 括弧が閉じたら(kakko==0)true を返す。
        /// </summary>
        public bool SelectControl(out string item)
        {
            var buffer = new StringBuilder();
            int depth = 1; // 【C原典】kakko = 1

            while (Ch != Eos)
            {
                if (Ch == '(')
                {
                    depth++;
                }
                else if (Ch == ')')
                {
                    depth--;
                }

                if (depth <= 0)
                {
                    NextChar(); // 【C原典】閉じ括弧を消費
                    break;
                }

                buffer.Append(Ch);
                NextChar();
            }

            item = buffer.ToString();
            return depth == 0; // 【C原典】(kakko == 0) ? TRUE : FALSE
        }
    }

    /// <summary>
    /// 前方向の単純文字カーソル。【C原典】Fyss11_Check_P/PS/UP のポインタ kairostring 走査。
    /// <see cref="Current"/> は C の *kairostring(未読の現在文字)に相当し、
    /// <see cref="Skip"/> で 1 文字進む(kairostring++)。C の kh/nextkhar 先読みモデルとは異なる。
    /// </summary>
    private sealed class TextCursor
    {
        private const char Eos = '\0'; // 【C原典】EOSCHAR
        private readonly string _text; // 【C原典】startkairo
        private int _pos;              // 【C原典】kairostring - startkairo

        public TextCursor(string text) => _text = text;

        /// <summary>現在(未読)文字。【C原典】*kairostring。終端は '\0'。</summary>
        public char Current => _pos < _text.Length ? _text[_pos] : Eos;

        /// <summary>終端判定。【C原典】*kairostring == EOSCHAR。</summary>
        public bool AtEnd => Current == Eos;

        /// <summary>現在位置(1 始まり)。【C原典】kairostring - startkairo + 1。</summary>
        public int Column => _pos + 1;

        /// <summary>1 文字進める。【C原典】kairostring++。</summary>
        public void Skip()
        {
            if (_pos < _text.Length) { _pos++; }
        }

        /// <summary>n 文字進める。【C原典】kairostring += n。</summary>
        public void Advance(int count) => _pos = Math.Min(_pos + count, _text.Length);

        /// <summary>空白を読み飛ばす。【C原典】while(*kairostring == ' ') kairostring++。</summary>
        public void SkipSpaces()
        {
            while (Current == ' ') { Skip(); }
        }

        /// <summary>現在位置が指定文字列で始まるか。【C原典】strncmp(text, kairostring, n) == 0。</summary>
        public bool MatchesAt(string text)
        {
            if (_pos + text.Length > _text.Length) { return false; }
            return string.CompareOrdinal(_text, _pos, text, 0, text.Length) == 0;
        }
    }
}

