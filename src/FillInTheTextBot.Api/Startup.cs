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
        
        private IContainer _applicationContainer;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // ReSharper disable once UnusedMember.Global
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            _applicationContainer = DependencyConfiguration.Configure(services, _configuration);

            return new AutofacServiceProvider(_applicationContainer);
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, AppConfiguration configuration)
        {
            app.UseMiddleware<MetricsMiddleware>();

            if (configuration.HttpLog.Enabled)
            {
                app.UseMiddleware<HttpLogMiddleware>();
            }

            app.UseMiddleware<ExceptionsMiddleware>();

            app.UseMvc();
        }
    }
}
