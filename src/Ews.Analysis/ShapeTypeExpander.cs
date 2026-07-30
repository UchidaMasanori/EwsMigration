namespace Ews.Analysis;

/// <summary>
/// 形状タイプ(2)の展開結果。【C原典】Fysk01_Type_Check2 の出力 wtype / tsu / ti。
/// </summary>
/// <param name="ShapeTypes">変換形状タイプ一覧(各7桁, 件数=tsu)。【C原典】wtype[][7]。</param>
/// <param name="TypeIndex">タイプ位置。【C原典】ti。</param>
public sealed record ShapeTypeExpansion(IReadOnlyList<string> ShapeTypes, int TypeIndex);

/// <summary>
/// データタイプ(dtype)を予約語別テーブルで展開し、直近上下位検索で試行する形状タイプ一覧を求める。
/// 【C原典】Fysk01_Type_Check2(toku/sekkei/src/Fysk01.c:3224)＋ type_tbl2(toku/include/sekkei/fyrt819.h:148)。
///   予約語が type_tbl2 にあればタイプ位置 ti をそのエントリの ichi に設定し、dtype[ti] が
///   設定タイプの sym に前方一致すれば su 個の代替タイプへ展開する。一致しなければ dtype[ti] そのまま。
///   STM は接点タイプの並べ替えを別途行う。
/// </summary>
public static class ShapeTypeExpander
{
    private const int TypeWidth = 7;
    private static readonly string Blank7 = new(' ', TypeWidth);

    // 【C原典】Type_T2: シンボル・type数・設定type[3]。
    private sealed record TypeRule(string Symbol, params string[] Types);

    // 【C原典】Type_Tbl2: 予約語・設定位置 ichi・設定タイプ情報。
    private sealed record TableEntry(string ReservedWord, int Index, TypeRule[] Rules);

    // 【C原典】type_t2_* (fyrt819.h)。末尾の end 行(sym="")は Rules 配列終端で表現。
    private static readonly TypeRule[] Elb = [new("TLA", "TLA", "NT"), new("NT ", "NT")];
    private static readonly TypeRule[] OnOff2 = [new("   ", "1A1B ", "1C ")];   // THR/2ERY/3ERY/4ERY/MG/MGFR 共通
    private static readonly TypeRule[] Ts = [new("   ", "ET ", "MT ")];
    private static readonly TypeRule[] Tu = [new("   ", "LD ", "TB ")];
    private static readonly TypeRule[] Ssw = [new("   ", "TB ", "ST ")];
    private static readonly TypeRule[] Xl = [new("NOTHING", "NOTHING", "WP ")];   // WL/GL/RL/OL/BL/PBS/COS 共通
    private static readonly TypeRule[] Lgr = [new("   ", "1A ", "1C ")];
    private static readonly TypeRule[] Ct = [new("KT ", "KT ", "LT ", "KE ")];
    private static readonly TypeRule[] Tb = [new("   ", "BT ", "RT ", "LTG ")];

    // 【C原典】type_tbl2[](fyrt819.h:148)。
    private static readonly TableEntry[] Table =
    [
        new("ELB ", 1, Elb),
        new("THR ", 1, OnOff2),
        new("MG  ", 1, OnOff2),
        new("TS  ", 1, Ts),
        new("MGFR ", 4, OnOff2),
        new("TU  ", 1, Tu),
        new("SSW ", 3, Ssw),
        new("GL  ", 4, Xl),
        new("RL  ", 4, Xl),
        new("OL  ", 4, Xl),
        new("BL  ", 4, Xl),
        new("WL  ", 4, Xl),
        new("PBS ", 5, Xl),
        new("COS ", 5, Xl),
        new("LGR ", 3, Lgr),
        new("2ERY ", 3, OnOff2),
        new("3ERY ", 3, OnOff2),
        new("4ERY ", 3, OnOff2),
        new("CT  ", 1, Ct),
        new("TB  ", 1, Tb),
    ];

