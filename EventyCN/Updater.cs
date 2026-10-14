using System.Net.Http;
using System.Threading.Tasks;

namespace EventyCN;

public static class Updater
{
    private const string BaseUrl = "https://cqnews.web.sdo.com/api/news/newsList";

    private static readonly HttpClient Client = new();

    static Updater()
    {
        Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        Client.DefaultRequestHeaders.Add("Referer", "https://actff1.web.sdo.com/");

        Client.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 获取活动中心节庆列数据（CategoryCode=7141）。
    /// 一次性拉取全部条目，不按月分页。
    /// </summary>
    /// <returns>API 返回的 JSON 字符串</returns>
    public static async Task<string> GetEventsCn()
    {
        try
        {
            var url = $"{BaseUrl}?gameCode=ff&CategoryCode=7141&pageIndex=0&pageSize=50";
            var response = await Client.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "获取活动数据失败");
            return string.Empty;
        }
    }
}
