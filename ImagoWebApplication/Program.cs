var builder = WebApplication.CreateBuilder(args);

// Ключи чат-бота (OpenAI, Telegram, SMTP): файл публикуется вместе с сайтом, но не попадает в git (.gitignore)
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSession();
builder.Services.AddMemoryCache();

// Чат-бот: приём обновлений Telegram (ответы менеджеров), включается "Telegram:ReceiveUpdates": true + "Telegram:BotToken"
builder.Services.AddHostedService<ImagoWebApplication.Chatbot.TelegramPollingService>();

var app = builder.Build();

app.UseDeveloperExceptionPage();

app.UseSession();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
