using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(FillInTheTextBot.Messengers.Yandex.YandexHostingStartup))]
namespace FillInTheTextBot.Messengers.Yandex
{
    public class YandexHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddConfiguration<YandexConfiguration>("appsettings.Yandex.json");

                services.AddTransient<IYandexService, YandexService>();
            });
        }
    }
}
