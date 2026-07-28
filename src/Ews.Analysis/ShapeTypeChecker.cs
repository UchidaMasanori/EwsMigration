using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 各回路データの形状タイプをチェックし、デフォルト値が無いときは規定の指定により
/// 変換形状タイプ一覧を作成する。【C原典】<c>Fysk01_Type_Check2</c>(toku/sekkei/src/Fysk01.c:3224)。
///
/// 予約語 <c>yo</c> を <see cref="ShapeTypeTables.ConversionTable"/> と先頭一致で照合し、
/// 一致時はその位置 <c>ichi</c> のデータタイプをシンボル照合して展開形状タイプへ変換する。
/// 一致しない/シンボル未ヒット時は当該位置のデータタイプをそのまま採用する。
/// STM(スイッチ)は接点タイプ追加(1996.12.19)に伴う特別な並べ替えを最後に適用する。
/// </summary>
public static class ShapeTypeChecker
{
    /// <summary>形状タイプ1枠の文字数。【C原典】TSIZE(fyrt808.h:46)。</summary>
    private const int TypeSize = 7;

    /// <summary>
    /// 形状タイプ変換一覧を作成する。【C原典】Fysk01_Type_Check2(yo, ktype, wtype, tsu, ti)。
    /// </summary>
    /// <param name="reservedWord">指定予約語。【C原典】yo。</param>
    /// <param name="dataTypes">データタイプ一覧(各要素は最大 7 文字の固定枠)。【C原典】ktype[][7]。</param>
    /// <returns>変換形状タイプ一覧(各 7 文字)とタイプ位置。</returns>
    public static ShapeTypeResult ResolveShapeTypes(string reservedWord, IReadOnlyList<string> dataTypes)
    {
        ArgumentNullException.ThrowIfNull(reservedWord);
        ArgumentNullException.ThrowIfNull(dataTypes);

        // *ti = 1 ; *tsu = 1 ; memset(wtype[0], ' ', 21) ;
        int position = 1;
        List<string> types = new();

        // 予約語をテーブルと先頭一致で照合(最初にヒットしたものを採用)。
        ShapeTypeTableEntry? matched = null;
        foreach (ShapeTypeTableEntry entry in ShapeTypeTables.ConversionTable)
        {
            if (MatchesPrefix(reservedWord, entry.ReservedWord))
            {
                matched = entry;
                break;
            }
        }

        if (matched is null)
        {
            // 予約語未ヒット: memcpy(wtype[0], ktype[ti], TSIZE)
            types.Add(Slot(dataTypes, position));
        }
        else
        {
            position = matched.Position;

            // ktype[ti] をシンボル照合(最初にヒットした変換を採用)。
            ShapeTypeVariant? hit = null;
            string target = Slot(dataTypes, position);
            foreach (ShapeTypeVariant variant in matched.Variants)
            {
                if (MatchesPrefix(target, variant.Symbol))
                {
                    hit = variant;
                    break;
                }
            }

            if (hit is null)
            {
                // シンボル未ヒット: memcpy(wtype[0], ktype[ti], TSIZE)
                types.Add(Slot(dataTypes, position));
            }
            else
            {
                // 先頭 su 個の設定形状タイプを 7 文字枠へ展開。
                for (int k = 0; k < hit.Count; k++)
                {
                    types.Add(PadSlot(hit.Types[k]));
                }
            }
        }

        // STM 特別処理(1996.12.19 add): 接点タイプの読み直し順に並べ替える。
        types = ApplyStmReorder(reservedWord, types);

        return new ShapeTypeResult(types, position);
    }

    /// <summary>
    /// STM の接点タイプ並べ替え。【C原典】Fysk01_Type_Check2 末尾の STM ブロック。
    /// 先頭形状タイプ(wtype[0], 7 文字)の内容に応じて FC/1C/2C を規定順で並べる。
    /// STM 以外・いずれのパターンにも該当しない場合は入力をそのまま返す。
    /// </summary>
    private static List<string> ApplyStmReorder(string reservedWord, List<string> types)
    {
        if (!MatchesPrefix(reservedWord, "STM"))
        {
            return types;
        }

        string head = types.Count > 0 ? PadSlot(types[0]) : Blank();

        if (head == "NOTHING")
        {
            return new List<string> { "NOTHING", "FC     ", "1C     ", "2C     " };
        }
        if (head == Blank())
        {
            return new List<string> { "       ", "FC     ", "1C     ", "2C     " };
        }
        if (head == "FC     ")
        {
            return new List<string> { "FC     ", "1C     ", "2C     " };
        }
        if (head == "1C     ")
        {
            return new List<string> { "1C     ", "2C     ", "FC     " };
        }
        if (head == "2C     ")
        {
            return new List<string> { "2C     ", "FC     ", "1C     " };
        }

        return types;
    }

    /// <summary>
    /// C の <c>memcmp(src, pat, strlen(pat)) == 0</c> 相当。pat の桁数ぶんだけ先頭一致を判定する。
    /// src が短い場合は空白で桁合わせして比較する。
    /// </summary>
    private static bool MatchesPrefix(string src, string pattern)
    {
        string head = src.Length >= pattern.Length ? src[..pattern.Length] : src.PadRight(pattern.Length);
        return head == pattern;
    }

    /// <summary>
    /// ktype[index](7 文字枠)を取得する。【C原典】memcpy(wtype, ktype[ti], TSIZE)。
    /// 範囲外は空白枠(C の固定長 dtype[7][7] を空白とみなす)。
    /// </summary>
    private static string Slot(IReadOnlyList<string> dataTypes, int index)
    {
        if (index < 0 || index >= dataTypes.Count)
        {
            return Blank();
        }
        return PadSlot(dataTypes[index] ?? string.Empty);
    }

    /// <summary>文字列を 7 文字枠へ空白詰め/切り詰めする(memset ' ' + memcpy strlen 相当)。</summary>
    private static string PadSlot(string value)
    {
        return value.Length >= TypeSize ? value[..TypeSize] : value.PadRight(TypeSize);
    }

    /// <summary>空白 7 文字枠。</summary>
    private static string Blank() => new(' ', TypeSize);
}
