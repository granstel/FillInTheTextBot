using FillInTheTextBot.Api.Middleware;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using FillInTheTextBot.Api.DI;
using Prometheus;

namespace FillInTheTextBot.Api
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            InternalLoggerFactory.Factory = loggerFactory;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // ReSharper disable once UnusedMember.Global
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddMvc()
                .AddNewtonsoftJson();

            services.AddOpenTracing();
            services.AddHttpLogging(o =>
            {
                o.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
            });

            services.AddAppConfiguration(_configuration);
            services.AddInternalServices();
            services.AddExternalServices();
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, AppConfiguration configuration)
        {
            app.UseMiddleware<ExceptionsMiddleware>();

            app.UseRouting();
            app.UseHttpMetrics();
            app.UseGrpcMetrics();

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
                e.MapMetrics();
            });
        }
    }
}
