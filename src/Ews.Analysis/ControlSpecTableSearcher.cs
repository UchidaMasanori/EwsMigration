namespace Ews.Analysis;

using System.Collections.Generic;
using Ews.Domain.Analysis;

/// <summary>
/// 制御仕様テーブル(FYRT820)を横断検索するリーフ群。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>checkSameSgtkk</c>(改訂&lt;20&gt;)。
///
/// 制御仕様テーブルの記述領域(Pcstrg)から制御対象機器(コロン前をカンマ区切り)を取り出し、
/// 指定予約語が自身以外のエントリの制御対象機器に含まれるかを判定する。
/// </summary>
public static class ControlSpecTableSearcher
{
    /// <summary>
    /// 指定予約語が、自身以外の制御仕様の制御対象機器に含まれるか判定する。
    /// 【C原典】checkSameSgtkk(Fyss1k.c:3569, 改訂&lt;20&gt;)。
    /// 各エントリの Pcstrg のコロン(':')前をカンマで分割し(strtok 相当)、
    /// 予約語と一致するトークンがあれば true を返す。
    /// </summary>
    /// <param name="reservedWord">予約語。【C原典】yoyaku。</param>
    /// <param name="specNameSequence">自身の制御回路仕様名称追番(除外対象)。【C原典】cnameno。</param>
    /// <param name="controlSpecs">制御仕様テーブル。【C原典】P_SgsTable(件数 i_SgsTable)。</param>
    /// <returns>該当する制御対象機器が他エントリに存在すれば true。【C原典】TRUE/FALSE。</returns>
    public static bool HasSameControlTargetEquipment(
        string? reservedWord,
        short specNameSequence,
        IReadOnlyList<ControlSpecEntry> controlSpecs)
    {
        ArgumentNullException.ThrowIfNull(controlSpecs);

        foreach (ControlSpecEntry entry in controlSpecs)
        {
            // 【C原典】自身(cnameno 一致)はスキップ。
            if (entry.SpecNameSequence == specNameSequence)
            {
                continue;
            }

            // 【C原典】strchr(Pcstrg, ':')。コロンが無ければ対象外。
            int colon = entry.RawText.IndexOf(':');
            if (colon < 0)
            {
                continue;
            }

            // 【C原典】コロン前の制御対象機器をカンマ分割(strtok は空トークンを返さない)。
            string targets = entry.RawText.Substring(0, colon);
            foreach (string token in targets.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(token, reservedWord, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
