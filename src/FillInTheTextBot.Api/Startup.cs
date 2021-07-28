﻿using FillInTheTextBot.Api.Middleware;
using FillInTheTextBot.Services.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // ReSharper disable once UnusedMember.Global
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddMvc()
                .AddNewtonsoftJson();

            services.AddOpenTracing();

            DependencyConfiguration.Configure(services, _configuration);
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, AppConfiguration configuration)
        {
            app.UseMiddleware<ExceptionsMiddleware>();

            app.UseRouting();

            if (configuration.HttpLog.Enabled)
            {
                app.UseMiddleware<HttpLogMiddleware>();
            }

            app.UseEndpoints(e => e.MapControllers());
        }
    }
}
