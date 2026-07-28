using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 形状タイプ変換テーブル。予約語別に「参照するデータタイプ位置」と
/// 「シンボル別の展開形状タイプ」を保持する。<see cref="ShapeTypeChecker"/> が参照する。
/// 【C原典】<c>type_tbl2</c> と各 <c>type_t2_xxx</c>(usr/include/toku/sekkei/fyrt819.h)。
///
/// 形状タイプ文字列(sym/typ)は原典リテラルを忠実に転記する(末尾空白も含む)。
/// </summary>
public static class ShapeTypeTables
{
    // 各 type_t2_xxx(シンボル別展開)。C 原典の配列リテラルを忠実に転記する。
    // 末尾の {"",0,...} 終端は「一致なし」の既定経路であり、本モデルでは要素として持たない。

    /// <summary>【C原典】type_t2_elb(fyrt819.h:61)。</summary>
    private static readonly ShapeTypeVariant[] Elb =
    {
        new("TLA", 2, new[] { "TLA", "NT" }),
        new("NT ", 1, new[] { "NT" }),
    };

    /// <summary>【C原典】type_t2_thr / type_t2_2ery / 3ery / 4ery / mg / mgfr(いずれも同一定義)。</summary>
    private static readonly ShapeTypeVariant[] Contact1A1B =
    {
        new("   ", 2, new[] { "1A1B ", "1C " }),
    };

    /// <summary>【C原典】type_t2_ts(fyrt819.h:99)。</summary>
    private static readonly ShapeTypeVariant[] Ts =
    {
        new("   ", 2, new[] { "ET ", "MT " }),
    };

    /// <summary>【C原典】type_t2_tu(fyrt819.h:108)。</summary>
    private static readonly ShapeTypeVariant[] Tu =
    {
        new("   ", 2, new[] { "LD ", "TB " }),
    };

    /// <summary>【C原典】type_t2_ssw(fyrt819.h:113)。</summary>
    private static readonly ShapeTypeVariant[] Ssw =
    {
        new("   ", 2, new[] { "TB ", "ST " }),
    };

    /// <summary>【C原典】type_t2_xl / type_t2_pbs / type_t2_cos(いずれも同一定義)。</summary>
    private static readonly ShapeTypeVariant[] NothingWp =
    {
        new("NOTHING", 2, new[] { "NOTHING", "WP " }),
    };

    /// <summary>【C原典】type_t2_lgr(fyrt819.h:133)。</summary>
    private static readonly ShapeTypeVariant[] Lgr =
    {
        new("   ", 2, new[] { "1A ", "1C " }),
    };

    /// <summary>【C原典】type_t2_ct(fyrt819.h)。</summary>
    private static readonly ShapeTypeVariant[] Ct =
    {
        new("KT ", 3, new[] { "KT ", "LT ", "KE " }),
    };

    /// <summary>【C原典】type_t2_tb(fyrt819.h)。</summary>
    private static readonly ShapeTypeVariant[] Tb =
    {
        new("   ", 3, new[] { "BT ", "RT ", "LTG " }),
    };

    /// <summary>【C原典】type_t3_pbs(fyrt819.h:30)。PBS の接点タイプ展開(最大4タイプ)。</summary>
    private static readonly ShapeTypeVariant[] Pbs =
    {
        new("1A   ", 4, new[] { "1A   ", "2A   ", "3A   ", "4A   " }),
        new("2A   ", 3, new[] { "2A   ", "3A   ", "4A   " }),
        new("3A   ", 2, new[] { "3A   ", "4A   " }),
        new("1B   ", 4, new[] { "1B   ", "2B   ", "3B   ", "4B   " }),
        new("2B   ", 3, new[] { "2B   ", "3B   ", "4B   " }),
        new("3B   ", 2, new[] { "3B   ", "4B   " }),
        new("1A1B ", 4, new[] { "1A1B ", "2A1B ", "1A2B ", "2A2B " }),
        new("2A1B ", 2, new[] { "2A1B ", "2A2B " }),
        new("1A2B ", 2, new[] { "1A2B ", "2A2B " }),
    };

    /// <summary>
    /// 予約語別の形状タイプ変換テーブル本体。【C原典】<c>type_tbl2[]</c>。
    /// 宣言順(照合は先頭一致・最初にヒットした予約語を採用)を保持する。
    /// </summary>
    public static readonly IReadOnlyList<ShapeTypeTableEntry> ConversionTable = new[]
    {
        new ShapeTypeTableEntry("ELB ",  1, Elb),
        new ShapeTypeTableEntry("THR ",  1, Contact1A1B),
        new ShapeTypeTableEntry("MG  ",  1, Contact1A1B),
        new ShapeTypeTableEntry("TS  ",  1, Ts),
        new ShapeTypeTableEntry("MGFR ", 4, Contact1A1B),
        new ShapeTypeTableEntry("TU  ",  1, Tu),
        new ShapeTypeTableEntry("SSW ",  3, Ssw),
        new ShapeTypeTableEntry("GL  ",  4, NothingWp),
        new ShapeTypeTableEntry("RL  ",  4, NothingWp),
        new ShapeTypeTableEntry("OL  ",  4, NothingWp),
        new ShapeTypeTableEntry("BL  ",  4, NothingWp),
        new ShapeTypeTableEntry("WL  ",  4, NothingWp),
        new ShapeTypeTableEntry("PBS ",  5, NothingWp),
        new ShapeTypeTableEntry("COS ",  5, NothingWp),
        new ShapeTypeTableEntry("LGR ",  3, Lgr),
        new ShapeTypeTableEntry("2ERY ", 3, Contact1A1B),
        new ShapeTypeTableEntry("3ERY ", 3, Contact1A1B),
        new ShapeTypeTableEntry("4ERY ", 3, Contact1A1B),
        new ShapeTypeTableEntry("CT  ",  1, Ct),
        new ShapeTypeTableEntry("TB  ",  1, Tb),
    };

    /// <summary>
    /// PBS 専用の形状タイプ変換テーブル。【C原典】<c>type_tbl3[]</c>(fyrt819.h:42)。
    /// <see cref="ShapeTypeChecker.ResolveShapeTypesForPbs"/>(=Fysk01_Type_Check3)が参照する。
    /// </summary>
    public static readonly IReadOnlyList<ShapeTypeTableEntry> PbsConversionTable = new[]
    {
        new ShapeTypeTableEntry("PBS ", 3, Pbs),
    };
}
