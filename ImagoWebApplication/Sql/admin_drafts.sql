-- ImagoAdmin: черновики, предпросмотр и история публикаций. Скрипт можно запускать повторно.

-- Настройки сайта (ключ предпросмотра черновика: сайт показывает черновик только с ?nahled=<ключ>)
IF OBJECT_ID(N'[dbo].[SiteSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SiteSettings] (
        [Key]    NVARCHAR(100) NOT NULL,
        [Value]  NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_SiteSettings] PRIMARY KEY CLUSTERED ([Key])
    );
END
GO

-- Черновик стилей текста (как EditingDictionaryEntries для текстов): на сайт попадает только по «Publikovat»
IF OBJECT_ID(N'[dbo].[EditingTextStyles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[EditingTextStyles] (
        [Id]             INT IDENTITY(1,1) NOT NULL,
        [EntryKey]       NVARCHAR(255) NOT NULL,
        [FontFamily]     NVARCHAR(100) NULL,
        [FontSize]       NVARCHAR(50)  NULL,
        [FontWeight]     NVARCHAR(50)  NULL,
        [FontStyle]      NVARCHAR(50)  NULL,
        [TextDecoration] NVARCHAR(50)  NULL,
        [TextColor]      NVARCHAR(50)  NULL,
        [TextAlignment]  NVARCHAR(50)  NULL,
        CONSTRAINT [PK_EditingTextStyles] PRIMARY KEY CLUSTERED ([Id])
    );
    CREATE INDEX [IX_EditingTextStyles_EntryKey] ON [dbo].[EditingTextStyles] ([EntryKey]);

    INSERT INTO [dbo].[EditingTextStyles] (EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor, TextAlignment)
    SELECT EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor, TextAlignment FROM [dbo].[TextStyles];
END
GO

-- История публикací: снимок опубликованных текстов и стилей страницы ДО каждой публикации (для «Vrátit verzi»)
IF OBJECT_ID(N'[dbo].[PublishHistory]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PublishHistory] (
        [Id]          INT IDENTITY(1,1) NOT NULL,
        [PageId]      INT           NOT NULL,
        [PublishedAt] DATETIME      NOT NULL,
        [PublishedBy] NVARCHAR(100) NULL,
        [Texts]       NVARCHAR(MAX) NOT NULL,   -- JSON: [{ "k": EntryKey, "t": ContentText }]
        [Styles]      NVARCHAR(MAX) NULL,       -- JSON: стили этих текстов
        CONSTRAINT [PK_PublishHistory] PRIMARY KEY CLUSTERED ([Id])
    );
    CREATE INDEX [IX_PublishHistory_Page] ON [dbo].[PublishHistory] ([PageId], [PublishedAt]);
END
GO
