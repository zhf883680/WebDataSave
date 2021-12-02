﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
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

        public static async Task Roads(IConfiguration configuration, IOptions<List<KewRoad>> keyRoadsList, int type)
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
            var keyRoads = keyRoadsList.Value;
            foreach (var item in resultObj.route.paths)
            {
                roads.Clear();
                var isMatch = false;
                foreach (var step in item.steps)
                {
                    if (!string.IsNullOrWhiteSpace(step.road_name)
                        && step.road_name.IndexOf("入口") < 0
                        && step.road_name.IndexOf("出口") < 0)
                    {
                        var keyRoadMatch = keyRoads.FirstOrDefault(a => a.name == step.road_name);
                        //命中节点直接结束
                        if (keyRoadMatch != null)
                        {
                            //若下班 则将配置文件中的路径顺序反转
                            if (type == 2)
                            {
                                var showRoads = keyRoadMatch.roadShow.Split("->");
                                showRoads=showRoads.Reverse().ToArray();
                                tellMsg.Append(String.Join("->", showRoads));
                            }
                            else
                            {
                                tellMsg.Append(keyRoadMatch.roadShow);
                            }
                            tellMsg.Append($"-{Convert.ToInt32(Convert.ToInt32(item.cost.duration)) / 60}分\n");
                            isMatch=true;
                            continue;
                        }
                        else
                        {
                            roads.Add(step.road_name);
                        }
                    }
                }
                if (!isMatch)
                {
                    tellMsg.Append(string.Join("-", roads));
                    tellMsg.Append($"-{Convert.ToInt32(Convert.ToInt32(item.cost.duration)) / 60}分\n");
                }
               
            }
            await HttpHelper.HttpGet($"{configuration["barkUrl"]}{typeStr}/{tellMsg}");
        }
    }
    [DisallowConcurrentExecution]
    public class WorkJob : IJob
    {
        public IConfiguration _configuration;
        private readonly IOptions<List<KewRoad>> _keyRoadsList;


        public WorkJob(IConfiguration configuration, IOptions<List<KewRoad>> keyRoadsList)
        {
            this._configuration = configuration;
            this._keyRoadsList = keyRoadsList;

        }
        public async Task Execute(IJobExecutionContext context)
        {
            await HttpHelper.Roads(_configuration, _keyRoadsList, 1);
        }
    }

    [DisallowConcurrentExecution]
    public class HomeJob : IJob
    {
        public IConfiguration _configuration;
        private readonly IOptions<List<KewRoad>> _keyRoadsList;

        public HomeJob(IConfiguration configuration, IOptions<List<KewRoad>> keyRoadsList)
        {
            this._configuration = configuration;
            this._keyRoadsList = keyRoadsList;
        }
        public async Task Execute(IJobExecutionContext context)
        {
            await HttpHelper.Roads(_configuration, _keyRoadsList, 2);
        }
    }
    public class KewRoad
    {
        public string name { get; set; }
        public string roadShow { get; set; }
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
