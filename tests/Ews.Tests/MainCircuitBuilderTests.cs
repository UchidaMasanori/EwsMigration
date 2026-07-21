using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// 主回路生成(<see cref="MainCircuitBuilder"/>)の検証。
/// 【C原典】toku/sekkei/src/Fyss12.c Fyss12_Make_Main / Keitou_Check / Find_Gyosyu_Sym。
/// 本フェーズでは統括骨組みと系統チェック(Keitou_Check)を対象とする。
/// </summary>
public sealed class MainCircuitBuilderTests
{
    /// <summary>行種テーブル1件を生成する。【C原典】struct GYOSYU の主要フィールド。</summary>
    private static LineTypeTableEntry Gyo(short systemNumber, string lineType, char kind, int row,
        short groupNumber = 0, string? raw = null)
        => new()
        {
            SystemNumber = systemNumber,        // 【C原典】K_No
            LineType = lineType,                // 【C原典】gyosyu(整形済)
            LineTypeRaw = raw ?? lineType,      // 【C原典】Gyosyu(原文, Find_Numeric 用)
            DescriptionKind = kind,             // 【C原典】K_kind
            DescriptionRow = row.ToString(),    // 【C原典】K_Gyo
            GroupNumber = groupNumber,          // 【C原典】G_No
        };

    private static CircuitParseResult MakeMain(params LineTypeTableEntry[] lineTypes)
    {
        var parse = new CircuitParseResult();
        parse.LineTypes.AddRange(lineTypes);
        var builder = new MainCircuitBuilder();
        builder.MakeMain(parse);
        return parse;
    }

    /// <summary>回路設計エリア(imagea)付きで MakeMain を実行する。【C原典】imagec/imagea。</summary>
    private static CircuitParseResult MakeMainWithImage(
        LineTypeTableEntry[] lineTypes, params CircuitDescriptionLine[] designArea)
    {
        var parse = new CircuitParseResult();
        parse.LineTypes.AddRange(lineTypes);
        new MainCircuitBuilder().MakeMain(parse, designArea);
        return parse;
    }

