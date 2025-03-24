using ImagoLib.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.ObjectModel;
using Microsoft.Data.SqlClient;

public abstract class BaseController : Controller {
    public override void OnActionExecuting(ActionExecutingContext context) {
        base.OnActionExecuting(context);

        var pages = Pages.GetPagesHierarchy();
        ViewBag.Pages = pages;

        var globalEntries = DictionaryEntryForText.GetEntriesForPage(3); 
        ViewBag.GlobalEntries = globalEntries.ToDictionary(e => e.EntryKey, e => e.ContentText);
    }
}