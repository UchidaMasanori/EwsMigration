using Ews.Analysis;
using Ews.Domain.Analysis;
using Ews.Domain.Masters;
using Xunit;

namespace Ews.Tests;

/// <summary>
/// タイプチェック(変換形状タイプ一覧作成)検証。
/// 【C原典】Fysk01_Type_Check / Fysk01_HandleRock_Check / Fysk08_Usetype_Check /
///          Fysk01_Keijyoutype_Check (Fysk01.c / Fysk08.c, type_tbl=fyrt819.h)。
/// </summary>
public sealed class ShapeTypeSelectorTests
{
    /// <summary>7 枠のデータタイプ配列を作る(不足枠はブランク)。</summary>
    private static List<string> Slots(params string[] values)
    {
        var list = new List<string>(7);
        for (int i = 0; i < 7; i++)
        {
            list.Add(i < values.Length ? values[i] : string.Empty);
        }
        return list;
    }

    /// <summary>予約語マスタ 1 件(機器選定要素区分 7 枠)。</summary>
    private static ReservedWordMaster Master(string word, params char[] kinds)
        => new() { ReservedWord = word, SelectionElementKinds = kinds };

    /// <summary>全 7 枠が機器選定要素('1')のマスタ。</summary>
    private static ReservedWordMaster AllUsed(string word)
        => Master(word, '1', '1', '1', '1', '1', '1', '1');

    // ---- CheckHandleLock (=Fysk01_HandleRock_Check) ----

    [Fact]
    public void ハンドルロック_MCBで位置5がHLなら5を返す()
    {
        List<string> dt = Slots("KT", "ET", "ST", "", "", "HL");
        Assert.Equal(5, ShapeTypeSelector.CheckHandleLock("MCB", dt));
    }

    [Fact]
    public void ハンドルロック_位置5がHLでなければ_1を返す()
    {
        List<string> dt = Slots("KT", "ET", "ST", "", "", "ET");
        Assert.Equal(-1, ShapeTypeSelector.CheckHandleLock("MCB", dt));
    }

    [Fact]
    public void ハンドルロック_hdlチェック無しの予約語は_1を返す()
    {
        List<string> dt = Slots("KM", "ET", "ST");
        Assert.Equal(-1, ShapeTypeSelector.CheckHandleLock("MMCB", dt));
    }

    // ---- ApplyUseTypeMask (=Fysk08_Usetype_Check) ----

    [Fact]
    public void 使用有無_区分が空白の枠をクリアする()
    {
        ReservedWordMaster m = Master("AM", '1', '1', '1', ' ', ' ', ' ', ' ');
        List<string> dt = Slots("AA", "BB", "CC", "DD", "EE", "FF", "GG");

        (bool found, IReadOnlyList<string> masked) = ShapeTypeSelector.ApplyUseTypeMask("AM", dt, [m]);

        Assert.True(found);
        Assert.Equal("AA", masked[0].TrimEnd());
        Assert.Equal("CC", masked[2].TrimEnd());
        Assert.Equal("", masked[3].TrimEnd());
        Assert.Equal("", masked[6].TrimEnd());
    }

    [Fact]
    public void 使用有無_予約語がマスタに無ければ未検出()
    {
        ReservedWordMaster m = AllUsed("AM");
        (bool found, _) = ShapeTypeSelector.ApplyUseTypeMask("ZZ", Slots("AA"), [m]);
        Assert.False(found);
    }

    // ---- BuildConvertedShapeTypes (=Fysk01_Keijyoutype_Check) ----

    [Fact]
    public void 形状一覧_MCB既定分岐_選択番号8でKMKYETST()
    {
        // bn='1'(盤種別内), ks='1'(3相でない), gs="B"(分岐), fi=' ', ss="01" → ii=8。
        List<string> dt = Slots(""); // 位置0がブランク → 既定分岐
        (IReadOnlyList<string> w, int tsu, int ti) =
            ShapeTypeSelector.BuildConvertedShapeTypes("MCB", dt, '1', '1', "B", ' ', "01", 0);

        Assert.Equal(0, ti);
        Assert.Equal(4, tsu);
        Assert.Equal(new[] { "KM", "KY", "ET", "ST" }, new[] { w[0].TrimEnd(), w[1].TrimEnd(), w[2].TrimEnd(), w[3].TrimEnd() });
    }

