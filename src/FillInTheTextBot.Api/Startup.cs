using System;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FillInTheTextBot.Api.Middleware;
using FillInTheTextBot.Services.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api
{
    public class Startup
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        private IContainer _applicationContainer;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // ReSharper disable once UnusedMember.Global
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services
                .AddMvc()
                .AddNewtonsoftJson();

            services.AddOpenTracing();

            _applicationContainer = DependencyConfiguration.Configure(services, _configuration);

            return new AutofacServiceProvider(_applicationContainer);
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, AppConfiguration configuration)
        {
            app.UseMiddleware<MetricsMiddleware>();
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
