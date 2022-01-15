using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WebDataSave
{
    public class AutoWorkApply
    {
        public class UserInfo
        {
            public string UserName { get; set; }
            public string WechatId { get; set; }
        }


        public static List<UserInfo> AllowUserList = new List<UserInfo>()
        {
             new UserInfo
            {
                UserName="张三",
                WechatId="zhangsan"
            }
        };
        public static string accessToken = string.Empty;
        //审批的Secret
        public static string approveSecret = "";
        //打卡的Secret
        public static string checkInSecret = "";
        //审批模板Id   https://work.weixin.qq.com/api/doc/90000/90135/91982
        public static string template_id = "";




        [DisallowConcurrentExecution]
        public class AutoWorkApplyJob : IJob
        {
            public IConfiguration _configuration;

            public AutoWorkApplyJob(IConfiguration configuration)
            {
                this._configuration = configuration;
            }

            public async Task Execute(IJobExecutionContext context)
            {
                await CheckWork();
            }
        }
        public static async Task CheckWork()
        {
            if (HttpHelper.holidays.Count == 0)
            {
                HttpHelper.holidays.AddRange(await HttpHelper.GetHolidayByYear(DateTime.Now.Year));
                HttpHelper.holidays.AddRange(await HttpHelper.GetHolidayByYear(DateTime.Now.Year + 1));
            }
            //获取token
            var accessTokenWork = await GetAccessTokenAsync(checkInSecret);
            accessToken = await GetAccessTokenAsync(approveSecret);
            //获取前一天的加班记录
            var startTime = TimeToInt(Convert.ToDateTime(DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd 00:00:00")));
            var endTime = TimeToInt(Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 06:00:00")));
            foreach (var item in AllowUserList)
            {
                //获取打卡记录
                var workResultStr = await HttpRequestAsync($"https://qyapi.weixin.qq.com/cgi-bin/checkin/getcheckindata?access_token={accessTokenWork}", "post", new StringContent(JsonSerializer.Serialize(new
                {
                    opencheckindatatype = 3,
                    starttime = startTime,
                    endtime = endTime,
                    useridlist = item.WechatId
                }), Encoding.UTF8, "application/json"));
                var workResult = JsonSerializer.Deserialize<CheckInResponse>(workResultStr);
                if (workResult.errcode != 0)
                {
                    Console.WriteLine(workResult.errmsg);
                    continue;
                }
                //当日所有打卡记录
                var todayWorkTimeList = workResult.checkindata.Where(a => a.checkin_time > startTime && a.checkin_time <= endTime).ToList();
                var todayWorkBegin = todayWorkTimeList.Where(a => a.checkin_type == "上班打卡" && !a.exception_type.Contains("未打卡")).OrderBy(a => a.checkin_time).FirstOrDefault();
                var todayWorkEnd = todayWorkTimeList.Where(a => a.checkin_type == "下班打卡" && !a.exception_type.Contains("未打卡")).OrderByDescending(a => a.checkin_time).FirstOrDefault();
                if (todayWorkEnd == null || todayWorkBegin == null)
                {
                    //有未打卡
                    startTime += 24 * 60 * 60;
                    continue;
                }
                //节假日
                if (CheckIsHoliday(UnixToDateTime(todayWorkEnd.checkin_time)))
                {
                    //申请加班
                    var result = await SendApply(UnixToDateTime(todayWorkBegin.checkin_time), UnixToDateTime(todayWorkEnd.checkin_time), item.WechatId);
                    if (result.errcode != 0)
                    {
                        Console.WriteLine(result.errmsg);
                        continue;
                    }
                    else
                    {
                        Console.WriteLine($"{item.UserName}申请成功{UnixToDateTime(todayWorkBegin.checkin_time)}--{UnixToDateTime(todayWorkEnd.checkin_time)}");
                        continue;

                    }
                }
                else
                {
                    //工作日
                    //下班打卡时间超过7点 
                    if (todayWorkEnd.checkin_time > TimeToInt(Convert.ToDateTime(UnixToDateTime(todayWorkEnd.checkin_time).ToString("yyyy-MM-dd 19:00:00"))))
                    {

                        //申请加班
                        var result = await SendApply(UnixToDateTime(startTime + 18 * 60 * 60), todayWorkEnd.checkin_time > TimeToInt(Convert.ToDateTime(UnixToDateTime(todayWorkEnd.checkin_time).ToString("yyyy-MM-dd 20:00:00"))) ? UnixToDateTime(startTime + 20 * 60 * 60) : UnixToDateTime(startTime + 19 * 60 * 60), item.WechatId);
                        if (result.errcode != 0)
                        {
                            Console.WriteLine(result.errmsg);
                            continue;

                        }
                        else
                        {
                            Console.WriteLine($"{item.UserName}申请成功:{UnixToDateTime(startTime + 18 * 60 * 60)}--{(todayWorkEnd.checkin_time > TimeToInt(Convert.ToDateTime(UnixToDateTime(todayWorkEnd.checkin_time).ToString("yyyy-MM-dd 20:00:00"))) ? UnixToDateTime(startTime + 20 * 60 * 60) : UnixToDateTime(startTime + 19 * 60 * 60))}");
                            continue;
                        }
                    }


                }
            }
        }
        public static DateTime UnixToDateTime(double unix)
        {
            return new DateTime(1970, 1, 1).AddSeconds(unix).ToLocalTime();
        }
        public async static Task<AccessResponse> SendApply(DateTime begin, DateTime end, string wechatId)
        {
            if (begin >= end)
            {
                return new AccessResponse()
                {
                    errcode = 1,
                    errmsg = $"时间不对,固未申请:{begin}-{end}"
                };
            }
            var beginInt = TimeToInt(begin);
            var endInt = TimeToInt(end);
            var applyInfo = new ApplyEvent()
            {
                creator_userid = wechatId,
                template_id = template_id,//模版接口
                use_template_approver = 1,
                approver = new List<Approver>(),
                notify_type = 1,
                apply_data = new ApplyData()
                {
                    contents = new List<Content>()
                    {
                        new Content()
                        {
                            control="Attendance",
                            id="smart-time",//模版接口
                            value=new Value(){
                            attendance=new Attendance()
                            {
                                date_range=new DateRange()
                                {
                                    type="halfday",
                                    new_begin=beginInt,
                                    new_end=endInt,
                                    new_duration=endInt-beginInt

                                },
                                type=5
                            }
                            },
                        }
                    }
                },
                summary_list = new List<SummaryList>() {
                    new SummaryList
                    {
                        summary_info=new List<SummaryInfo>()
                        {
                            new SummaryInfo(){
                            text=begin.ToString("yyyy-MM-dd")+"加班"
                            }
                        }
                        },
                        new SummaryList
                    {
                        summary_info=new List<SummaryInfo>()
                        {
                            new SummaryInfo(){
                            text=begin.ToString("yyyy-MM-dd HH:mm:ss")
                            }
                        }
                    },
                         new SummaryList
                    {
                        summary_info=new List<SummaryInfo>()
                        {
                            new SummaryInfo(){
                            text=end.ToString("yyyy-MM-dd HH:mm:ss")
                            }
                        }
                    }
                }
            };
            var result = await HttpRequestAsync($"https://qyapi.weixin.qq.com/cgi-bin/oa/applyevent?access_token={accessToken}", "post", new StringContent(JsonSerializer.Serialize(applyInfo), Encoding.UTF8, "application/json"));
            return JsonSerializer.Deserialize<AccessResponse>(result);
        }
        public async static Task<string> GetAccessTokenAsync(string pwd)
        {
            string path = $"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid=wwc5c46a33e9fe271f&corpsecret={pwd}";
            string result = await HttpRequestAsync(path, "get");
            if (string.IsNullOrEmpty(result))
            {
                Console.WriteLine("获取Token失败");
                return null;
            }
            var response = JsonSerializer.Deserialize<AccessResponse>(result);
            if (response.errcode == 0)
            {
                return response.access_token;
            }
            else
            {
                Console.WriteLine($"获取Token失败，接口返回错误代码：{response.errcode},错误信息：{response.errmsg}");
            }
            return null;
        }
        public class WeChatResponse
        {
            public int errcode { get; set; }
            public string errmsg { get; set; }
        }
        public class AccessResponse : WeChatResponse
        {
            public string access_token { get; set; }
            public double expires_in { get; set; }
        }
        /// <summary>
        /// 批量获取审批单号返回数据
        /// </summary>
        public class ApprovalResponse : WeChatResponse
        {
            public int next_cursor { get; set; }
            public List<string> sp_no_list { get; set; }
        }
        /// <summary>
        /// 审批信息返回数据
        /// </summary>
        public class ApprovalInfoResponse : WeChatResponse
        {
            public ApprovalInfo info { get; set; }
        }
        /// <summary>
        /// 审批信息
        /// </summary>
        public class ApprovalInfo
        {
            public string sp_no { get; set; }
            public string sp_name { get; set; }
            public int sp_status { get; set; }
            public string template_id { get; set; }
            public double apply_time { get; set; }
            public applyer applyer { get; set; }
            public object apply_data { get; set; }
            public List<object> comments { get; set; }
        }
        public class applyer
        {
            public string userid { get; set; }
            public string partyid { get; set; }
        }

        /// <summary>
        /// 考勤返回数据
        /// </summary>
        public class CheckInResponse : WeChatResponse
        {
            public List<CheckIn> checkindata { get; set; }
        }
        /// <summary>
        /// 考勤信息
        /// </summary>
        public class CheckIn
        {
            public string userid { get; set; }
            public string groupname { get; set; }
            public string checkin_type { get; set; }
            public string exception_type { get; set; }
            public int checkin_time { get; set; }
            public string location_title { get; set; }
            public string location_detail { get; set; }
            public string wifiname { get; set; }
            public string notes { get; set; }
            public string wifimac { get; set; }
            public string deviceid { get; set; }
            public int sch_checkin_time { get; set; }
            public int groupid { get; set; }
            public int timeline_id { get; set; }
        }
        public async static Task<string> HttpRequestAsync(string path, string type, HttpContent content = null)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage message;
                if (type == "get")
                {
                    message = await httpClient.GetAsync(path);
                }
                else
                {
                    message = await httpClient.PostAsync(path, content);
                }
                return await message.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"接口通讯失败！\n接口路径：{path}\n类型:{type}\n参数:{await content.ReadAsStringAsync()}\n错误：{ex.Message}");
            }
            return null;
        }
        public static int TimeToInt(DateTime time)
        {
            DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (int)(time.AddHours(-8) - Jan1st1970).TotalSeconds;
        }
        public class Approver
        {
            public int attr { get; set; }
            public List<string> userid { get; set; }
        }

        public class Value
        {
            public Attendance attendance { get; set; }
        }

        public class DateRange
        {
            public string type { get; set; }
            public int new_begin { get; set; }
            public int new_end { get; set; }
            public int new_duration { get; set; }
        }

        public class Attendance
        {
            public DateRange date_range { get; set; }
            public int type { get; set; }
        }

        public class Content
        {
            public string control { get; set; }
            public string id { get; set; }
            public Value value { get; set; }
        }

        public class ApplyData
        {
            public List<Content> contents { get; set; }
        }

        public class SummaryInfo
        {
            public string text { get; set; }
            public string lang { get; set; }
        }

        public class SummaryList
        {
            public List<SummaryInfo> summary_info { get; set; }
        }

        public class ApplyEvent
        {
            public string creator_userid { get; set; }
            public string template_id { get; set; }
            public int use_template_approver { get; set; }
            public List<Approver> approver { get; set; }
            public List<string> notifyer { get; set; }
            public int notify_type { get; set; }
            public ApplyData apply_data { get; set; }
            public List<SummaryList> summary_list { get; set; }
        }

        public static bool CheckIsHoliday(DateTime nowDate)
        {
            //.ToString("yyyy-MM-dd")
           // var checkData =Convert.ToDateTime(nowDate);
            //节假日不执行
            var todayInfo = HttpHelper.holidays.FirstOrDefault(a => a.date == nowDate.ToString("yyyy-MM-dd"));
            if (todayInfo != null)
            {
                if (todayInfo.holiday)
                {
                    return true;
                }
            }
            else
            {
                //非列表中 则周末默认休息
                if (nowDate.DayOfWeek == DayOfWeek.Saturday || nowDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    return true;
                }
            }
            return false;
        }
    }


}
