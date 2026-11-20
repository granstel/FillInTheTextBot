using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Web;

namespace FillInTheTextBot.Api
{
    // Не static: тип используется как параметр WebApplicationFactory в интеграционных тестах
    public class Program
    {
        private Program()
        {
        }

        public static void Main(string[] args)
        {
            var app = BuildApplication(args);

            app.Run();
        }

        public static WebApplication BuildApplication(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.ClearProviders();
            builder.Host.UseNLog();

            var startup = new Startup(builder.Configuration);
            startup.ConfigureServices(builder.Services);

            var app = builder.Build();

            // Статические мапперы берут логгер отсюда, поэтому фабрику нужно выставить
            // до того, как приложение начнёт принимать запросы
            InternalLoggerFactory.Factory = app.Services.GetRequiredService<ILoggerFactory>();

            var configuration = app.Services.GetRequiredService<AppConfiguration>();
            startup.Configure(app, configuration);

            return app;
        }
    }
}