    [Fact]
    public void 形状一覧_MCB位置0が非ブランクならそのまま採用()
    {
        List<string> dt = Slots("KT");
        (IReadOnlyList<string> w, int tsu, _) =
            ShapeTypeSelector.BuildConvertedShapeTypes("MCB", dt, '1', '1', "B", ' ', "01", 0);

        Assert.Equal(1, tsu);
        Assert.Equal("KT", w[0].TrimEnd());
    }

    [Fact]
    public void 形状一覧_MCB特別処理tfg1は選択番号99で未ヒットtsu0()
    {
        List<string> dt = Slots("");
        (IReadOnlyList<string> w, int tsu, _) =
            ShapeTypeSelector.BuildConvertedShapeTypes("MCB", dt, '1', '1', "B", ' ', "01", 1);

        Assert.Equal(0, tsu);
        Assert.Equal("", w[0].TrimEnd());
    }

    [Fact]
    public void 形状一覧_ELB特別処理tfg2は選択番号8でKMETST()
    {
        List<string> dt = Slots("");
        (IReadOnlyList<string> w, int tsu, _) =
            ShapeTypeSelector.BuildConvertedShapeTypes("ELB", dt, '1', '1', "B", ' ', "01", 2);

        Assert.Equal(3, tsu);
        Assert.Equal(new[] { "KM", "ET", "ST" }, new[] { w[0].TrimEnd(), w[1].TrimEnd(), w[2].TrimEnd() });
    }

    [Fact]
    public void 形状一覧_PBSは位置6を採用しNOTHINGを付与()
    {
        List<string> dt = Slots("", "", "", "", "", "", "XX");
        (IReadOnlyList<string> w, int tsu, int ti) =
            ShapeTypeSelector.BuildConvertedShapeTypes("PBS", dt, '1', '1', "B", ' ', "01", 0);

        Assert.Equal(6, ti);
        Assert.Equal(1, tsu);
        Assert.Equal("XX", w[0].TrimEnd());
        Assert.Equal("NOTHING", w[1].TrimEnd());
    }

    [Fact]
    public void 形状一覧_WHは位置3がKEでなければNOTHINGとKE()
    {
        List<string> dt = Slots("", "", "", "AB");
        (IReadOnlyList<string> w, int tsu, int ti) =
            ShapeTypeSelector.BuildConvertedShapeTypes("WH", dt, '1', '1', "B", ' ', "01", 0);

        Assert.Equal(3, ti);
        Assert.Equal(2, tsu);
        Assert.Equal("NOTHING", w[0].TrimEnd());
        Assert.Equal("KE", w[1].TrimEnd());
    }

    [Fact]
    public void 形状一覧_WHは位置3がKEならそのまま採用()
    {
        List<string> dt = Slots("", "", "", "KE");
        (IReadOnlyList<string> w, int tsu, _) =
            ShapeTypeSelector.BuildConvertedShapeTypes("WH", dt, '1', '1', "B", ' ', "01", 0);

        Assert.Equal(1, tsu);
        Assert.Equal("KE", w[0].TrimEnd());
    }

    [Fact]
    public void 形状一覧_EEはwtype0が位置0で上書きされRO_CPが残る()
    {
        // 【C原典】EE 分岐は break が無く終端落ちで wtype[0]=ktype[0] 上書き。
        List<string> dt = Slots("ZZ");
        (IReadOnlyList<string> w, int tsu, int ti) =
            ShapeTypeSelector.BuildConvertedShapeTypes("EE", dt, '1', '1', "B", ' ', "01", 0);

        Assert.Equal(0, ti);
        Assert.Equal(3, tsu);
        Assert.Equal("ZZ", w[0].TrimEnd());
        Assert.Equal("RO", w[1].TrimEnd());
        Assert.Equal("CP", w[2].TrimEnd());
    }

