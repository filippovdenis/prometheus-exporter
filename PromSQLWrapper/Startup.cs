using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PromSQLWrapper.Services;

namespace PromSQLWrapper
{
    public class Startup
    {
        
        private readonly ILogger<Startup> _logger;
        private readonly IConfiguration _configuration;
        
        public Startup(ILogger<Startup> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }
        
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<MetricCollection>();
            
            services.AddSingleton<SystemMetrics>();

            services.AddSingleton<DbContext>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, MetricCollection metricCollection, DbContext dbContext)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            _logger.LogInformation("This is from logger");

            app.Map("/metrics", Metrics);

            //default output
            app.Run(async (context) =>
            {
                context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
                await context.Response.WriteAsync("Prometheus SQL Wrapper");
            });
        }

        private static void Metrics(IApplicationBuilder app)
        {
            app.Run(async context =>
                {
                    var dbconn = app.ApplicationServices.GetService<DbContext>();
                    var metricsStringBuilder = dbconn.GetMetrics();
                    
                    var sysMetrics = app.ApplicationServices.GetService<SystemMetrics>();
                    var sb = sysMetrics.GetMetrics();
                    metricsStringBuilder.Append(sb);
                    
                    context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
                    await context.Response.WriteAsync(metricsStringBuilder.ToString());
                }
            );
        }
    }
}