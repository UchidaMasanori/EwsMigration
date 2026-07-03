namespace Ews.Domain.Analysis;

/// <summary>
/// 1機器分の定格値ホルダ。【C原典】<c>union fyrt811</c> / KIKITABLE の <c>key_tbl</c>。
///
/// C は約90型を union で重ねるが、1機器につき有効な型は <c>s_yoyaku</c> で定まる1つのみ。
/// 本移植では「フィールド名 → 格納値文字列」の辞書で表現する。
/// C の未登録判定 <c>field[0] != '\0'</c> は <see cref="Has"/> に対応する。
/// 交流/直流 区分 fv/fvc も通常フィールド("fv"/"fvc")として格納する。
/// この表現により Ele_Equal_Check(Fyss12 step3)の型別フィールド比較を素直に実装できる。
///
/// 値の格納・範囲検証は Ews.Analysis の ElectricalParameterChecker(【C原典】Fyss1d.c
/// key_check_main/key_check_&lt;TYPE&gt;)が行う。本型はその結果を機器テーブル
/// (<see cref="EquipmentTableEntry"/>)へ保持し、後段チェックへ引き渡すためのドメインモデル。
/// </summary>
public sealed class RatingValues
{
    private readonly Dictionary<string, string> _fields = new(StringComparer.Ordinal);

    /// <summary>予約語(型名)を指定して生成する。【C原典】s_yoyaku。</summary>
    public RatingValues(string typeName) => TypeName = typeName;

    /// <summary>予約語(型名)。【C原典】s_yoyaku。</summary>
    public string TypeName { get; }

    /// <summary>格納済みフィールドの読み取り専用ビュー。</summary>
    public IReadOnlyDictionary<string, string> Fields => _fields;

    /// <summary>フィールドが登録済みか。【C原典】<c>field[0] != '\0'</c>。</summary>
    public bool Has(string field) => _fields.TryGetValue(field, out string? v) && v.Length > 0;

    /// <summary>フィールド値を取得する(未登録は null)。</summary>
    public string? Get(string field) => _fields.TryGetValue(field, out string? v) ? v : null;

    /// <summary>フィールド値を格納する。【C原典】<c>memcpy( field, val, n )</c>。</summary>
    public void Set(string field, string value) => _fields[field] = value;
}
