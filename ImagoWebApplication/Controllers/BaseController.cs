using ImagoLib.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.ObjectModel;
using Microsoft.Data.SqlClient;

public abstract class BaseController : Controller {

    // Ключ предпросмотра черновика (SiteSettings.PreviewToken) — читается из базы, кэш 5 минут
    private static string? _previewToken;
    private static DateTime _previewTokenLoaded = DateTime.MinValue;

    /// <summary>
    /// Предпросмотр черновика для ImagoAdmin: адрес страницы с ?nahled=&lt;ключ&gt; показывает неопубликованные тексты, фото и стили.
    /// Без ключа (обычные посетители) — всегда опубликованная версия.
    /// </summary>
    protected bool IsDraft { get; private set; }

    public override void OnActionExecuting(ActionExecutingContext context) {
        base.OnActionExecuting(context);

        IsDraft = IsValidPreview(Request.Query["nahled"]);
        ViewBag.IsDraft = IsDraft;
        if (IsDraft) {
            // черновик не кэшируется и не индексируется
            Response.Headers["Cache-Control"] = "no-store";
            Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        }

        var pages = Pages.GetPagesHierarchy();
        ViewBag.Pages = pages;

        var globalEntries = DictionaryEntryForText.GetEntriesForPage(3, IsDraft);
        ViewBag.GlobalEntries = HtmlLite.ToSafeDictionary(globalEntries);
    }

    private static bool IsValidPreview(string? value) {
        if (string.IsNullOrEmpty(value)) return false;
        try {
            if (_previewToken == null || DateTime.Now - _previewTokenLoaded > TimeSpan.FromMinutes(5)) {
                _previewToken = SiteSettings.Get("PreviewToken");
                _previewTokenLoaded = DateTime.Now;
            }
            return !string.IsNullOrEmpty(_previewToken)
                   && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                          System.Text.Encoding.UTF8.GetBytes(value), System.Text.Encoding.UTF8.GetBytes(_previewToken));
        }
        catch {
            return false;
        }
    }
}
