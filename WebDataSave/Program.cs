using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;
namespace WebDataSave
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //¼ÇÂ¼±ÚÖ½Ë³Ðò
            var begin = Convert.ToDateTime("2021-11-30 10:00:00");
            var days = (DateTime.Now - begin).Days;
            if (!System.IO.File.Exists("lock.txt"))
            {
                //201
                System.IO.File.WriteAllText("lock.txt", (days+201).ToString());
            }
            if (!System.IO.File.Exists("base.txt"))
            {
                //190
                System.IO.File.WriteAllText("base.txt", (days + 190).ToString());
            }
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    //webBuilder.UseStartup<Startup>();
                    webBuilder.UseStartup<Startup>();
                });
    }
}
