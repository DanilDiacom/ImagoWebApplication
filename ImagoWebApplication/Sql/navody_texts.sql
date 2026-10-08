-- Тексты страницы «Návody k přístrojům» (Pages.id = 46) для редактирования в ImagoAdmin.
-- Перенесены из прежней статичной разметки (Views/Diacom/Navody.cshtml, DeviceDiacom.cshtml). Скрипт можно запускать повторно:
-- добавляет только отсутствующие тексты, существующие не трогает.
-- Видео — блоки Navody_Video{N}_* (Title, Url, Description); добавляются и удаляются в ImagoAdmin, вкладка «Bloky».

DECLARE @texts TABLE (EntryKey NVARCHAR(200) NOT NULL, ContentText NVARCHAR(MAX) NOT NULL);
INSERT INTO @texts (EntryKey, ContentText) VALUES
    (N'Navody_Title', N'Návody k přístrojům'),
    (N'Navody_Intro', N'Připravili jsme pro Vás instruktážní videa s podrobným návodem k jednotlivým přístrojům.'),
    (N'Navody_Video1_Title', N'DIACOM-SOLO-Ionizer'),
    (N'Navody_Video1_Url', N'https://www.youtube.com/watch?v=w5F7zgRqVs0'),
    (N'Navody_Video1_Description', N'');

INSERT INTO DictionaryEntries (PageId, EntryKey, ContentText)
SELECT 46, t.EntryKey, t.ContentText FROM @texts t
WHERE NOT EXISTS (SELECT 1 FROM DictionaryEntries d WHERE d.PageId = 46 AND d.EntryKey = t.EntryKey);

INSERT INTO EditingDictionaryEntries (PageId, EntryKey, ContentText)
SELECT 46, t.EntryKey, t.ContentText FROM @texts t
WHERE NOT EXISTS (SELECT 1 FROM EditingDictionaryEntries e WHERE e.PageId = 46 AND e.EntryKey = t.EntryKey);
