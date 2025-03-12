using ImagoLib.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.ObjectModel;

public abstract class BaseController : Controller {
    public override void OnActionExecuting(ActionExecutingContext context) {
        base.OnActionExecuting(context);

        // Загружаем данные (например, страницы для меню)
        var pages = Pages.GetPagesHierarchy();

        // Передаем данные в ViewBag
        ViewBag.Pages = pages;

        // Логируем данные для отладки
        System.Diagnostics.Debug.WriteLine($"Загружено страниц: {pages.Count}");
        foreach (var page in pages) {
            System.Diagnostics.Debug.WriteLine($"Страница: {page.Title}, URL: {page.Url}, Дочерних страниц: {page.SubPages?.Count ?? 0}");
        }
    }
}