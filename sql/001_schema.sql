/* =====================================================================================
   EWS 移行 パイロット用 SQL Server スキーマ (正規化)

   対象は回路解析(C原典: fyskews / Fysk10_Main)が触れるデータのみ。
   旧 ISAM ファイル / .cns マスタを正規化テーブルへ再設計する。
   元の C 構造体/ファイルIDは各テーブルのコメントに併記する。

   実行例: sqlcmd -S localhost -i sql/001_schema.sql
   ===================================================================================== */

IF DB_ID('Ews') IS NULL
    CREATE DATABASE Ews;
GO
USE Ews;
GO

/* ---------------------------------------------------------------------------
   機器マスター
   【C原典】struct FYDM805 (機器マスター, EWS-ISAM, レコード長 579)
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.EquipmentMaster', 'U') IS NOT NULL
    DROP TABLE dbo.EquipmentMaster;
GO
CREATE TABLE dbo.EquipmentMaster
(
    ReservedWord         NVARCHAR(8)   NOT NULL,   -- 【C原典】pkey.yoyaku[8](PRIMARY キーの一部)
    MakerCode            NVARCHAR(3)   NOT NULL,   -- 【C原典】pkey.mkcd[3]   (PRIMARY キーの一部)
    PartNumber           NVARCHAR(15)  NULL,        -- 【C原典】hinban[15]   (ALTERNATE キー1, C原典で NULL あり)
    PartName             NVARCHAR(25)  NULL,        -- 【C原典】hinmei[25]   (ALTERNATE キー2)
    ElectricalParameters NVARCHAR(64)  NULL,        -- 【C原典】pstring[64]
    -- 【C原典】PRIMARY キー = pkey (yoyaku + mkcd)。C原典に忠実に主キーへ採用する。
    CONSTRAINT PK_EquipmentMaster PRIMARY KEY (ReservedWord, MakerCode)
);
GO

-- 【C原典】ALTERNATE キー1 = hinban (品番)。C原典で NULL があるため、
-- NULL を除外したフィルタ付き一意インデックスで「非NULLの品番は一意」を担保する。
CREATE UNIQUE INDEX UX_EquipmentMaster_PartNumber
    ON dbo.EquipmentMaster (PartNumber)
    WHERE PartNumber IS NOT NULL;
GO

/* ---------------------------------------------------------------------------
   物件情報
   【C原典】struct FYDF801 (物件情報ファイル, EWS-ISAM, レコード長 1200)
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.ProjectInfo', 'U') IS NOT NULL
    DROP TABLE dbo.ProjectInfo;
GO
CREATE TABLE dbo.ProjectInfo
(
    SalesOfficeCode     NVARCHAR(2)  NOT NULL,   -- 【C原典】key.im.eigyocd[2]  (第1キーの一部)
    ItemNumber          NVARCHAR(2)  NOT NULL,   -- 【C原典】key.meisaino[2]    (第2キー)
    DrawingNumberUpper  NVARCHAR(10) NULL,        -- 【C原典】zubanu10[10]
    DrawingNumberLower  NVARCHAR(5)  NULL,        -- 【C原典】zubanl5[5]
    DataStatus          NCHAR(1)     NULL,        -- 【C原典】datastat
    ManagementKind      NCHAR(1)     NULL,        -- 【C原典】datbukbn
    RegistrationKind    NCHAR(1)     NULL,        -- 【C原典】datzukbn
    NewRequestNumber    NVARCHAR(7)  NULL,        -- 【C原典】rk.airaino[7]
    NewItemNumber       NVARCHAR(2)  NULL,        -- 【C原典】rk.ameisano[2]
    AutoDrawingKind     NCHAR(1)     NULL,        -- 【C原典】autokbn
    CONSTRAINT PK_ProjectInfo PRIMARY KEY (SalesOfficeCode, ItemNumber)
);
GO

/* ---------------------------------------------------------------------------
   回路内容記述
   【C原典】struct FYDF805 (回路内容記述ファイル, EWS-ISAM)
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.CircuitDescription', 'U') IS NOT NULL
    DROP TABLE dbo.CircuitDescription;
GO
CREATE TABLE dbo.CircuitDescription
(
    RequestNumber       NVARCHAR(7)   NOT NULL,   -- 【C原典】key.im.airaino[7]
    ItemNumber          NVARCHAR(2)   NOT NULL,   -- 【C原典】key.im.ameisano[2]
    LineNumber          INT           NOT NULL,   -- 【C原典】key.gyono[3]
    LineType            NVARCHAR(5)   NULL,        -- 【C原典】gyosyu[5]
    CircuitText         NVARCHAR(200) NULL,        -- 【C原典】kairoar[KAIROARLEN=200]
    OriginalLineNumber  INT           NULL,        -- 【C原典】orgno[3]
    Command             NCHAR(1)      NULL,        -- 【C原典】cmd
    CONSTRAINT PK_CircuitDescription PRIMARY KEY (RequestNumber, ItemNumber, LineNumber)
);
GO

/* ---------------------------------------------------------------------------
   部門(営業所/支店)マスター
   【C原典】bumon.*.cns (確認用/bumon.gai17.cns, Shift-JIS テキストマスタ)
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.DepartmentMaster', 'U') IS NOT NULL
    DROP TABLE dbo.DepartmentMaster;
GO
CREATE TABLE dbo.DepartmentMaster
(
    DepartmentCode  NVARCHAR(8)  NOT NULL,   -- 【C原典】bumon.cns 1列目
    DepartmentName  NVARCHAR(40) NULL,        -- 【C原典】bumon.cns 2列目
    PhoneNumber     NVARCHAR(20) NULL,        -- 【C原典】bumon.cns 3列目
    CONSTRAINT PK_DepartmentMaster PRIMARY KEY (DepartmentCode)
);
GO

/* ---------------------------------------------------------------------------
   データファイル構成レジストリ
   【C原典】TOKUD/datafile.inf (ﾌｧｲﾙID, ﾌｧｲﾙﾊﾟｽID, ﾌｧｲﾙ名, ﾌｧｲﾙ名称)
            旧 FyGetFileName/FyGetFilePath のパス解決を本テーブルへ移送。
   --------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.DataFileRegistry', 'U') IS NOT NULL
    DROP TABLE dbo.DataFileRegistry;
GO
CREATE TABLE dbo.DataFileRegistry
(
    FileId       NVARCHAR(16)  NOT NULL,   -- 【C原典】datafile.inf 1列目 (例 FYDM805)
    PathId       NVARCHAR(16)  NULL,        -- 【C原典】datafile.inf 2列目 (例 MSTCL)
    FileName     NVARCHAR(64)  NULL,        -- 【C原典】datafile.inf 3列目
    DisplayName  NVARCHAR(64)  NULL,        -- 【C原典】datafile.inf 4列目 (ﾌｧｲﾙ名称)
    TableName    NVARCHAR(128) NULL,        -- 新設: 論理ファイルID→SQL テーブル名の対応
    CONSTRAINT PK_DataFileRegistry PRIMARY KEY (FileId)
);
GO
