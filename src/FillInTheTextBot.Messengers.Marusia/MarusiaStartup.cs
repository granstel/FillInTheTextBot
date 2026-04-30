using FillInTheTextBot.Messengers.Marusia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(MarusiaStartup))]

namespace FillInTheTextBot.Messengers.Marusia;

public class MarusiaStartup : IHostingStartup
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