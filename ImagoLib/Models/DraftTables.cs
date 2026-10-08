namespace ImagoLib.Models {

    /// <summary>
    /// Черновик mítinků a novinek: таблицы Editing* (EditingMeetings, EditingNoviny…) с теми же Id.
    /// ImagoAdmin работает с черновиком (draft: true), сайт — с опубликованными таблицами; перенос — PageDraft.Publish.
    /// </summary>
    public static class DraftTables {
        public static string Name(string table, bool draft) => draft ? "Editing" + table : table;
    }
}
