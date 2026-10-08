-- Черновики mítinků и novinek для ImagoAdmin (как EditingDictionaryEntries для текстов). Скрипт можно запускать повторно.
-- Админка правит Editing*-таблицы, сайт показывает основные; «Publikovat stránku» (Mítink / Novinky) переносит черновик на сайт
-- с теми же Id (ссылки /Home/ProductDetails/{id} не меняются). Создаются копией текущих данных.

IF OBJECT_ID(N'[dbo].[EditingMeetings]', N'U') IS NULL
BEGIN
    SELECT * INTO [dbo].[EditingMeetings] FROM [dbo].[Meetings];
    ALTER TABLE [dbo].[EditingMeetings] ADD CONSTRAINT [PK_EditingMeetings] PRIMARY KEY CLUSTERED ([Id]);
    ALTER TABLE [dbo].[EditingMeetings] ADD CONSTRAINT [DF_EditingMeetings_CreatedAt] DEFAULT (GETDATE()) FOR [CreatedAt];
    ALTER TABLE [dbo].[EditingMeetings] ADD CONSTRAINT [DF_EditingMeetings_UpdatedAt] DEFAULT (GETDATE()) FOR [UpdatedAt];
END
GO
IF OBJECT_ID(N'[dbo].[EditingMeetingPhotos]', N'U') IS NULL
BEGIN
    SELECT * INTO [dbo].[EditingMeetingPhotos] FROM [dbo].[MeetingPhotos];
    ALTER TABLE [dbo].[EditingMeetingPhotos] ADD CONSTRAINT [PK_EditingMeetingPhotos] PRIMARY KEY CLUSTERED ([Id]);
    CREATE INDEX [IX_EditingMeetingPhotos_Meeting] ON [dbo].[EditingMeetingPhotos] ([MeetingId]);
END
GO
IF OBJECT_ID(N'[dbo].[EditingNoviny]', N'U') IS NULL
BEGIN
    SELECT * INTO [dbo].[EditingNoviny] FROM [dbo].[Noviny];
    ALTER TABLE [dbo].[EditingNoviny] ADD CONSTRAINT [PK_EditingNoviny] PRIMARY KEY CLUSTERED ([Id]);
    ALTER TABLE [dbo].[EditingNoviny] ADD CONSTRAINT [DF_EditingNoviny_PostedDate] DEFAULT (GETDATE()) FOR [PostedDate];
END
GO
IF OBJECT_ID(N'[dbo].[EditingNovinyPhotos]', N'U') IS NULL
BEGIN
    SELECT * INTO [dbo].[EditingNovinyPhotos] FROM [dbo].[NovinyPhotos];
    ALTER TABLE [dbo].[EditingNovinyPhotos] ADD CONSTRAINT [PK_EditingNovinyPhotos] PRIMARY KEY CLUSTERED ([Id]);
    CREATE INDEX [IX_EditingNovinyPhotos_Noviny] ON [dbo].[EditingNovinyPhotos] ([NovinyId]);
END
GO
IF OBJECT_ID(N'[dbo].[EditingNovinyParameters]', N'U') IS NULL
BEGIN
    SELECT * INTO [dbo].[EditingNovinyParameters] FROM [dbo].[NovinyParameters];
    ALTER TABLE [dbo].[EditingNovinyParameters] ADD CONSTRAINT [PK_EditingNovinyParameters] PRIMARY KEY CLUSTERED ([Id]);
    CREATE INDEX [IX_EditingNovinyParameters_Noviny] ON [dbo].[EditingNovinyParameters] ([NovinyId]);
END
GO

-- Новые записи черновика получают номера выше, чем на сайте (чтобы при публикации Id не пересеклись)
DECLARE @t NVARCHAR(100), @main NVARCHAR(100), @max INT, @sql NVARCHAR(400);
DECLARE c CURSOR LOCAL FOR SELECT e, m FROM (VALUES
    (N'EditingMeetings', N'Meetings'), (N'EditingMeetingPhotos', N'MeetingPhotos'), (N'EditingNoviny', N'Noviny'),
    (N'EditingNovinyPhotos', N'NovinyPhotos'), (N'EditingNovinyParameters', N'NovinyParameters')) v(e, m);
OPEN c; FETCH NEXT FROM c INTO @t, @main;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @max = IDENT_CURRENT(@main);
    IF IDENT_CURRENT(@t) < @max BEGIN SET @sql = N'DBCC CHECKIDENT (''' + @t + N''', RESEED, ' + CAST(@max AS NVARCHAR(20)) + N') WITH NO_INFOMSGS'; EXEC (@sql); END
    FETCH NEXT FROM c INTO @t, @main;
END
CLOSE c; DEALLOCATE c;
GO
