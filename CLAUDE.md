# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

A CMS-style system for the Czech site **imagodt.cz** (Diacom devices). It has three .NET 6 projects in `ImagoWebApplication.sln`:

- **ImagoWebApplication**: ASP.NET Core MVC public website. It renders pages whose text, images and text styles come from SQL Server.
- **ImagoAdmin**: a WPF desktop editor (`net6.0-windows`, x64, WebView2). It loads the live site, lets an admin edit the text and styles behind each element, and writes the changes back to the same database.
- **ImagoLib**: shared data-access library used by both apps. Each model class has static ADO.NET methods.

The solution also contains installer projects (`ImagoInstaller` WiX, `ImagoAdminInstaller` .vdproj). `ImagoMSI`, `SetupProject1` and `Installer` are other installer projects that are not in the solution, and `ImagoAdmin — копия` is a stale copy of the admin app. The vendored package folders at the repo root (`squirrel.windows.*`, `Mono.Cecil.*`, `SharpCompress.*`) are not source code. Code comments are mostly in Russian, and UI and site text are in Czech.

## Commands

```bash
dotnet build ImagoWebApplication/ImagoWebApplication.csproj
dotnet run --project ImagoWebApplication          # site; profiles in Properties/launchSettings.json
dotnet build ImagoAdmin/ImagoAdmin.csproj         # Windows only
dotnet run --project ImagoAdmin
```

Building the whole `.sln` with `dotnet build` fails on the WiX/.vdproj installer projects; those need Visual Studio. Build the individual `.csproj` files instead. The repo has no test projects and no linter configuration. Client-side libraries in `wwwroot/lib` are managed with LibMan (`libman.json`).

## Architecture

### Data access (ImagoLib)
- `Db.Get()` returns an **already opened** `SqlConnection`. The connection string is hard-coded in `ImagoLib/Models/Db.cs` and points at the production SQL Server for both DEBUG and RELEASE, so running either app locally reads and writes live data.
- There is no ORM or repository layer. Every model (`DictionaryEntryForText`, `DictionaryEntryForImages`, `TextStyle`, `Pages`, `Meeting`, `Noviny`, `NovinyFoto`, `MeetingPhoto`, ...) has static methods that write raw SQL, bind parameters with `Db.SetParam` and map rows positionally in a `FromDataReader` method. A change to a SELECT column list must keep the `dr.GetXxx(i)` indexes in sync.
- Collections are returned as `ObservableCollection<T>` so the WPF admin can bind to them directly. `Pages` uses CommunityToolkit.Mvvm `[ObservableProperty]` on `m_`-prefixed fields.

### Content model
- `Pages` (`id, title, url, parentId`) is a tree. It drives the site's navigation menu and the admin's TreeView. The children of page id 5 are device pages that share one view, `Diacom/DeviceDiacom?id={pageId}`.
- `DictionaryEntries` (`PageId, EntryKey, ContentText`) holds editable text. `TextStyles` is keyed by `EntryKey` and holds font, size, weight, color and so on. Images are stored as byte arrays and passed to views as base64.
- Page id 3 holds the global entries (phone, email) shown in the layout.

### Website request flow
- Controllers inherit `BaseController`, whose `OnActionExecuting` sets `ViewBag.Pages` (the menu tree) and `ViewBag.GlobalEntries` on every request.
- Each action fills `ViewBag.Entries`, `ViewBag.TextStyles` and/or `ViewBag.Images` with helper methods. Most actions load **all** entries rather than the current page's entries, but `DeviceDiacom` filters by page id.
- Views read `ViewBag.Entries["key"]` behind a `ContainsKey` guard and write the matching `TextStyle` into inline `style` attributes. The element must carry `data-key="<EntryKey>"` (or `data-value` for spans) because the admin editor uses that attribute to find it.
- Each page has its own stylesheet in `wwwroot/css/`.
- Some action names contain Czech diacritics (`Příslušenství`, `Školení`), and the view file names match them.
- `ContactController.SendEmail` sends the contact form through MailKit over SMTP.

### Admin editing flow (ImagoAdmin/MainWindow.xaml.cs)
1. Selecting a page in the TreeView loads that page from `https://imagodt.cz{url}` into WebView2. A backup copy of the page HTML is saved to the database.
2. Selecting an entry runs injected JS that updates elements matching `[data-key=...]` or `[data-value=...]` in place, which previews text and style changes.
3. Edits are saved to the **staging** table `EditingDictionaryEntries`. **Publish** copies the staging rows into `DictionaryEntries`, which makes them live. Text styles are written directly to `TextStyles`.
4. Some page ids get special handling in code: 38 is meetings ("Mitink"), 8 is news ("Novinky"), and 5 is the parent of the device pages.

The admin checks GitHub Releases (`DanilDiacom/ImagoWebApplication`) for updates and installs an MSI. `ImagoAdmin/update.xml` is the AutoUpdater.NET manifest. Raise `<Version>`/`<AssemblyVersion>` in `ImagoAdmin.csproj` when you cut a release.
