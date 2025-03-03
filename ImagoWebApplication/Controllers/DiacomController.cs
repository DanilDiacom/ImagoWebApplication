using Microsoft.AspNetCore.Mvc;
using ImagoLib.Models;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Linq;
using ImagoWebApplication.Models;

namespace ImagoWebApplication.Controllers {
    public class DiacomController : Controller {
        private readonly ILogger<DiacomController> _logger;

        public DiacomController(ILogger<DiacomController> logger) {
            _logger = logger;
        }

        private void SetViewBagEntries() {
            var entries = DictionaryEntryForText.GetAllEntries();
            ViewBag.Entries = entries.ToDictionary(e => e.EntryKey, e => e.ContentText);
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

        public IActionResult Enerscan() {
            SetViewBagEntries();
            SetViewHomeBagImages();
            return View();
        }

        public IActionResult Plasmotronic() {
            SetViewBagEntries();
            SetViewHomeBagImages();
            return View();
        }

        public IActionResult Medio() {
            SetViewBagEntries();
            SetViewHomeBagImages();
            return View();
        }

        public IActionResult LiteFreq() {
            SetViewBagEntries();
            SetViewHomeBagImages();
            return View();
        }

        public IActionResult FreqPc() {
            SetViewBagEntries();
            SetViewHomeBagImages();
            return View();
        }

        public IActionResult Ioniser() {
            SetViewBagEntries();
            SetViewHomeBagImages();
            return View();
        }

        public IActionResult ProductPrice() {
            SetViewBagEntries();
            return View();
        }

        public IActionResult Navody() {
            SetViewBagEntries();
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}