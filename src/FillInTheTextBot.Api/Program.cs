using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NLog.Web;

namespace FillInTheTextBot.Api
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost
            .CreateDefaultBuilder(args)
            .UseSetting(WebHostDefaults.ApplicationKey, "HostingStartupApp")
            .UseSetting(
                    WebHostDefaults.PreventHostingStartupKey, "false")
                .UseStartup<Startup>()
                .UseNLog()
                .Build();
    }
}