    /// <summary>
    /// 形状タイプを展開する。【C原典】Fysk01_Type_Check2(yo, ktype, wtype, tsu, ti)。
    /// </summary>
    /// <param name="reservedWord">指定予約語。【C原典】yo(=tbl.yoyaku)。</param>
    /// <param name="dataTypes">データタイプ(7枠)。【C原典】ktype(=dtype)。</param>
    public static ShapeTypeExpansion Expand(string reservedWord, IReadOnlyList<string> dataTypes)
    {
        ArgumentNullException.ThrowIfNull(reservedWord);
        ArgumentNullException.ThrowIfNull(dataTypes);

        int typeIndex = 1;
        var shapeTypes = new List<string> { Blank7 };

        foreach (TableEntry entry in Table)
        {
            if (!Matches(reservedWord, entry.ReservedWord, entry.ReservedWord.Length))
            {
                continue;
            }

            // 【C原典】ti = type_tbl2[i].ichi。
            typeIndex = entry.Index;
            string target = DataTypeAt(dataTypes, typeIndex);

            bool matched = false;
            foreach (TypeRule rule in entry.Rules)
            {
                if (Matches(target, rule.Symbol, rule.Symbol.Length))
                {
                    // 【C原典】設定type を su 個展開。
                    shapeTypes = [.. rule.Types.Select(Pad7)];
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                // 【C原典】sym 不一致は末尾に到達 → wtype[0]=ktype[ti]。
                shapeTypes = [Pad7(target)];
            }
            break;
        }

        if (Table.All(e => !Matches(reservedWord, e.ReservedWord, e.ReservedWord.Length)))
        {
            // 【C原典】予約語がテーブルに無い場合は ti=1・wtype[0]=ktype[1]。
            shapeTypes = [Pad7(DataTypeAt(dataTypes, typeIndex))];
        }

        // 【C原典】STM の接点タイプ並べ替え(1996.12.19 追加)。
        if (Matches(reservedWord, "STM ", 3))
        {
            shapeTypes = ExpandStm(shapeTypes[0]);
        }

        return new ShapeTypeExpansion(shapeTypes, typeIndex);
    }

    // 【C原典】STM は wtype[0] の現在値で 3～4 タイプに並べ替える。
    private static List<string> ExpandStm(string current)
    {
        string head = Pad7(current);
        if (head == "NOTHING") return [Pad7("NOTHING"), Pad7("FC "), Pad7("1C "), Pad7("2C ")];
        if (head == Blank7) return [Blank7, Pad7("FC "), Pad7("1C "), Pad7("2C ")];
        if (head == Pad7("FC ")) return [Pad7("FC "), Pad7("1C "), Pad7("2C ")];
        if (head == Pad7("1C ")) return [Pad7("1C "), Pad7("2C "), Pad7("FC ")];
        if (head == Pad7("2C ")) return [Pad7("2C "), Pad7("FC "), Pad7("1C ")];
        return [head];
    }

    // 【C原典】type_t3_pbs(fyrt819.h:30)。PBS のタイプ位置3(ti3)の追加展開。
    private static readonly TypeRule[] Pbs3 =
    [
        new("1A   ", "1A ", "2A ", "3A ", "4A "),
        new("2A   ", "2A ", "3A ", "4A "),
        new("3A   ", "3A ", "4A "),
        new("1B   ", "1B ", "2B ", "3B ", "4B "),
        new("2B   ", "2B ", "3B ", "4B "),
        new("3B   ", "3B ", "4B "),
        new("1A1B ", "1A1B ", "2A1B ", "1A2B ", "2A2B "),
        new("2A1B ", "2A1B ", "2A2B "),
        new("1A2B ", "1A2B ", "2A2B "),
    ];

    // 【C原典】type_tbl3[](fyrt819.h:43)。現状 PBS のみ(ichi=3)。
    private static readonly TableEntry[] Table3 = [new("PBS ", 3, Pbs3)];

    /// <summary>
    /// PBS のタイプ位置3(ti3)を追加展開する。【C原典】Fysk01_Type_Check3(Fysk01.c:3325)+type_tbl3。
    /// </summary>
    /// <param name="reservedWord">指定予約語。【C原典】yo。</param>
    /// <param name="dataTypes">データタイプ(7枠)。【C原典】ktype(=dtype)。</param>
    public static ShapeTypeExpansion ExpandSecondary(string reservedWord, IReadOnlyList<string> dataTypes)
    {
        ArgumentNullException.ThrowIfNull(reservedWord);
        ArgumentNullException.ThrowIfNull(dataTypes);

        int typeIndex = 1;
        var shapeTypes = new List<string> { Blank7 };

        foreach (TableEntry entry in Table3)
        {
            if (!Matches(reservedWord, entry.ReservedWord, entry.ReservedWord.Length))
            {
                continue;
            }

            typeIndex = entry.Index;
            string target = DataTypeAt(dataTypes, typeIndex);

            bool matched = false;
            foreach (TypeRule rule in entry.Rules)
            {
                if (Matches(target, rule.Symbol, rule.Symbol.Length))
                {
                    shapeTypes = [.. rule.Types.Select(Pad7)];
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                shapeTypes = [Pad7(target)];
            }
            return new ShapeTypeExpansion(shapeTypes, typeIndex);
        }

        // 【C原典】テーブルに無い予約語は ti=1・wtype[0]=ktype[1]。
        return new ShapeTypeExpansion([Pad7(DataTypeAt(dataTypes, typeIndex))], typeIndex);
    }

    private static string DataTypeAt(IReadOnlyList<string> dataTypes, int index)
    {
        string value = index >= 0 && index < dataTypes.Count ? dataTypes[index] ?? string.Empty : string.Empty;
        return Pad7(value);
    }

    private static string Pad7(string value) => value.PadRight(TypeWidth)[..TypeWidth];

    // 【C原典】memcmp(a, b, width): 先頭 width バイトの一致。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
