using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(FillInTheTextBot.Messengers.Yandex.HostingStartup))]
namespace FillInTheTextBot.Messengers.Yandex
{
    public class HostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services => services.AddYandexServices());
        }
    }
}
