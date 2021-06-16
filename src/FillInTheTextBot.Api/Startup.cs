using System;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FillInTheTextBot.Api.Middleware;
using FillInTheTextBot.Services.Configuration;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders.Thrift;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Util;
using Configuration = Jaeger.Configuration;

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

            services.AddSingleton<ITracer>(serviceProvider =>
            {
                var serviceName = _env.ApplicationName;


                var sampler = new ConstSampler(true);
                var reporter = new RemoteReporter.Builder()
                    .WithSender(new UdpSender("localhost", 6831, 0))
                    .Build();

                var tracer = new Tracer.Builder(serviceName)
                    .WithSampler(sampler)
                    .WithReporter(reporter)
                    .Build();

                GlobalTracer.Register(tracer);

                return tracer;
            });
            
            _applicationContainer = DependencyConfiguration.Configure(services, _configuration);

            return new AutofacServiceProvider(_applicationContainer);
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, AppConfiguration configuration)
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
