using ImagoLib.Models;
using ImagoWebApplication.Controllers;
using ImagoWebApplication.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

public class HomeController : BaseController {
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger) : base() {
        _logger = logger;

    }

    private void SetViewHomeBagEntries() {
        var entries = DictionaryEntryForText.GetAllEntries();
        ViewBag.Entries = entries.ToDictionary(e => e.EntryKey, e => e.ContentText);

    }
    private void SetViewHomeBagStyles() {
        var styles = TextStyle.GetAllStyles();
        ViewBag.TextStyles = styles.ToDictionary(s => s.EntryKey, s => s);
    }

    private void SetViewHomeBagImages() {
        var images = DictionaryEntryForImages.GetAllEntries()
            .Select(img => new Dictionary<string, string> {
            { "EntryKey", img.EntryKey },
            { "Base64", Convert.ToBase64String(img.ImageData) }
            })
            .ToList();

        ViewBag.Images = images;
    }


    public IActionResult Index() {
        SetViewHomeBagEntries();
        SetViewHomeBagImages();
        SetViewHomeBagStyles();
        return View();
    }

    public IActionResult Privacy() {
        SetViewHomeBagEntries();
        return View();
    }

    public IActionResult GDPR() {
        SetViewHomeBagEntries();
        SetViewHomeBagStyles();
        return View();
    }

    public IActionResult Marketing() {
        SetViewHomeBagEntries();
        SetViewHomeBagStyles();

        return View();
    }

    public IActionResult Příslušenství() {
        SetViewHomeBagEntries();
        return View();
    }

    public IActionResult Školení() {
        SetViewHomeBagEntries();
        SetViewHomeBagStyles();

        return View();
    }

    public IActionResult Vyhody() {
        SetViewHomeBagEntries();
        return View();
    }

    public IActionResult PracovniAktiv() {
        SetViewHomeBagEntries();
        SetViewHomeBagStyles();

        return View();
    }

    public IActionResult PageDCW() {
        SetViewHomeBagEntries();
        SetViewHomeBagStyles();

        return View();
    }

    public IActionResult AKTUALNE() {
        SetViewHomeBagEntries();
        return View();
    }

    public IActionResult diacomClub() {
        SetViewHomeBagEntries();
        return View();
    }

    public IActionResult pristrojeDiacom() {
        SetViewHomeBagEntries();
        return View();
    }

    public IActionResult AboutUs() {
        SetViewHomeBagEntries();
        return View();
    }

    public IActionResult Prozovna() {
        SetViewHomeBagEntries();
        SetViewHomeBagImages();
        SetViewHomeBagStyles();

        return View();
    }

    public IActionResult Mitink() {
        var meetings = Meeting.GetMeetings().OrderByDescending(m => m.Id).ToList();

        foreach (var meeting in meetings) {
            meeting.Photos = MeetingPhoto.GetPhotosForMeeting(meeting.Id);
        }

        SetViewHomeBagEntries();
        ViewBag.Meetings = meetings;
        return View();
    }

    public IActionResult Novinky(string period) {
        SetViewHomeBagEntries();

        DateTime? startDate = null;
        DateTime? endDate = null;

        if (!string.IsNullOrEmpty(period)) {
            var dates = period.Split(" to ");
            startDate = DateTime.Parse(dates[0]);
            endDate = dates.Length > 1 ? DateTime.Parse(dates[1]) : startDate;
        }

        var novinkyList = Noviny.GetNoviny(startDate, endDate).OrderByDescending(m => m.Id).ToList();

        foreach (var item in novinkyList) {
            item.Photos = NovinyFoto.GetPhotosForRequest(item.Id);
        }

        ViewBag.Novinky = novinkyList;
        return View();
    }

    public IActionResult Kontakty() {
        SetViewHomeBagEntries();
        return View();
    }
    
    public IActionResult ProductDetails(int id) {
        var novinka = Noviny.GetNoviny().FirstOrDefault(n => n.Id == id);

        if (novinka != null) {
            novinka.Photos = NovinyFoto.GetPhotosForRequest(novinka.Id);
            novinka.Parameters = NovinyParameter.GetParametersForNoviny(novinka.Id);
        }

        ViewBag.NovinkaDetails = novinka;
        return View();
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}