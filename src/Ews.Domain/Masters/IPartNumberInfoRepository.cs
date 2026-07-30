namespace Ews.Domain.Masters;

/// <summary>
/// 依頼明細番号をキーに品番情報(hbninf / .clh)を取得するリポジトリ。
/// 【C原典】FyCpHbHbnInfFileR(toku/compo/lib/clhbn_dir/clfilerw.c)。
///   案件ごとの生バイナリ 1 レコードファイルを依頼明細番号で読み込む境界。
///   ファイルが無い・サイズ不一致のときは null(C 原典の NULL 返却に相当)。
/// </summary>
public interface IPartNumberInfoRepository
{
    /// <summary>指定した依頼明細番号の品番情報を取得する。無ければ null。</summary>
    /// <param name="requestDetailNumber">依頼明細番号(依頼番号 7 + 明細番号 2)。【C原典】iraimei。</param>
    PartNumberInfo? Find(string requestDetailNumber);
}
