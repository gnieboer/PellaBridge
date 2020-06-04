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
            // Initialize connection to Pella device and connect
            pellaBridgeClient = new PellaBridgeClient();
            // Set up listener REST API for the ST Hub
            CreateHostBuilder(args).Build().Run();
            try
            {
                BridgeInfo bi = pellaBridgeClient.GetBridgeInfo();
                Trace.WriteLine("Connection to Bridge Successful");
                Trace.WriteLine($"Version: {bi.Version}, MAC: {bi.MAC}");
            }
            catch (Exception)
            {
                Trace.WriteLine("Connection to bridge failed");
                throw;
            }
            
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}