    [Fact]
    public void Keitou_Check_正常な系統構成はエラーを出さない()
    {
        // 系統種別'1'(P系): P/M/BN/NP はいずれも許可行種。BN の後続は NP のみ(FY-672E回避)。
        // P には後続の M が必要(Gyosyu_Check の FY-677E回避)。
        var result = MakeMain(
            Gyo(1, "P", '1', 1),
            Gyo(1, "M", '1', 2),
            Gyo(1, "BN", '1', 3),
            Gyo(1, "NP", '1', 4));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Keitou_Check_系統内にBNが2つでFY671E()
    {
        // 【C原典】盤タイトル(BN)は系統内に1つ以下。2つ目で FY-671E。
        var result = MakeMain(
            Gyo(1, "BN", '1', 1),
            Gyo(1, "BN", '1', 2));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-671E");
    }

    [Fact]
    public void Keitou_Check_BNの後にNPC以外が続くとFY672E()
    {
        // 【C原典】BN 検出後、同一系統内で NP/C 以外(ここでは P)が現れると FY-672E。
        var result = MakeMain(
            Gyo(1, "BN", '1', 1),
            Gyo(1, "P", '1', 2));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-672E");
        Assert.Contains(result.Errors, e => e.LineNumber == 2);
    }

    [Fact]
    public void Keitou_Check_系統内にSEPが2つでFY673E()
    {
        // 【C原典】系統終了(SEP)は系統内に1つ以下。2つ目で FY-673E。
        var result = MakeMain(
            Gyo(1, "SEP", '1', 1),
            Gyo(1, "SEP", '1', 2));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-673E");
    }

    [Fact]
    public void Keitou_Check_SEPの後にNPCBN以外が続くとFY674E()
    {
        // 【C原典】SEP 検出後、同一系統内で NP/C/BN 以外(ここでは P)が現れると FY-674E。
        var result = MakeMain(
            Gyo(1, "SEP", '1', 1),
            Gyo(1, "P", '1', 2));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-674E");
    }

    [Fact]
    public void Keitou_Check_PSが1つのみでFY679E()
    {
        // 【C原典】PS は系統内に2つ存在できる。1つのみで FY-679E。
        var result = MakeMain(
            Gyo(1, "PS", '1', 1));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-679E");
    }

    [Fact]
    public void Keitou_Check_PSが3つ以上でFY678E()
    {
        // 【C原典】PS が3つ以上で FY-678E。
        var result = MakeMain(
            Gyo(1, "PS", '1', 1),
            Gyo(1, "PS", '1', 2),
            Gyo(1, "PS", '1', 3));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-678E");
    }

    [Fact]
    public void Keitou_Check_PSが2つは正常()
    {
        // 【C原典】PS がちょうど2つは正常。
        var result = MakeMain(
            Gyo(1, "PS", '1', 1),
            Gyo(1, "PS", '1', 2));

        Assert.DoesNotContain(result.Errors, e => e.ErrorCode is "FY-678E" or "FY-679E");
    }

    [Fact]
    public void Keitou_Check_系統種別1に許可外行種でFY675E()
    {
        // 【C原典】系統種別'1'(P系)は SP を許可しない → FY-675E。
        var result = MakeMain(
            Gyo(1, "SP", '1', 5));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-675E");
    }

    [Fact]
    public void Keitou_Check_系統種別2に許可外行種でFY675E()
    {
        // 【C原典】系統種別'2'(SP系)は P を許可しない → FY-675E。
        var result = MakeMain(
            Gyo(1, "P", '2', 5));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-675E");
    }

    [Fact]
    public void Keitou_Check_系統ごとにBN判定がリセットされる()
    {
        // 系統(K_No)が変わると exist_BN がリセットされるため、別系統の BN は重複扱いにならない。
        var result = MakeMain(
            Gyo(1, "BN", '1', 1),
            Gyo(2, "BN", '1', 2));

        Assert.DoesNotContain(result.Errors, e => e.ErrorCode == "FY-671E");
    }

    // ---- Gyosyu_Check(行種関連チェック / 上下関係)----

    [Fact]
    public void Gyosyu_Check_PからMへの階層は正常で親が設定される()
    {
        // 【C原典】P は後続に M/S が必要。M は前方の P を親(O_No)にする。
        var p = Gyo(1, "P", '1', 1, groupNumber: 10);
        var m = Gyo(1, "M", '1', 2, groupNumber: 20);
        var result = MakeMain(p, m);

        Assert.True(result.IsValid);
        Assert.Equal((short)10, m.ParentGroupNumber); // M.O_No = P.G_No
    }

    [Fact]
    public void Gyosyu_Check_PにMSが後続しないとFY677E()
    {
        // 【C原典】P の後に同一系統内で M も S も無ければ FY-677E。
        var result = MakeMain(Gyo(1, "P", '1', 3));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-677E");
        Assert.Contains(result.Errors, e => e.LineNumber == 3);
    }

    [Fact]
    public void Gyosyu_Check_MにTMPが前置しないとFY677E()
    {
        // 【C原典】M は前方に TM か P が必要。無ければ FY-677E。
        var result = MakeMain(Gyo(1, "M", '1', 4));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-677E");
    }

    [Fact]
    public void Gyosyu_Check_PからTMからMの階層は正常で親が連鎖する()
    {
        // 【C原典】TM は前方の P を親にする。M は前方の TM を親にする。
        var p = Gyo(1, "P", '1', 1, groupNumber: 10);
        var tm = Gyo(1, "TM", '1', 2, groupNumber: 20);
        var m = Gyo(1, "M", '1', 3, groupNumber: 30);
        var result = MakeMain(p, tm, m);

        Assert.True(result.IsValid);
        Assert.Equal((short)10, tm.ParentGroupNumber); // TM.O_No = P.G_No
        Assert.Equal((short)20, m.ParentGroupNumber);  // M.O_No = TM.G_No
    }

    [Fact]
    public void Gyosyu_Check_SMはMに連結し親が設定される()
    {
        // 【C原典】SM(番号1)は前方の M を親(O_No)にする。Find_Numeric("SM")=1。
        var p = Gyo(1, "P", '1', 1, groupNumber: 10);
        var m = Gyo(1, "M", '1', 2, groupNumber: 20);
        var sm = Gyo(1, "SM", '1', 3, groupNumber: 30, raw: "SM");
        var result = MakeMain(p, m, sm);

        Assert.True(result.IsValid);
        Assert.Equal((short)20, sm.ParentGroupNumber); // SM.O_No = M.G_No
    }

    [Fact]
    public void Gyosyu_Check_BにTMMSMが前置しないとFY677E()
    {
        // 【C原典】B/BO は前方に TM/M/SM が必要。P のみが前置だと FY-677E。
        // P の後続 M(末尾)で P チェックは通過し、B で FY-677E になる。
        var result = MakeMain(
            Gyo(1, "P", '1', 1),
            Gyo(1, "B", '1', 2),
            Gyo(1, "M", '1', 3));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-677E");
        Assert.Contains(result.Errors, e => e.LineNumber == 2);
    }

    [Fact]
    public void Gyosyu_Check_PMは後続のMを親に継承する()
    {
        // 【C原典】PM は後方の M 等を親(O_No)と G_kind に継承する。
        var p = Gyo(1, "P", '1', 1, groupNumber: 10);
        var pm = Gyo(1, "PM", '1', 2, groupNumber: 20);
        var m = Gyo(1, "M", '1', 3, groupNumber: 30);
        m.CircuitClass = 'X';
        var result = MakeMain(p, pm, m);

        Assert.True(result.IsValid);
        Assert.Equal(m.ParentGroupNumber, pm.ParentGroupNumber); // PM.O_No = M.O_No
        Assert.Equal('X', pm.CircuitClass);                       // PM.G_kind = M.G_kind
    }

    [Fact]
    public void Gyosyu_Check_PMに後続の対象行種が無いとFY677E()
    {
        // 【C原典】PM の後方に M/B/BO/TM/SM/S が無ければ FY-677E。
        var result = MakeMain(
            Gyo(1, "P", '1', 1),
            Gyo(1, "M", '1', 2),
            Gyo(1, "PM", '1', 3));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-677E");
        Assert.Contains(result.Errors, e => e.LineNumber == 3);
    }

    [Fact]
    public void Gyosyu_Check_改訂15_PM最下段でも回路記述に27Aあれば正常()
    {
        // 【C原典】改訂<15>: PM が系統最下段(後方に対象行種なし)でも、同一行番号の
        //   回路設計エリア記述に 27A/27B/27C があれば FY-677E にしない。
        var lineTypes = new[]
        {
            Gyo(1, "P", '1', 1),
            Gyo(1, "M", '1', 2),
            Gyo(1, "PM", '1', 3),
        };
        var image = new CircuitDescriptionLine { LineNumber = 3, CircuitText = "AB27ACD" };

        var result = MakeMainWithImage(lineTypes, image);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Gyosyu_Check_改訂15_回路記述に27系が無ければFY677E()
    {
        // 【C原典】改訂<15>: 回路記述に 27A/27B/27C が無ければ従来どおり FY-677E。
        var lineTypes = new[]
        {
            Gyo(1, "P", '1', 1),
            Gyo(1, "M", '1', 2),
            Gyo(1, "PM", '1', 3),
        };
        var image = new CircuitDescriptionLine { LineNumber = 3, CircuitText = "AB99XYZ" };

        var result = MakeMainWithImage(lineTypes, image);

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-677E");
    }

    // ==== E.3: Ele_Equal_Check(電気パラメータ同一チェック) ====

    /// <summary>機器テーブル1件を生成する。【C原典】struct KIKITABLE の主要フィールド。</summary>
    private static EquipmentTableEntry Kiki(
        string reservedWord, string ysno, short row = 1, short column = 0,
        params (string Field, string Value)[] rating)
    {
        var kiki = new EquipmentTableEntry
        {
            ReservedWord = reservedWord,        // 【C原典】yoyaku
            ReservedWordNumber = ysno,          // 【C原典】ysno
            LineNumber = row,                   // 【C原典】K_Gyo
            Column = column,                    // 【C原典】K_Ket
        };
        if (rating.Length > 0)
        {
            var values = new RatingValues(reservedWord); // 【C原典】key_tbl
            foreach ((string field, string value) in rating)
            {
                values.Set(field, value);
            }
            kiki.RatingValues = values;
        }
        return kiki;
    }

    private static CircuitParseResult RunEleEqual(params EquipmentTableEntry[] equipment)
    {
        var parse = new CircuitParseResult();
        parse.MainEquipment.AddRange(equipment);
        new MainCircuitBuilder().MakeMain(parse);
        return parse;
    }

    [Fact]
    public void EleEqual_MCDTペアで定格一致は正常()
    {
        // 【C原典】同一 ysno の MCDT が2件、p/a/v/vc 一致 → エラーなし。
        var result = RunEleEqual(
            Kiki("MCDT", "1", 1, 0, ("p", "3"), ("a", "100"), ("v", "200"), ("vc", "110")),
            Kiki("MCDT", "1", 2, 0, ("p", "3"), ("a", "100"), ("v", "200"), ("vc", "110")));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EleEqual_MCDT単独はFY685E()
    {
        // 【C原典】MCDT 単独(同一番号が2件でない)は step4 Yoyakugo_Check_MCDT が先に FY-685E を出す
        //   ため、step16 Ele_Equal_Check の FY-630E(n != 1)には到達しない。
        var result = RunEleEqual(
            Kiki("MCDT", "1", 5, 0, ("p", "3")));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-685E" && e.LineNumber == 5);
    }

    [Fact]
    public void EleEqual_MCDTペアで定格不一致はFY630E()
    {
        // 【C原典】ペアだが v が異なる → FY-630E。
        var result = RunEleEqual(
            Kiki("MCDT", "1", 3, 0, ("p", "3"), ("a", "100"), ("v", "200"), ("vc", "110")),
            Kiki("MCDT", "1", 4, 0, ("p", "3"), ("a", "100"), ("v", "400"), ("vc", "110")));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-630E");
    }

    [Fact]
    public void EleEqual_CSDTペアで定格一致は正常()
    {
        // 【C原典】CSDT は p/a/v/fv を比較する。
        var result = RunEleEqual(
            Kiki("CSDT", "2", 1, 0, ("p", "2"), ("a", "100"), ("v", "200"), ("fv", "A")),
            Kiki("CSDT", "2", 2, 0, ("p", "2"), ("a", "100"), ("v", "200"), ("fv", "A")));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EleEqual_MC_2件目に入力がありFY631E()
    {
        // 【C原典】MC の2件目以降が非空 → FY-631E。
        var result = RunEleEqual(
            Kiki("MC", "1", 1, 0, ("p", "3"), ("a", "100")),
            Kiki("MC", "1", 7, 0, ("p", "3")));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-631E" && e.LineNumber == 7);
    }

    [Fact]
    public void EleEqual_MC_2件目が空なら基準値を複写する()
    {
        // 【C原典】MC の2件目が空 → 基準(1件目)の p/a/v/fv 等を複写。
        var baseMc = Kiki("MC", "1", 1, 0, ("p", "3"), ("a", "100"), ("v", "200"), ("fv", "A"));
        var dupMc = Kiki("MC", "1", 2, 0);

        var parse = new CircuitParseResult();
        parse.MainEquipment.Add(baseMc);
        parse.MainEquipment.Add(dupMc);
        new MainCircuitBuilder().MakeMain(parse);

        Assert.True(parse.IsValid);
        Assert.NotNull(dupMc.RatingValues);
        Assert.Equal("3", dupMc.RatingValues!.Get("p"));
        Assert.Equal("100", dupMc.RatingValues.Get("a"));
        Assert.Equal("200", dupMc.RatingValues.Get("v"));
        Assert.Equal("A", dupMc.RatingValues.Get("fv"));
    }

    [Fact]
    public void EleEqual_TSW重複はFY682E()
    {
        // 【C原典】同一 ysno の TSW 重複は step4 Yoyakugo_Check_OTHER が先に FY-682E(後続機器位置)を出す
        //   ため、step16 Ele_Equal_Check の FY-630E には到達しない。
        var result = RunEleEqual(
            Kiki("TSW", "1", 8, 0),
            Kiki("TSW", "1", 9, 0));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-682E" && e.LineNumber == 9);
    }

    [Fact]
    public void EleEqual_ysnoが0の機器は対象外()
    {
        // 【C原典】ysno==0(atoi)は同一チェック対象外。MC 単独(ysno=0)は step4/step16 とも素通り。
        //   (MCDT の ysno=0 は step4 Yoyakugo_Check_MCDT が FY-684E を出すため別途 E.4 で検証。)
        var result = RunEleEqual(
            Kiki("MC", "0", 1, 0, ("p", "3")));

        Assert.True(result.IsValid);
    }

    // ==== E.4: Yoyakugo_Check_Double(機器情報関連チェック) ====

    /// <summary>機器テーブル+行種テーブルで MakeMain を実行する。</summary>
    private static CircuitParseResult RunYoyakugo(
        LineTypeTableEntry[] lineTypes, params EquipmentTableEntry[] equipment)
    {
        var parse = new CircuitParseResult();
        parse.LineTypes.AddRange(lineTypes);
        parse.MainEquipment.AddRange(equipment);
        new MainCircuitBuilder().MakeMain(parse);
        return parse;
    }

    /// <summary>行種グループNo付きで機器テーブル1件を生成する。【C原典】KIKITABLE(G_No 付)。</summary>
    private static EquipmentTableEntry KikiG(
        string reservedWord, string ysno, short groupNumber, short row = 1, short column = 0)
        => new()
        {
            ReservedWord = reservedWord,        // 【C原典】yoyaku
            ReservedWordNumber = ysno,          // 【C原典】ysno
            GroupNumber = groupNumber,          // 【C原典】G_No
            LineNumber = row,                   // 【C原典】K_Gyo
            Column = column,                    // 【C原典】K_Ket
        };

    [Fact]
    public void Yoyakugo_RMCB同番号重複はFY682E()
    {
        // 【C原典】Yoyakugo_Check_RM: RMCB 番号付きの後続同番号重複 → FY-682E(後続機器位置)。
        var result = RunEleEqual(
            Kiki("RMCB", "1", 3, 0),
            Kiki("RMCB", "1", 4, 0));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-682E" && e.LineNumber == 4);
    }

    [Fact]
    public void Yoyakugo_RRYは同番号重複を許容する()
    {
        // 【C原典】Yoyakugo_Check_RM: RRY は除外(番号重複を許容)。
        var result = RunEleEqual(
            Kiki("RRY", "1", 3, 0),
            Kiki("RRY", "1", 4, 0));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Yoyakugo_MCDT番号なしはFY684E()
    {
        // 【C原典】Yoyakugo_Check_MCDT: 予約語だけ(ysno==0)は不可 → FY-684E。
        var result = RunEleEqual(
            Kiki("MCDT", "0", 6, 0));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-684E" && e.LineNumber == 6);
    }

    [Fact]
    public void Yoyakugo_MCDT同番号が3件はFY685E()
    {
        // 【C原典】Yoyakugo_Check_MCDT: 同一番号は丁度2件のみ許可(2件でない→ FY-685E)。
        var result = RunEleEqual(
            Kiki("MCDT", "1", 1, 0),
            Kiki("MCDT", "1", 2, 0),
            Kiki("MCDT", "1", 3, 0));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-685E");
    }

    [Fact]
    public void Yoyakugo_TR番号付きはFY683E()
    {
        // 【C原典】Yoyakugo_Check_TR: TR に番号付きは不可 → FY-683E(行種!=PS)。
        var result = RunEleEqual(
            Kiki("TR", "1", 7, 0));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-683E" && e.LineNumber == 7);
    }

    [Fact]
    public void Yoyakugo_TR番号なしは正常()
    {
        // 【C原典】Yoyakugo_Check_TR: 番号なし TR はエラーなし(2電源TR検証は E.4b 保留)。
        var result = RunEleEqual(
            Kiki("TR", "0", 7, 0));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Yoyakugo_SC位置不正はFY649E()
    {
        // 【C原典】Yoyakugo_Check_SC: 行種が PM/O/B/S/BO 以外(ここでは行種未定義=空)は FY-649E。
        var result = RunEleEqual(
            Kiki("SC", "0", 5, 0));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-649E" && e.LineNumber == 5);
    }

    [Fact]
    public void Yoyakugo_MCと同番号MGはFY682E()
    {
        // 【C原典】Yoyakugo_Check_OTHER: MC 番号付きと同一番号の MG は不可 → FY-682E(MG位置)。
        var result = RunEleEqual(
            Kiki("MC", "1", 2, 0),
            Kiki("MG", "1", 8, 0));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-682E" && e.LineNumber == 8);
    }

    [Fact]
    public void Yoyakugo_一般機器の同番号重複はFY682E()
    {
        // 【C原典】Yoyakugo_Check_OTHER: MC/LGR/ELR/MCDT/CSDT/TR 以外の番号付き後続重複 → FY-682E。
        var result = RunEleEqual(
            Kiki("MCB", "1", 2, 0),
            Kiki("MCB", "1", 9, 0));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-682E" && e.LineNumber == 9);
    }

    [Fact]
    public void Yoyakugo_SC行種Sは正常()
    {
        // 【C原典】Yoyakugo_Check_SC: 行種 O/B/S/BO は許可。
        //   有効な行種構成(系統1: P→S)を用意し、SC を行種グループ(S)に属させる。
        var lineTypes = new[]
        {
            Gyo(1, "P", '1', 1, groupNumber: 1),
            Gyo(1, "S", '1', 2, groupNumber: 2),
        };
        var result = RunYoyakugo(lineTypes,
            KikiG("SC", "0", groupNumber: 2, row: 2));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Yoyakugo_SC行種PMで直後同一グループはFY656E()
    {
        // 【C原典】Yoyakugo_Check_SC: 行種 PM で直後機器が同一グループNo → FY-656E。
        //   有効な行種構成(系統1: P→PM→M)を用意。
        var lineTypes = new[]
        {
            Gyo(1, "P",  '1', 1, groupNumber: 1),
            Gyo(1, "PM", '1', 2, groupNumber: 2),
            Gyo(1, "M",  '1', 3, groupNumber: 3),
        };
        var result = RunYoyakugo(lineTypes,
            KikiG("SC",  "0", groupNumber: 2, row: 2, column: 0),
            KikiG("MCB", "0", groupNumber: 2, row: 2, column: 1));

        Assert.Contains(result.Errors, e => e.ErrorCode == "FY-656E");
    }

    [Fact]
    public void Yoyakugo_SC行種PMで直後別グループは正常()
    {
        // 【C原典】Yoyakugo_Check_SC: 行種 PM で直後機器が別グループNo → 可(return 0)。
        var lineTypes = new[]
        {
            Gyo(1, "P",  '1', 1, groupNumber: 1),
            Gyo(1, "PM", '1', 2, groupNumber: 2),
            Gyo(1, "M",  '1', 3, groupNumber: 3),
        };
        var result = RunYoyakugo(lineTypes,
            KikiG("SC",  "0", groupNumber: 2, row: 2),
            KikiG("MCB", "0", groupNumber: 3, row: 3));

        Assert.True(result.IsValid);
    }

    // ==== E.5: Kairo_Kubun_Set(回路区分セット) ====

    [Fact]
    public void KairoKubun_CT_WHパターンは両方K()
    {
        // 【C原典】計器パターン "CT,WH" 一致 → 構成機器すべて K_Kubun='K'。
        var ct = Kiki("CT", "0", 1, 0);
        var wh = Kiki("WH", "0", 2, 0);
        var result = RunEleEqual(ct, wh);

        Assert.True(result.IsValid);
        Assert.Equal('K', ct.CircuitDivision);
        Assert.Equal('K', wh.CircuitDivision);
    }

    [Fact]
    public void KairoKubun_単独CTはM()
    {
        // 【C原典】CT 単独はパターン不一致 → 基本区分 'M'(CT/WH/AM は 'M')。
        var ct = Kiki("CT", "0", 1, 0);
        RunEleEqual(ct);

        Assert.Equal('M', ct.CircuitDivision);
    }

    [Fact]
    public void KairoKubun_MCB単独はM_MCB_LGRは両方K()
    {
        // 【C原典】MCB は 'M'。直後 LGR があれば両者 'K' として取り込む。
        var mcbAlone = Kiki("MCB", "0", 1, 0);
        RunEleEqual(mcbAlone);
        Assert.Equal('M', mcbAlone.CircuitDivision);

        var mcb = Kiki("MCB", "0", 1, 0);
        var lgr = Kiki("LGR", "0", 2, 0);
        RunEleEqual(mcb, lgr);
        Assert.Equal('K', mcb.CircuitDivision);
        Assert.Equal('K', lgr.CircuitDivision);
    }

    [Fact]
    public void KairoKubun_ZCT_ELRは両方K()
    {
        // 【C原典】ZCT は 'K'。行種 TM/M 以外では直後 ELR を 'K' として取り込む。
        var zct = Kiki("ZCT", "0", 1, 0);
        var elr = Kiki("ELR", "0", 2, 0);
        RunEleEqual(zct, elr);

        Assert.Equal('K', zct.CircuitDivision);
        Assert.Equal('K', elr.CircuitDivision);
    }

    [Fact]
    public void KairoKubun_ZCT_LGRは行種未指定ではLGRを取り込まない()
    {
        // 【C原典】行種 TM/M 以外では直後 LGR は取り込まない(ELR のみ)。ZCT='K', LGR は基本区分 'M'。
        var zct = Kiki("ZCT", "0", 1, 0);
        var lgr = Kiki("LGR", "0", 2, 0);
        RunEleEqual(zct, lgr);

        Assert.Equal('K', zct.CircuitDivision);
        Assert.Equal('M', lgr.CircuitDivision);
    }

    [Fact]
    public void KairoKubun_ZCT_LGRは行種TMで両方K()
    {
        // 【C原典】行種 TM/M では直後 LGR/ELR を 'K' として取り込む(950907)。
        //   有効な行種構成(系統1: P→TM→M)を用意し、ZCT/LGR を TM グループに属させる。
        var lineTypes = new[]
        {
            Gyo(1, "P",  '1', 1, groupNumber: 1),
            Gyo(1, "TM", '1', 2, groupNumber: 2),
            Gyo(1, "M",  '1', 3, groupNumber: 3),
        };
        var zct = KikiG("ZCT", "0", groupNumber: 2, row: 2, column: 0);
        var lgr = KikiG("LGR", "0", groupNumber: 2, row: 2, column: 1);
        RunYoyakugo(lineTypes, zct, lgr);

        Assert.Equal('K', zct.CircuitDivision);
        Assert.Equal('K', lgr.CircuitDivision);
    }

    [Fact]
    public void KairoKubun_F_VMパターンは両方K()
    {
        // 【C原典】計器パターン "F,VM" 一致 → 両者 'K'(F は既定でも 'K')。
        var f = Kiki("F", "0", 1, 0);
        var vm = Kiki("VM", "0", 2, 0);
        RunEleEqual(f, vm);

        Assert.Equal('K', f.CircuitDivision);
        Assert.Equal('K', vm.CircuitDivision);
    }

    [Fact]
    public void KairoKubun_表示灯WL単独はK()
    {
        // 【C原典】XL(表示灯 WL/GL/RL/OL/BL/HM/FL/CR)は 'K'。
        var wl = Kiki("WL", "0", 1, 0);
        var asw = Kiki("AS", "0", 2, 0);
        RunEleEqual(wl, asw);

        Assert.Equal('K', wl.CircuitDivision);
        Assert.Equal('K', asw.CircuitDivision);
    }

    [Fact]
    public void KairoKubun_SC直後同一グループはS()
    {
        // 【C原典】SC は直後機器が同一行種グループなら 'S'。
        var lineTypes = new[]
        {
            Gyo(1, "P", '1', 1, groupNumber: 1),
            Gyo(1, "S", '1', 2, groupNumber: 2),
        };
        var sc = KikiG("SC", "0", groupNumber: 2, row: 2, column: 0);
        var mcb = KikiG("MCB", "0", groupNumber: 2, row: 2, column: 1);
        RunYoyakugo(lineTypes, sc, mcb);

        Assert.Equal('S', sc.CircuitDivision);
    }

    [Fact]
    public void KairoKubun_SC直後別グループはM()
    {
        // 【C原典】SC は直後機器が別グループなら 'M'。
        var lineTypes = new[]
        {
            Gyo(1, "P", '1', 1, groupNumber: 1),
            Gyo(1, "S", '1', 2, groupNumber: 2),
        };
        var sc = KikiG("SC", "0", groupNumber: 2, row: 2, column: 0);
        var mcb = KikiG("MCB", "0", groupNumber: 3, row: 3, column: 0);
        RunYoyakugo(lineTypes, sc, mcb);

        Assert.Equal('M', sc.CircuitDivision);
    }

    [Fact]
    public void KairoKubun_行種PMの機器はK()
    {
        // 【C原典】基本的回路区分: 行種 PM の機器は 'K'。
        var lineTypes = new[]
        {
            Gyo(1, "P",  '1', 1, groupNumber: 1),
            Gyo(1, "PM", '1', 2, groupNumber: 2),
            Gyo(1, "M",  '1', 3, groupNumber: 3),
        };
        var dev = KikiG("MCCB", "0", groupNumber: 2, row: 2, column: 0);
        RunYoyakugo(lineTypes, dev);

        Assert.Equal('K', dev.CircuitDivision);
    }

    // ==== step6/7: Yoyakugo_Add_Main(D_No*=10) / qsort(cmp by D_No) ====

    [Fact]
    public void AddDerivedEquipment_機器Noを10倍にスケーリングする()
    {
        // 【C原典】Yoyakugo_Add_Main 前段: while(i<Max_Kikic){ (S_Kiki+i)->D_No *= 10; }
        var k1 = new EquipmentTableEntry { EquipmentNumber = 1 };
        var k2 = new EquipmentTableEntry { EquipmentNumber = 2 };
        var k3 = new EquipmentTableEntry { EquipmentNumber = 3 };
        var parse = new CircuitParseResult();
        parse.MainEquipment.AddRange(new[] { k1, k2, k3 });

        new MainCircuitBuilder().MakeMain(parse);

        Assert.Equal(10, k1.EquipmentNumber);
        Assert.Equal(20, k2.EquipmentNumber);
        Assert.Equal(30, k3.EquipmentNumber);
    }

    [Fact]
    public void SortEquipmentByNumber_機器Noの昇順に並べ替える()
    {
        // 【C原典】qsort(P_Kiki,*i_Kikic,sizeof(KIKITABLE),cmp): cmp = D_No 昇順。
        //   MakeMain では先に D_No*=10(step6)後にソート(step7)するため、
        //   入力 3,1,2 → ×10 で 30,10,20 → 昇順 10,20,30。
        var k3 = new EquipmentTableEntry { EquipmentNumber = 3, ReservedWord = "C" };
        var k1 = new EquipmentTableEntry { EquipmentNumber = 1, ReservedWord = "A" };
        var k2 = new EquipmentTableEntry { EquipmentNumber = 2, ReservedWord = "B" };
        var parse = new CircuitParseResult();
        parse.MainEquipment.AddRange(new[] { k3, k1, k2 });

        new MainCircuitBuilder().MakeMain(parse);

        Assert.Equal(new[] { "A", "B", "C" }, parse.MainEquipment.Select(k => k.ReservedWord).ToArray());
        Assert.Equal(new short[] { 10, 20, 30 }, parse.MainEquipment.Select(k => k.EquipmentNumber).ToArray());
    }

    [Fact]
    public void SortEquipmentByNumber_同一機器Noは入力順を保持する()
    {
        // 安定ソート: 同一 D_No(ここでは全て0)の相対順序は入力順を維持する。
        var a = new EquipmentTableEntry { EquipmentNumber = 0, ReservedWord = "A" };
        var b = new EquipmentTableEntry { EquipmentNumber = 0, ReservedWord = "B" };
        var c = new EquipmentTableEntry { EquipmentNumber = 0, ReservedWord = "C" };
        var parse = new CircuitParseResult();
        parse.MainEquipment.AddRange(new[] { a, b, c });

        new MainCircuitBuilder().MakeMain(parse);

        Assert.Equal(new[] { "A", "B", "C" }, parse.MainEquipment.Select(k => k.ReservedWord).ToArray());
    }

    // ==== step8: Gyosyu_Rank_Set(行種ランク/出現数セット) ====

    [Fact]
    public void Gyosyu_Rank_Set_入線Pは基点でRank0_Cnt1_機器無し子行種はRank据置()
    {
        // 【C原典】Gyosyu_Rank_Set: 入線(P)は Rank=0/Cnt=1 の基点。
        //   子行種(M)は機器が無ければ Cnt=0 で、親(P)Rank と同値のまま(Cnt>0 で +1 のため据置)。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var result = MakeMain(p, m);

        Assert.True(result.IsValid);
        Assert.Equal((short)0, p.Rank);
        Assert.Equal((short)1, p.Count);
        Assert.Equal((short)0, m.Rank);
        Assert.Equal((short)0, m.Count);
    }

    [Fact]
    public void Gyosyu_Rank_Set_機器数量のある子行種はRankとCntが加算される()
    {
        // 【C原典】Gyosyu_Rank_Set + Kiki_Suryou_Set/Calc:
        //   子行種(M)のグループに数量2の機器があると Cnt=2、親が入線(P)で Cnt>0 のため Rank=P.Rank+1=1。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        // グループ2に数量2の MCB(F/CT/VT 以外なので Kiki_Suryou_Calc が数量を返す)。
        var mcb = new EquipmentTableEntry
        {
            ReservedWord = "MCB",       // 【C原典】yoyaku
            ReservedWordNumber = "0",   // 【C原典】ysno(0 は同一チェック対象外)
            GroupNumber = 2,            // 【C原典】G_No
            LineNumber = 2,             // 【C原典】K_Gyo
            Quantity = 2,               // 【C原典】Kosu
        };
        var result = RunYoyakugo(new[] { p, m }, mcb);

        Assert.True(result.IsValid);
        Assert.Equal((short)2, m.Count);
        Assert.Equal((short)1, m.Rank);
    }

    [Fact]
    public void Gyosyu_Rank_Set_PM行種は後続M行種のRankを継承する()
    {
        // 【C原典】Gyosyu_Rank_Set パス2: PM/O 行種は同一系統内で後続の TM/M/SM/B/BO の Rank を継承。
        //   P→PM→M 構成で、M グループに数量2の機器を置くと M.Rank=1(親 PM は入線以外で Cnt>1)。
        //   PM は後続 M の Rank(1)を継承する。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var pm = Gyo(1, "PM", '1', 2, groupNumber: 2);
        var m = Gyo(1, "M", '1', 3, groupNumber: 3);
        var mcb = new EquipmentTableEntry
        {
            ReservedWord = "MCB",
            ReservedWordNumber = "0",
            GroupNumber = 3,            // 【C原典】M のグループ
            LineNumber = 3,
            Quantity = 2,               // 【C原典】Kosu(Cnt>1 で親 PM の Rank+1)
        };
        var result = RunYoyakugo(new[] { p, pm, m }, mcb);

        Assert.True(result.IsValid);
        Assert.Equal((short)1, m.Rank);
        Assert.Equal((short)1, pm.Rank);
    }

    // ==== step9-13.5: Kiki_Rank_Set / Kiki_Rank_Update / Gyosyu_Rank_Update /
    //                  Pattern_Rank_Update / WH_Rank_Set / TR_Rank_Set ====

    [Fact]
    public void Pattern_Rank_Update_グループ先頭の主機器はTOP_Flgが1になる()
    {
        // 【C原典】Kiki_Rank_Update/Pattern_Rank_Update: 行種グループ先頭の主機器(K_Kubun='M')は
        //   先頭機器フラグ(TOP_Flg)が '1'。有効な P→M 構成で MCCB(主機器)を1台配置する。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var mccb = new EquipmentTableEntry
        {
            ReservedWord = "MCCB",      // 【C原典】yoyaku(type_MCB → 主機器)
            ReservedWordNumber = "0",
            SystemNumber = 1,           // 【C原典】K_No
            GroupNumber = 2,            // 【C原典】G_No(M 行種)
            EquipmentNumber = 1,        // 【C原典】D_No
            LineNumber = 2,
        };
        var result = RunYoyakugo(new[] { p, m }, mccb);

        Assert.True(result.IsValid);
        Assert.Equal('M', mccb.CircuitDivision); // step5 で主機器区分
        Assert.Equal('1', mccb.TopFlag);         // グループ先頭
    }

    [Fact]
    public void TR_Rank_Set_2電源トランスはTRのRankが0にリセットされる()
    {
        // 【C原典】TR_Rank_Set: 「TR」直後に「PS」がある2電源トランス(行種が PS 以外)は、
        //   後続 PS のランクを TR に合わせた後、TR 自身とその行種のランクを 0 にする。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var tr = new EquipmentTableEntry
        {
            ReservedWord = "TR",        // 【C原典】yoyaku
            ReservedWordNumber = "0",   // 番号なし(FY-683E 回避)
            SystemNumber = 1,           // 【C原典】K_No
            GroupNumber = 2,            // 行種 M(≠PS)
            EquipmentNumber = 1,        // 【C原典】D_No(TR が先)
            LineNumber = 1,
        };
        var ps = new EquipmentTableEntry
        {
            ReservedWord = "PS",        // 【C原典】yoyaku
            ReservedWordNumber = "0",
            SystemNumber = 1,
            GroupNumber = 2,
            EquipmentNumber = 2,        // 【C原典】D_No(TR の直後)
            LineNumber = 2,
        };
        var result = RunYoyakugo(new[] { p, m }, tr, ps);

        Assert.True(result.IsValid);
        Assert.Equal((short)0, tr.Rank); // TR_Rank_Set が最終的に 0 へリセット
    }

    // ==== step17: Fyss12_Make_Main_Sub / Main_File_Area_Make(主回路ファイルエリア作成・数量分解) ====

    /// <summary>主機器(MCCB)テーブル1件を生成する。【C原典】KIKITABLE(主回路機器)。</summary>
    private static EquipmentTableEntry MainKiki(
        short groupNumber, short equipmentNumber, short stringSequence = 0,
        short circuitNumberSequence = 0, short groupQuantity = 0, short row = 2)
        => new()
        {
            ReservedWord = "MCCB",                          // 【C原典】yoyaku(type_MCB → 主機器)
            ReservedWordNumber = "0",                       // ysno=0(同一チェック対象外)
            SystemNumber = 1,                               // 【C原典】K_No
            GroupNumber = groupNumber,                      // 【C原典】G_No
            EquipmentNumber = equipmentNumber,              // 【C原典】D_No(step6 で×10)
            StringSequence = stringSequence,                // 【C原典】B_No
            CircuitNumberSequence = circuitNumberSequence,  // 【C原典】N_No
            GroupQuantity = groupQuantity,                  // 【C原典】GKosu
            LineNumber = row,                               // 【C原典】K_Gyo
        };

    [Fact]
    public void MakeMainSub_単一機器グループはSimpleセグメント1件()
    {
        // 【C原典】Find_Group → Main_File_Make_s。繰り返し/回路番号文なしの単純グループ。
        //   step6 で D_No は×10 されるため Min/Max は 10。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var result = RunYoyakugo(new[] { p, m }, MainKiki(groupNumber: 2, equipmentNumber: 1));

        Assert.True(result.IsValid);
        Assert.Single(result.MainCircuitSegments);
        var seg = result.MainCircuitSegments[0];
        Assert.Equal(MainCircuitSegmentKind.Simple, seg.Kind);
        Assert.Equal((short)1, seg.Count);
        Assert.Equal((short)2, seg.GroupNumber);
        Assert.Equal((short)10, seg.MinNumber);
        Assert.Equal((short)10, seg.MaxNumber);
    }

    [Fact]
    public void MakeMainSub_同一グループ複数機器は1件のSimpleセグメントにまとまる()
    {
        // 【C原典】Find_Group は同一 G_No/B_No/N_No を1グループとして数える(kensu=2)。
        //   D_No は×10 で 10,20。Min=10, Max=20。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var result = RunYoyakugo(
            new[] { p, m },
            MainKiki(groupNumber: 2, equipmentNumber: 1),
            MainKiki(groupNumber: 2, equipmentNumber: 2));

        Assert.True(result.IsValid);
        Assert.Single(result.MainCircuitSegments);
        var seg = result.MainCircuitSegments[0];
        Assert.Equal(MainCircuitSegmentKind.Simple, seg.Kind);
        Assert.Equal((short)2, seg.Count);
        Assert.Equal((short)10, seg.MinNumber);
        Assert.Equal((short)20, seg.MaxNumber);
    }

    [Fact]
    public void MakeMainSub_グループ数量ありはIterationセグメントになる()
    {
        // 【C原典】Find_Iteration: GKosu≠0 の機器を繰り返し基点として検出 → Main_File_Make_d。
        //   先頭機器に GKosu=3 を設定。D_No は×10 で 10,20。
        //   StartNumber=先頭 D_No(10), MaxNumber=最終 D_No(20), Iteration=GKosu(3)。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var result = RunYoyakugo(
            new[] { p, m },
            MainKiki(groupNumber: 2, equipmentNumber: 1, groupQuantity: 3),
            MainKiki(groupNumber: 2, equipmentNumber: 2));

        Assert.True(result.IsValid);
        Assert.Single(result.MainCircuitSegments);
        var seg = result.MainCircuitSegments[0];
        Assert.Equal(MainCircuitSegmentKind.Iteration, seg.Kind);
        Assert.Equal((short)2, seg.Count);
        Assert.Equal((short)10, seg.StartNumber);
        Assert.Equal((short)20, seg.MaxNumber);
        Assert.Equal((short)3, seg.Iteration);
    }

    // ==== step17: mainfile_set(主回路ファイルエリア = FYRT800 レコード整形) ====

    /// <summary>系統テーブル付きで主回路生成を実行する。【C原典】Find_Keitou 用の KEITOU を用意する。</summary>
    private static CircuitParseResult RunMainFile(
        SystemTableEntry[] systems, LineTypeTableEntry[] lineTypes, params EquipmentTableEntry[] equipment)
    {
        var parse = new CircuitParseResult();
        parse.Systems.AddRange(systems);
        parse.LineTypes.AddRange(lineTypes);
        parse.MainEquipment.AddRange(equipment);
        new MainCircuitBuilder().MakeMain(parse);
        return parse;
    }

    [Fact]
    public void MainFileSet_単純グループで主回路レコードを1件生成する()
    {
        // 【C原典】Main_File_Make_s → mainfile_pre_set → mainfile_set。Kosu 既定 0→1 で 1 レコード。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var result = RunYoyakugo(new[] { p, m }, MainKiki(groupNumber: 2, equipmentNumber: 1));

        Assert.True(result.IsValid);
        var rec = Assert.Single(result.MainCircuits);
        Assert.Equal("001", rec.SequenceNumber);          // 【C原典】datano = "%03d" *Pmainc
        Assert.Equal("001", rec.Data.SystemNumber);       // 【C原典】kno = "%03d" K_No(=1)
        Assert.Equal("MCCB", rec.Data.ReservedWord);      // 【C原典】yoyaku
        Assert.Equal("00", rec.Data.IdentityNumber);      // 【C原典】doukkno = "%02d" E_No(=0)
        Assert.Equal("000", rec.Data.DescriptionColumn);  // 【C原典】keta(K_Ket 未設定)
        Assert.Equal("000", rec.Data.DescriptionRow);     // 【C原典】gyo(K_Gyo 未設定)
        Assert.Equal("000", rec.Data.LineTypeGroupNumber);// 【C原典】gyoglno = "000"
    }

    [Fact]
    public void MainFileSet_系統テーブルから系統種別をセットする()
    {
        // 【C原典】mainfile_set: S_Keitou->Kind != '\0' のとき ksyubetu = Kind。
        var sys = new SystemTableEntry { SystemNumber = 1, SystemKind = '1' };
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var result = RunMainFile(new[] { sys }, new[] { p, m }, MainKiki(groupNumber: 2, equipmentNumber: 1));

        Assert.True(result.IsValid);
        var rec = Assert.Single(result.MainCircuits);
        Assert.Equal('1', rec.Data.SystemKind);
    }

    [Fact]
    public void MainFileSet_数量分のレコードを生成し生成サフィックスを付与する()
    {
        // 【C原典】mainfile_pre_set: Kosu 分だけ mainfile_set。
        //   ysno!=0 かつ (Max_Iteration||Max_Suryo)!=0 のとき yssfx=(safix+'A')。
        //   safix = Iteration(0)*max(Kosu,1)+Suryo(=0,1) → 'A','B'。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var kiki = MainKiki(groupNumber: 2, equipmentNumber: 1);
        kiki.ReservedWordNumber = "1"; // ysno!=0
        kiki.Quantity = 2;             // Kosu=2 → 2 レコード
        var result = RunYoyakugo(new[] { p, m }, kiki);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.MainCircuits.Count);
        Assert.Equal("001", result.MainCircuits[0].SequenceNumber);
        Assert.Equal("002", result.MainCircuits[1].SequenceNumber);
        Assert.Equal("1", result.MainCircuits[0].Data.DesignationNumber);
        Assert.Equal('A', result.MainCircuits[0].Data.DesignationSuffix);
        Assert.Equal('B', result.MainCircuits[1].Data.DesignationSuffix);
    }

    [Fact]
    public void MainFileMakeD_繰り返し回数と機器数の積だけレコードを生成する()
    {
        // 【C原典】Main_File_Make_d: 繰り返し区間(D_No 以降の機器)を Iteration 回出力。
        //   先頭機器 GKosu=3、区間内 2 機器 → 3×2=6 レコード。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var result = RunYoyakugo(
            new[] { p, m },
            MainKiki(groupNumber: 2, equipmentNumber: 1, groupQuantity: 3),
            MainKiki(groupNumber: 2, equipmentNumber: 2));

        Assert.True(result.IsValid);
        Assert.Equal(6, result.MainCircuits.Count);
        Assert.Equal("001", result.MainCircuits[0].SequenceNumber);
        Assert.Equal("006", result.MainCircuits[5].SequenceNumber);
    }

    [Fact]
    public void MainFileMakeD_繰り返し番号ごとに生成サフィックスが変わる()
    {
        // 【C原典】mainfile_set: yssfx = Iteration*max(Kosu,1)+Suryo + 'A'。
        //   単一機器を GKosu=3 で 3 回繰り返し → 'A'/'B'/'C'。ysno!=0 が付与条件。
        var kiki = MainKiki(groupNumber: 2, equipmentNumber: 1, groupQuantity: 3);
        kiki.ReservedWordNumber = "1"; // ysno!=0
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var result = RunYoyakugo(new[] { p, m }, kiki);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.MainCircuits.Count);
        Assert.Equal('A', result.MainCircuits[0].Data.DesignationSuffix);
        Assert.Equal('B', result.MainCircuits[1].Data.DesignationSuffix);
        Assert.Equal('C', result.MainCircuits[2].Data.DesignationSuffix);
    }

    // ==== step6: Yoyakugo_Add_Main(計器回路 CT の主回路レコード展開) ====

    /// <summary>系統番号付きで計器/主機器テーブル1件を生成する。【C原典】KIKITABLE。</summary>
    private static EquipmentTableEntry InstKiki(
        string reservedWord, short groupNumber, short equipmentNumber, short systemNumber = 1)
        => new()
        {
            ReservedWord = reservedWord,        // 【C原典】yoyaku
            ReservedWordNumber = "0",           // 【C原典】ysno(0 は同一チェック対象外)
            SystemNumber = systemNumber,        // 【C原典】K_No
            GroupNumber = groupNumber,          // 【C原典】G_No
            EquipmentNumber = equipmentNumber,  // 【C原典】D_No(step6 で×10)
            LineNumber = 2,                     // 【C原典】K_Gyo
        };

    [Fact]
    public void AddDerivedEquipment_CT_WH計器回路はCT主回路とWH計器回路を追加する()
    {
        // 【C原典】Yoyakugo_Add_Main findtype==type_CT: 同一グループの CT+WH(K_Kubun='K')を
        //   走査し、Kikitable_Keiki_Make で WH 計器回路(D_No=CT.D_No-1, TOP_Flg='1')、
        //   Kikitable_Main_Make で CT 主回路(D_No=末尾機器D_No+1, K_Kubun='M', yoyakkbn='1')を追加する。
        //   系統先頭機器は系統ブレークで消費されるため、先頭に主機器(MCCB)を置く。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var result = RunYoyakugo(
            new[] { p, m },
            InstKiki("MCCB", groupNumber: 2, equipmentNumber: 1),
            InstKiki("CT", groupNumber: 2, equipmentNumber: 2),
            InstKiki("WH", groupNumber: 2, equipmentNumber: 3));

        // 元3件 + CT主回路 + WH計器回路 = 5件。
        Assert.Equal(5, result.MainEquipment.Count);

        // CT 主回路レコード(自動生成・主機器区分)。
        var ctMain = Assert.Single(result.MainEquipment,
            k => k.ReservedWord == "CT" && k.CircuitDivision == 'M');
        Assert.Equal('1', ctMain.AutoGenerationKind);
        Assert.Equal((short)0, ctMain.EquipmentIdentityNumber);
        // 末尾機器 WH(D_No=30)+1 = 31。
        Assert.Equal((short)31, ctMain.EquipmentNumber);

        // WH 計器回路レコード(自動生成・計器区分)。D_No = CT(D_No=20)-1 = 19。
        var whKeiki = Assert.Single(result.MainEquipment,
            k => k.ReservedWord == "WH" && k.AutoGenerationKind == '1');
        Assert.Equal('K', whKeiki.CircuitDivision);
        Assert.Equal((short)19, whKeiki.EquipmentNumber);
    }

    [Fact]
    public void AddDerivedEquipment_計器回路なしでは機器を追加しない()
    {
        // 【C原典】K_Kubun!='K'(主機器のみ)の系統では計器回路展開は発生しない。
        var p = Gyo(1, "P", '1', 1, groupNumber: 1);
        var m = Gyo(1, "M", '1', 2, groupNumber: 2);
        var result = RunYoyakugo(
            new[] { p, m },
            InstKiki("MCCB", groupNumber: 2, equipmentNumber: 1),
            InstKiki("MCCB", groupNumber: 2, equipmentNumber: 2));

        Assert.Equal(2, result.MainEquipment.Count);
        Assert.DoesNotContain(result.MainEquipment, k => k.AutoGenerationKind == '1');
    }
}