    [Fact]
    public void 形状一覧_TRは盤種別1でUT_RT()
    {
        List<string> dt = Slots("");
        (IReadOnlyList<string> w, int tsu, _) =
            ShapeTypeSelector.BuildConvertedShapeTypes("TR", dt, '1', '1', "B", ' ', "01", 0);

        Assert.Equal(2, tsu);
        Assert.Equal(new[] { "UT", "RT" }, new[] { w[0].TrimEnd(), w[1].TrimEnd() });
    }

    [Fact]
    public void 形状一覧_TRは盤種別1以外でRT()
    {
        List<string> dt = Slots("");
        (IReadOnlyList<string> w, int tsu, _) =
            ShapeTypeSelector.BuildConvertedShapeTypes("TR", dt, '2', '1', "B", ' ', "01", 0);

        Assert.Equal(1, tsu);
        Assert.Equal("RT", w[0].TrimEnd());
    }

    [Fact]
    public void 形状一覧_未登録予約語は位置0のデータタイプをそのまま採用()
    {
        List<string> dt = Slots("XY");
        (IReadOnlyList<string> w, int tsu, int ti) =
            ShapeTypeSelector.BuildConvertedShapeTypes("ZZZ", dt, '1', '1', "B", ' ', "01", 0);

        Assert.Equal(0, ti);
        Assert.Equal(1, tsu);
        Assert.Equal("XY", w[0].TrimEnd());
    }

    [Fact]
    public void 形状一覧_回路3相と製作仕様外で選択番号が加算される()
    {
        // ks='3'(+16), gs="B"(分岐+8), fi=' ', bn='1', ss="99"(標準外+1) → ii=25。
        // type_t_mcb seleno25 = ET,ST。
        List<string> dt = Slots("");
        (IReadOnlyList<string> w, int tsu, _) =
            ShapeTypeSelector.BuildConvertedShapeTypes("MCB", dt, '1', '3', "B", ' ', "99", 0);

        Assert.Equal(2, tsu);
        Assert.Equal(new[] { "ET", "ST" }, new[] { w[0].TrimEnd(), w[1].TrimEnd() });
    }

    // ---- Select (=Fysk01_Type_Check) 統合 ----

    [Fact]
    public void 統合_MCBは3関数を通し変換形状と位置を返す()
    {
        List<string> dt = Slots("", "", "", "", "", "HL");
        ShapeTypeCheckResult r = ShapeTypeSelector.Select(
            "MCB", dt, '1', '1', "B", ' ', "01", 0, [AllUsed("MCB")]);

        Assert.True(r.Found);
        Assert.Equal(5, r.HandleLockPosition);           // 位置5が "HL"
        Assert.Equal(4, r.TypeCount);                    // ii=8 → KM,KY,ET,ST
        Assert.Equal("KM", r.ConvertedTypes[0].TrimEnd());
    }

    [Fact]
    public void 統合_予約語がマスタ未登録ならNOGOOD()
    {
        List<string> dt = Slots("AA");
        ShapeTypeCheckResult r = ShapeTypeSelector.Select(
            "ZZ", dt, '1', '1', "B", ' ', "01", 0, [AllUsed("MCB")]);

        Assert.False(r.Found);
        Assert.Equal(0, r.TypeCount);
        Assert.Empty(r.ConvertedTypes);
    }

    [Fact]
    public void 統合_使用有無で位置0がクリアされ既定分岐に入る()
    {
        // マスタで位置0を未使用(' ')にすると、入力があってもクリアされ既定分岐へ。
        ReservedWordMaster m = Master("MCB", ' ', '1', '1', '1', '1', '1', '1');
        List<string> dt = Slots("KT"); // 位置0に値ありだがクリアされる
        ShapeTypeCheckResult r = ShapeTypeSelector.Select(
            "MCB", dt, '1', '1', "B", ' ', "01", 0, [m]);

        Assert.True(r.Found);
        Assert.Equal("", r.DataTypes[0].TrimEnd());      // クリア済
        Assert.Equal(4, r.TypeCount);                    // 既定分岐 ii=8
        Assert.Equal("KM", r.ConvertedTypes[0].TrimEnd());
    }
}
