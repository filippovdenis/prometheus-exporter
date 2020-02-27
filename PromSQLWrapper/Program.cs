using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data.SqlClient;
using Microsoft.Extensions.Logging.EventLog;

namespace PromSQLWrapper
{
    public class Program
    {
        private static Dictionary<string, string> arrayDict = new Dictionary<string, string>
        {
            {"MetricsFile", "PromSqlWrapperConfig.xml"},
            {"ConnectionString", "Data Source=(local);Initial Catalog=master;Integrated Security=true"}
        };        
        
        public static void Main(string[] args)
        {
            
            var isService = !args.Contains("--console");

            if (isService)
            {
                var pathToExe = Process.GetCurrentProcess().MainModule.FileName;
                var pathToContentRoot = Path.GetDirectoryName(pathToExe);
                Directory.SetCurrentDirectory(pathToContentRoot);
            }

            string CustomConfig = "";
            var argConfig = args.Where(arg => arg.StartsWith("--ConfigFile")).FirstOrDefault();

            if (argConfig != null)
            {
                var parts = argConfig.Split("=");
                if (parts?.Length > 1)
                    CustomConfig = parts[1];                
            }

            var builder = CreateWebHostBuilder(CustomConfig, args.Where(arg => arg != "--console").ToArray());

            var host = builder.Build();
            
            if (isService)
            {
                host.RunAsService();
            }
            else
            {
                host.Run();
            }            
        }

        public static IWebHostBuilder CreateWebHostBuilder(string CustomConfigFile, string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                    {
                        var env = hostingContext.HostingEnvironment;
                        config.AddInMemoryCollection(arrayDict);
                        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                        config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: false);
                        if (CustomConfigFile != "")
                        {
                            config.AddJsonFile(CustomConfigFile, optional: false, reloadOnChange: false);
                        }                        
                        config.AddCommandLine(args);
                    }
                 )
                .ConfigureLogging((hostingContext, logging) =>
                    {
                        logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                        logging.AddConsole();
                        logging.AddEventLog();
                    }
                    )
                .UseStartup<Startup>();
    }
}