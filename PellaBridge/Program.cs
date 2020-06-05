using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PellaBridge
{
    public class Program
    {
        public static PellaBridgeClient pellaBridgeClient;

        public static void Main(string[] args)
        {
            ConsoleTraceListener consoleTracer = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleTracer);

            // Initialize connection to Pella device and connect
            pellaBridgeClient = new PellaBridgeClient();
            // Set up listener REST API for the ST Hub
            CreateHostBuilder(args).Build().Run(); 
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
