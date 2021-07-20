using FillInTheTextBot.Messengers.Sber;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(FillInTheTextBot.Messengers.Yandex.SberHostingStartup))]
namespace FillInTheTextBot.Messengers.Yandex
{
    public class SberHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddConfiguration<SberConfiguration>("appsettings.Sber.json");

                services.AddTransient<ISberService, SberService>();
            });
        }
    }
}
