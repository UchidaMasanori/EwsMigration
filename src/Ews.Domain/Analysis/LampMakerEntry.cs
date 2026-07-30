namespace Ews.Domain.Analysis;

/// <summary>
/// ランプ機器 優先メーカー切替テーブル(sel_LAMP.cns)の 1 行。
/// 【C原典】struct lamp_seltbl(PropCnsLampRead 内ローカル定義, Fysk00.c:11738)。
///   工場コード(地区グループ)と予約語が一致する行のメーカーコード順位(4 件)を採用する。
/// </summary>
/// <param name="FacilityGroup">工場コード(地区グループ)。【C原典】fgrp。</param>
/// <param name="ReservedWord">予約語。【C原典】yoyaku[8]。</param>
/// <param name="MakerCodes">メーカーコード選定順位(4 件, 各 3 桁)。【C原典】mkcd1?4。</param>
public sealed record LampMakerEntry(int FacilityGroup, string ReservedWord, string[] MakerCodes);
