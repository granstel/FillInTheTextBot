using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(FillInTheTextBot.Messengers.Marusia.MarusiaHostingStartup))]
namespace FillInTheTextBot.Messengers.Marusia
{
    public class MarusiaHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddConfiguration<MarusiaConfiguration>("appsettings.Marusia.json");

                services.AddTransient<IMarusiaService, MarusiaService>();
            });
        }
    }
}
