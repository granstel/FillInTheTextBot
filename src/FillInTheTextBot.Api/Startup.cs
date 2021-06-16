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


                Environment.SetEnvironmentVariable("JAEGER_SERVICE_NAME", "my-store");
                //Environment.SetEnvironmentVariable("JAEGER_AGENT_HOST", "192.168.2.27");
                //Environment.SetEnvironmentVariable("JAEGER_AGENT_PORT", "6831");
                //Environment.SetEnvironmentVariable("JAEGER_SAMPLER_TYPE", "const");                     
                //Environment.SetEnvironmentVariable("JAEGER_REPORTER_LOG_SPANS", "false");
                //Environment.SetEnvironmentVariable("JAEGER_SAMPLER_PARAM","1");
                //Environment.SetEnvironmentVariable("JAEGER_SAMPLER_MANAGER_HOST_PORT", "5778");
                //Environment.SetEnvironmentVariable("JAEGER_REPORTER_FLUSH_INTERVAL" , "1000");
                //Environment.SetEnvironmentVariable("JAEGER_REPORTER_MAX_QUEUE_SIZE" , "100");
                //application - server - id = server - x

                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                var sampler = new ConstSampler(sample: true);
                var reporter = new RemoteReporter.Builder()
                    .WithLoggerFactory(loggerFactory)
                    .WithSender(new UdpSender("localhost", 6831, 0))
                    .Build();

                var tracer = new Tracer.Builder(serviceName)
                    .WithLoggerFactory(loggerFactory)
                    .WithSampler(sampler)
                    .WithReporter(reporter)
                    .Build();

                if (!GlobalTracer.IsRegistered())
                {
                    GlobalTracer.Register(tracer);
                }
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
