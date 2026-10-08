-- Меню сайта из ImagoAdmin: порядок пунктов и скрытие страниц. Скрипт можно запускать повторно.

IF COL_LENGTH('dbo.Pages', 'SortOrder') IS NULL
    ALTER TABLE [dbo].[Pages] ADD [SortOrder] INT NULL;               -- порядок в меню (NULL — по id, как раньше)
GO
IF COL_LENGTH('dbo.Pages', 'IsHidden') IS NULL
BEGIN
    ALTER TABLE [dbo].[Pages] ADD [IsHidden] BIT NOT NULL CONSTRAINT [DF_Pages_IsHidden] DEFAULT (0);   -- скрыта в меню (страница по адресу доступна)
END
GO
-- «CENY PRISTROJU DIACOM» раньше была скрыта прямо в коде сайта (if (subPage.Id == 45) continue;) — теперь флагом
UPDATE [dbo].[Pages] SET [IsHidden] = 1 WHERE [Id] = 45 AND [IsHidden] = 0;
-- текущий порядок (по id) — начальные номера
UPDATE p SET p.[SortOrder] = x.rn
FROM [dbo].[Pages] p
JOIN (SELECT Id, ROW_NUMBER() OVER (PARTITION BY ISNULL(ParentId, 0) ORDER BY Id) AS rn FROM [dbo].[Pages]) x ON x.Id = p.Id
WHERE p.[SortOrder] IS NULL;
GO
