using FillInTheTextBot.Api.Middleware;
using FillInTheTextBot.Services.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using FillInTheTextBot.Api.DI;
using FillInTheTextBot.Api.Health;
using Microsoft.Extensions.Hosting;

namespace FillInTheTextBot.Api
{
    public class Startup
    {
        /// <summary>
        /// Путь проверки здоровья. По нему ходит балансировщик, чтобы понимать,
        /// можно ли слать на экземпляр трафик.
        /// </summary>
        public const string HealthPath = "/health";

        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // ReSharper disable once UnusedMember.Global
        public void ConfigureServices(IServiceCollection services)
        {
            var appConfiguration = _configuration.GetSection(nameof(AppConfiguration)).Get<AppConfiguration>();

            services
                .AddMvc()
                .AddNewtonsoftJson();

            services.AddTelemetry(appConfiguration.Tracing);

            services.AddHttpLogging(o =>
            {
                o.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
            });

            services.AddAppConfiguration(appConfiguration);

            AddHealth(services);

            services.AddInternalServices();
            services.AddExternalServices();
        }

        private static void AddHealth(IServiceCollection services)
        {
            services.AddSingleton<ReadinessState>();
            services.AddHostedService<GracefulShutdownService>();

            services.AddHealthChecks()
                .AddCheck<ReadinessHealthCheck>("readiness");

            // Хост должен дождаться и текущих запросов, и разбора очереди фоновых работ
            services.AddOptions<HostOptions>()
                .Configure<ShutdownConfiguration>((options, shutdown) =>
                    options.ShutdownTimeout = TimeSpan.FromSeconds(shutdown.TimeoutSeconds));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, AppConfiguration configuration)
        {
            app.UseMiddleware<ExceptionsMiddleware>();

            app.UseRouting();

            if (configuration.HttpLog.Enabled)
            {
                app.UseWhen(context => configuration.HttpLog.IncludeEndpoints.Any(w =>
                    context.Request.Path.Value.Contains(w, StringComparison.InvariantCultureIgnoreCase)), a =>
                    {
                        a.UseHttpLogging();
                    });
            }

            app.UseEndpoints(e =>
            {
                e.MapControllers();
                e.MapPrometheusScrapingEndpoint();
                e.MapHealthChecks(HealthPath);
            });
        }
    }
}
