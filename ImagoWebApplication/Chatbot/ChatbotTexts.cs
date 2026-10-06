namespace ImagoWebApplication.Chatbot {

    /// <summary>
    /// Тексты виджета чата, служебные сообщения и письма — на чешском (язык сайта).
    /// </summary>
    public static class ChatbotTexts {

        private static readonly Dictionary<string, string> Texts = new Dictionary<string, string> {
            { "Chatbot_Title", "IMAGO AI asistent" },
            { "Chatbot_Greeting", "Dobrý den! Jsem virtuální asistent společnosti IMAGO D&T. Pomohu Vám s informacemi o přístrojích DIACOM, cenách, školení, mítincích a kontaktech, a když bude potřeba, předám Váš dotaz manažerovi IMAGO D&T." },
            { "Chatbot_Placeholder", "Napište svůj dotaz…" },
            { "Chatbot_Send", "Odeslat" },
            { "Chatbot_Open", "Otevřít chat" },
            { "Chatbot_AskMe", "Zeptejte se mě" },
            { "Chatbot_Close", "Zavřít" },
            { "Chatbot_NewChat", "Nový chat" },
            { "Chatbot_Disclaimer", "Odpovědi generuje AI na základě materiálů IMAGO D&T a DIACOM. Konverzace se uchovává 7 dní." },
            { "Chatbot_Typing", "Asistent píše…" },
            { "Chatbot_ContactTitle", "Zanechte kontakt a manažer IMAGO D&T se Vám ozve:" },
            { "Chatbot_Name", "Jméno" },
            { "Chatbot_Contact", "Telefon nebo e-mail" },
            { "Chatbot_ContactSend", "Odeslat kontakt" },
            { "Chatbot_ContactConsent", "Odesláním kontaktu souhlasíte s jeho zpracováním za účelem odpovědi na Váš dotaz. Konverzace se uchovává 7 dní." },
            { "Chatbot_ContactThanks", "Děkujeme! Váš dotaz byl předán manažerovi IMAGO D&T. Odpověď se objeví zde v chatu, a pokud jste uvedli e-mail, pošleme ji i tam." },
            { "Chatbot_ContactInvalid", "Zadejte prosím jméno a platný telefon nebo e-mail." },
            { "Chatbot_ServiceError", "Omlouváme se, teď nemohu odpovědět. Zanechte nám prosím kontakt, ozveme se Vám." },
            { "Chatbot_TooManyMessages", "Konverzace je příliš dlouhá. Zanechte nám kontakt nebo začněte nový chat." },
            { "Chatbot_ConfirmTitle", "Předat Váš dotaz manažerovi IMAGO D&T? Můžete se nejdřív zeptat na další věci — pošlu vše jednou zprávou." },
            { "Chatbot_ConfirmSend", "Předat dotaz" },
            { "Chatbot_NothingToSend", "Žádné nové dotazy k předání nejsou." },
            { "Chatbot_StaffLabel", "Odpověď manažera IMAGO D&T" },
            { "Chatbot_NewAnswer", "Nová odpověď na Váš dotaz" },
            { "Chatbot_RateLimited", "Příliš mnoho zpráv. Počkejte prosím minutu a zkuste to znovu." },
            { "Chatbot_EmailSubject", "IMAGO D&T: odpověď na Váš dotaz" },
            { "Chatbot_EmailGreeting", "Dobrý den" },
            { "Chatbot_EmailYourQuestion", "Váš dotaz v chatu na webu imagodt.cz" },
            { "Chatbot_EmailSignature", "S pozdravem," },
        };

        public static IEnumerable<string> Keys => Texts.Keys;

        public static string Get(string key) => Texts.TryGetValue(key, out var t) ? t : key;
    }
}
