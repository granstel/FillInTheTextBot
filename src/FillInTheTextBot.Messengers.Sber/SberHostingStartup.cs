using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(FillInTheTextBot.Messengers.Sber.SberHostingStartup))]
namespace FillInTheTextBot.Messengers.Sber
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
