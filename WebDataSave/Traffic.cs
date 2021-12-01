using Microsoft.Extensions.Configuration;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WebDataSave
{
    public class HttpHelper
    {
        public static async Task<string> HttpGet(string url, int timeout = 100)
        {
            try
            {
                //HttpClientHandler clientHandler = new HttpClientHandler();
                //忽略证书
                //clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(timeout);
                HttpResponseMessage msg = await client.GetAsync(url);
                if (msg.StatusCode != HttpStatusCode.OK)
                {
                    return "error";
                }
                return await msg.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"url请求失败{url}");
                Console.WriteLine(ex.ToString());
            }
            return "error";
        }

        public static async Task Roads(IConfiguration configuration,int type)
        {
            var typeStr = type == 1 ? "上班路线" : "下班路线";
            var origin = type == 1 ? configuration["origin"] : configuration["destination"];
            var destination = type == 1 ? configuration["destination"] : configuration["origin"];
            //上班
            var roads = new List<string>();
            var tellMsg = new StringBuilder();
            var roadUrl = $"https://restapi.amap.com/v5/direction/driving?origin={origin}&destination={destination}&show_fields=cost&key={configuration["key"]}";
            var result = await HttpHelper.HttpGet(roadUrl);
            var resultObj = JsonSerializer.Deserialize<Rootobject>(result);
            if (resultObj.status == "0")
            {
                await HttpHelper.HttpGet($"{configuration["barkUrl"]}错误/{resultObj.info}");
            }
            foreach (var item in resultObj.route.paths)
            {
                roads.Clear();
                foreach (var step in item.steps)
                {
                    if (!string.IsNullOrWhiteSpace(step.road_name)
                        && step.road_name.IndexOf("入口") < 0
                        && step.road_name.IndexOf("出口") < 0)
                    {
                        roads.Add(step.road_name);
                    }
                }
                if (roads.Count > 3)
                {
                    var needDelCount = roads.Count / 2;
                    roads = roads.Skip(needDelCount - 1).Take(3).ToList();
                }
                tellMsg.Append(string.Join("->", roads));
                tellMsg.Append($"-{Convert.ToInt32(Convert.ToInt32(item.cost.duration)) / 60}分\n");
            }
            await HttpHelper.HttpGet($"{configuration["barkUrl"]}{typeStr}/{tellMsg}");
        }
    }
    [DisallowConcurrentExecution]
    public class WorkJob : IJob
    {
        public IConfiguration _configuration;

        public WorkJob(IConfiguration configuration)
        {
            this._configuration = configuration;
        }
        public async Task Execute(IJobExecutionContext context)
        {
            await HttpHelper.Roads(_configuration, 1);
        }
    }

    [DisallowConcurrentExecution]
    public class HomeJob : IJob
    {
        public IConfiguration _configuration;

        public HomeJob(IConfiguration configuration)
        {
            this._configuration = configuration;
        }
        public async Task Execute(IJobExecutionContext context)
        {
            await HttpHelper.Roads(_configuration, 2);
        }
    }
    /// <summary>
    /// 高德返回的
    /// </summary>
    public class Rootobject
    {
        public string status { get; set; }
        public string info { get; set; }
        public string infocode { get; set; }
        public string count { get; set; }
        public Route route { get; set; }
    }

    public class Route
    {
        public string origin { get; set; }
        public string destination { get; set; }
        public string taxi_cost { get; set; }
        public Path[] paths { get; set; }
    }

    public class Path
    {
        public string distance { get; set; }
        public string restriction { get; set; }
        public Cost cost { get; set; }
        public Step[] steps { get; set; }
    }

    public class Cost
    {
        public string duration { get; set; }
        public string tolls { get; set; }
        public string toll_distance { get; set; }
        public string traffic_lights { get; set; }
    }

    public class Step
    {
        public string instruction { get; set; }
        public string orientation { get; set; }
        public string step_distance { get; set; }
        public Cost1 cost { get; set; }
        public string road_name { get; set; }
    }

    public class Cost1
    {
        public string duration { get; set; }
        public string tolls { get; set; }
        public string toll_distance { get; set; }
        public string toll_road { get; set; }
        public string traffic_lights { get; set; }
    }


}
