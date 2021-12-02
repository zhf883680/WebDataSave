using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebDataSave.Job;

namespace WebDataSave
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient();
            services.Configure<List<KewRoad>>(Configuration.GetSection("keyRoads"));
            //添加Quartz服务
            services.AddSingleton<IJobFactory, SingletonJobFactory>();
            services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
            //添加我们的Job
            services.AddSingleton<WorkJob>();
            services.AddSingleton(
                 new JobSchedule(jobType: typeof(WorkJob), cronExpression: "0 30 8 ? * 2,3,4,5,6"));
           

#if DEBUG
            services.AddSingleton<HomeJob>();
            services.AddSingleton(
                 new JobSchedule(jobType: typeof(HomeJob), cronExpression: "0/10 * * ? * 2,3,4,5,6"));
#else
 services.AddSingleton<HomeJob>();
            services.AddSingleton(
                 new JobSchedule(jobType: typeof(HomeJob), cronExpression: "0 0 18 ? * 2,3,4,5,6")
           );
#endif


            services.AddHostedService<QuartzHostedService>();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
