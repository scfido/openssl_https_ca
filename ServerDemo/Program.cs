using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace WebApplication2
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    var x509ca = new X509Certificate2(File.ReadAllBytes("server.pfx"), "123456");
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseKestrel(option =>
                    {
                        option.ListenAnyIP(5002, config => config.UseHttps(x509ca));
                    });
                })
            ;
    }
}
