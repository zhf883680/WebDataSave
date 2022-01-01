using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WebDataSave
{
    public class HttpHelper
    {
        public static List<HolidayDetails> holidays = new List<HolidayDetails>();

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

        /// <summary>
        /// 获取当年的节假日
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static async Task<List<HolidayDetails>> GetHolidayByYear(int year)
        {
            //没获取过或者缓存的时间不是今年时，重新获取

            string path = $"http://timor.tech/api/holiday/year/{year}";
            var result = await HttpHelper.HttpGet(path);
            var resultObj = JsonSerializer.Deserialize<Holiday>(result);
            return resultObj.holiday.Values.ToList();
        }

        public static async Task Roads(IConfiguration configuration, IOptions<List<KewRoad>> keyRoadsList,
            IOptions<List<Origin>> originList, IOptions<List<Destination>> destinationList, int type, int user)
        {
            if (HttpHelper.holidays.Count == 0)
            {
                HttpHelper.holidays.AddRange(await HttpHelper.GetHolidayByYear(DateTime.Now.Year));
                HttpHelper.holidays.AddRange(await HttpHelper.GetHolidayByYear(DateTime.Now.Year + 1));
            }

            var nowDate = DateTime.Now.ToString("yyyy-MM-dd");
            //节假日不执行
            var todayInfo = HttpHelper.holidays.FirstOrDefault(a => a.date == nowDate);
            if (todayInfo != null)
            {
                if (todayInfo.holiday)
                {
                    return;
                }
            }
            else
            {
                //非列表中 则周末默认休息
                if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                {
                    return;
                }
            }

            var typeStr = type == 1 ? "上班路线" : "下班路线";
            var origin = type == 1 ? originList.Value[user].origin : destinationList.Value[user].destination;
            var destination = type == 1 ? destinationList.Value[user].destination : originList.Value[user].origin;
            //上班
            var roads = new List<string>();
            var tellMsg = new StringBuilder();
            var roadUrl =
                $"https://restapi.amap.com/v5/direction/driving?origin={origin}&destination={destination}&show_fields=cost&key={configuration["key"]}";
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
                                showRoads = showRoads.Reverse().ToArray();
                                tellMsg.Append(String.Join("->", showRoads));
                            }
                            else
                            {
                                tellMsg.Append(keyRoadMatch.roadShow);
                            }

                            tellMsg.Append($"-{Convert.ToInt32(Convert.ToInt32(item.cost.duration)) / 60}分\n");
                            isMatch = true;
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
        private readonly IOptions<List<Origin>> _originList;
        private readonly IOptions<List<Destination>> _destinationList;


        public WorkJob(IConfiguration configuration, IOptions<List<KewRoad>> keyRoadsList,
            IOptions<List<Origin>> originList, IOptions<List<Destination>> destinationList)
        {
            this._configuration = configuration;
            this._keyRoadsList = keyRoadsList;
            this._originList = originList;
            this._destinationList = destinationList;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await HttpHelper.Roads(_configuration, _keyRoadsList, _originList, _destinationList, 1, 0);
        }
    }

    [DisallowConcurrentExecution]
    public class ZDWWorkJob : IJob
    {
        public IConfiguration _configuration;
        private readonly IOptions<List<KewRoad>> _keyRoadsList;
        private readonly IOptions<List<Origin>> _originList;
        private readonly IOptions<List<Destination>> _destinationList;


        public ZDWWorkJob(IConfiguration configuration, IOptions<List<KewRoad>> keyRoadsList,
            IOptions<List<Origin>> originList, IOptions<List<Destination>> destinationList)
        {
            this._configuration = configuration;
            this._keyRoadsList = keyRoadsList;
            this._originList = originList;
            this._destinationList = destinationList;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            if (_originList.Value.Count > 1)
            {
                await HttpHelper.Roads(_configuration, _keyRoadsList, _originList, _destinationList, 1, 1);
            }
        }
    }

    [DisallowConcurrentExecution]
    public class HomeJob : IJob
    {
        public IConfiguration _configuration;
        private readonly IOptions<List<KewRoad>> _keyRoadsList;
        private readonly IOptions<List<Origin>> _originList;
        private readonly IOptions<List<Destination>> _destinationList;


        public HomeJob(IConfiguration configuration, IOptions<List<KewRoad>> keyRoadsList,
            IOptions<List<Origin>> originList, IOptions<List<Destination>> destinationList)
        {
            this._configuration = configuration;
            this._keyRoadsList = keyRoadsList;
            this._originList = originList;
            this._destinationList = destinationList;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await HttpHelper.Roads(_configuration, _keyRoadsList, _originList, _destinationList, 2, 0);
        }
    }
    [DisallowConcurrentExecution]
    public class JDJob : IJob
    {
        public IConfiguration _configuration;


        public JDJob(IConfiguration configuration)
        {
            this._configuration = configuration;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await JDHelper.GetJdStockInfo(_configuration);
        }
    }
    public class JDHelper
    {
        public class JsonRootDto
        {
            public StockInfo stockInfo { get; set; }
        }

        public class StockInfo
        {
            public string stockDesc { get; set; }
        }

        public class ShopInfo
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public DateTime LastSendTime { get; set; } = DateTime.Now.AddDays(-1);
        }

        public static List<ShopInfo> shops = new List<ShopInfo>()
        {
            new ShopInfo()
            {
                Id = "100017631158",
                Name = "X-S10"
            },
            new ShopInfo()
            {
                Id = "100015253059",
                Name = "银色X-T30-XC35"
            },
            new ShopInfo()
            {
                Id = "100015253079",
                Name = "银色X-T30-1545"
            },
            new ShopInfo()
            {
                Id = "100015253061",
                Name = "黑色X-T30-XC35"
            },
            new ShopInfo()
            {
                Id = "100028021978",
                Name = "黑色X-T30-1545"
            },
        };
        public async static Task GetJdStockInfo(IConfiguration configuration)
        {
            foreach (var item in shops)
            {
                if (DateTime.Now < item.LastSendTime)
                {
                    //若当前时间小于最后发送时间，则不发送消息
                    return;
                }
                var url =
                    $"https://item-soa.jd.com/getWareBusiness?callback=jQuery3739321&skuId={item.Id}&cat=652%2C654%2C5012&area=12_988_3085_51586&shopId=1000000858&venderId=1000000858&paramJson=%7B%22platform2%22%3A%22100000000001%22%2C%22specialAttrStr%22%3A%22p0pp1ppppppp2p1ppppppppppp%22%2C%22skuMarkStr%22%3A%2200%22%7D&num=1";
                var msg = await HttpHelper.HttpGet(url);
                msg = msg.Replace("jQuery3739321(", "").TrimEnd(')');
                var thisResult = JsonSerializer.Deserialize<JsonRootDto>(msg);
                if (thisResult != null && !thisResult.stockInfo.stockDesc.Contains("无货"))
                {
                    await HttpHelper.HttpGet($"{configuration["barkUrl"]}{item.Name}有货了！！");
                    item.LastSendTime=DateTime.Now.AddMinutes(10);//5分钟后在发
                    //item.LastSendTime=DateTime.Now.AddSeconds(20);
                }
            }
        }
    }

    public class KewRoad
    {
        public string name { get; set; }
        public string roadShow { get; set; }
    }

    public class Origin
    {
        public string origin { get; set; }
    }

    public class Destination
    {
        public string destination { get; set; }
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


    public class Holiday
    {
        public int code { get; set; }
        public Dictionary<string, HolidayDetails> holiday { get; set; }
    }

    public class HolidayDetails
    {
        public bool holiday { get; set; }
        public string date { get; set; }
    }


    //public class Rootobject
    //{
    //    public int code { get; set; }
    //    public Holiday holiday { get; set; }
    //}

    //public class Holiday
    //{
    //    public _0101 _0101 { get; set; }
    //}

    //public class _0101
    //{
    //    public bool holiday { get; set; }
    //    public string name { get; set; }
    //    public int wage { get; set; }
    //    public string date { get; set; }
    //}

    //https://item-soa.jd.com/getWareBusiness?callback=jQuery2110954&skuId=100017631158&cat=652%2C654%2C5012&area=12_988_3085_51586&shopId=1000000858&venderId=1000000858&paramJson=%7B%22platform2%22%3A%22100000000001%22%2C%22specialAttrStr%22%3A%22p0pp1ppppppp2p1ppppppppppp%22%2C%22skuMarkStr%22%3A%2200%22%7D&num=1
    //https://item-soa.jd.com/getWareBusiness?callback=jQuery3739321&skuId=100017631158&cat=652%2C654%2C5012&area=12_988_3085_51586&shopId=1000000858&venderId=1000000858&paramJson=%7B%22platform2%22%3A%22100000000001%22%2C%22specialAttrStr%22%3A%22p0pp1ppppppp2p1ppppppppppp%22%2C%22skuMarkStr%22%3A%2200%22%7D&num=1

    /// <summary>
    /// 获取当年的节假日
    /// </summary>
    /// <param name="time"></param>
    /// <returns></returns>
}