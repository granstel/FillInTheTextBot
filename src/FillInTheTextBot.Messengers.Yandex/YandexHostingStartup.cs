using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(FillInTheTextBot.Messengers.Yandex.YandexHostingStartup))]
namespace FillInTheTextBot.Messengers.Yandex
{
    public class YandexHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services => services.AddYandexServices());
        }
    }
}
