using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(FillInTheTextBot.Messengers.Sber.SberStartup))]
namespace FillInTheTextBot.Messengers.Sber
{
    public class SberStartup : IHostingStartup
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
