using System.Collections.Frozen;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using EventyCN.Attributes;
using EventyCN.Windows;
using EventyCN.Windows.Config;
using Newtonsoft.Json;

namespace EventyCN;

public class Plugin : IDalamudPlugin
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager Commands { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static INotificationManager Notification { get; private set; } = null!;
    [PluginService] public static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;

    public static Configuration Configuration { get; private set; } = null!;

    public readonly WindowSystem WindowSystem = new("EventyCN");
    public ConfigWindow ConfigWindow { get; init; }

    private readonly PluginCommandManager<Plugin> CommandManager;
    public readonly ServerBar ServerBar;

    public FrozenDictionary<long, ParsedEvent[]> Events = FrozenDictionary<long, ParsedEvent[]>.Empty;

    private int _loaded;
    private readonly object _loadLock = new();

    /// <summary>
    /// 预定义颜色池，为新数据源缺少颜色字段的活动自动分配颜色。
    /// </summary>
    private static readonly uint[] ColorPool =
    [
        Helper.HexToUint("#4CAF50"),
        Helper.HexToUint("#2196F3"),
        Helper.HexToUint("#9C27B0"),
        Helper.HexToUint("#FF9800"),
        Helper.HexToUint("#E91E63"),
        Helper.HexToUint("#FFC107"),
        Helper.HexToUint("#F44336"),
        Helper.HexToUint("#FFEB3B"),
    ];

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        CommandManager = new PluginCommandManager<Plugin>(this, Commands);
        ServerBar = new ServerBar(this);

        PluginInterface.UiBuilder.Draw += DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;

        Task.Run(async () => await LoadEvents());
    }

    [Command("/eventycn")]
    [HelpMessage("打开插件设置")]
    public void OpenMainCommand(string _, string __)
    {
        ConfigWindow.Toggle();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();

        PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;

        CommandManager.Dispose();
        ServerBar.Dispose();
    }

    /// <summary>
    /// 初始化加载：一次性拉取活动中心节庆列的全部活动数据。
    /// </summary>
    private async Task LoadEvents()
    {
        await EnsureEventsLoaded();
    }

    /// <summary>
    /// 确保活动数据已加载。新数据源一次性返回全部活动（不按月分页），
    /// 因此只需加载一次。
    /// </summary>
    public async Task EnsureEventsLoaded()
    {
        lock (_loadLock)
        {
            if (_loaded != 0)
                return;
            _loaded = 1;
        }

        var json = await Updater.GetEventsCn();
        if (string.IsNullOrEmpty(json))
            return;

        ActiveCenterResponse? resp;
        try
        {
            resp = JsonConvert.DeserializeObject<ActiveCenterResponse>(json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "解析活动数据失败");
            return;
        }

        if (resp?.Code != "0" || resp.Data == null)
            return;

        var events = resp.Data.OrderBy(e => e.SortIndex).ToArray();
        if (events.Length == 0)
            return;

        lock (_loadLock)
        {
            var dict = new Dictionary<long, ParsedEvent[]>();
            var existingIds = new HashSet<long>();

            for (var i = 0; i < events.Length; i++)
            {
                var ev = events[i];

                if (existingIds.Contains(ev.Id))
                    continue;

                if (!TryParseEventTime(ev.Summary, out var begin, out var end))
                {
                    Log.Warning($"无法解析活动时间：{ev.Title} - {ev.Summary}");
                    continue;
                }

                var color = ColorPool[i % ColorPool.Length];
                var opacity = color & 0x00FFFFFF | ((uint)(0.5f * 255) << 24);

                var eventDay = new ParsedEvent
                {
                    Id = ev.Id,
                    Name = ev.Title,
                    Begin = begin,
                    End = end,
                    Url = ev.OutLink,
                    Color = color,
                    Opacity = opacity,
                    Spacing = 17.0f
                };

                existingIds.Add(ev.Id);

                foreach (var (idx, day) in Utils.EachDay(begin, end).Index())
                {
                    eventDay.IsFirst = idx == 0;
                    if (!dict.TryAdd(day.Ticks, [eventDay]))
                    {
                        var entries = dict[day.Ticks];

                        if (eventDay.IsFirst)
                        {
                            while (eventDay.Spacing < 80.0f)
                            {
                                if (entries.All(e => (int)e.Spacing != (int)eventDay.Spacing))
                                    break;

                                eventDay.Spacing += 10.0f;
                            }
                        }

                        dict[day.Ticks] = entries.Append(eventDay).ToArray();
                    }
                }
            }

            Events = dict.ToFrozenDictionary();
        }

        ServerBar.Refresh();
    }

    /// <summary>
    /// 从 Summary 字段中解析活动时间。
    /// 支持格式：活动时间：2026年10月7日16:00～2026年10月27日22:59
    /// 分隔符可以是 ～、~、-、— 等。
    /// </summary>
    private static readonly Regex EventTimeRegex = new(
        @"活动时间[：:]\s*(\d{4}年\d{1,2}月\d{1,2}日\s*\d{1,2}:\d{2})\s*[～~\-—–]\s*(\d{4}年\d{1,2}月\d{1,2}日\s*\d{1,2}:\d{2})",
        RegexOptions.Compiled);

    private static bool TryParseEventTime(string? summary, out DateTime begin, out DateTime end)
    {
        begin = DateTime.UnixEpoch;
        end = DateTime.UnixEpoch;

        if (string.IsNullOrEmpty(summary))
            return false;

        var match = EventTimeRegex.Match(summary);
        if (!match.Success)
            return false;

        if (!TryParseCnDateTime(match.Groups[1].Value, out begin))
            return false;

        if (!TryParseCnDateTime(match.Groups[2].Value, out end))
            return false;

        return true;
    }

    /// <summary>
    /// 解析中文日期时间格式，如 "2026年10月7日16:00" 或 "2026年10月7日 16:00"。
    /// </summary>
    private static bool TryParseCnDateTime(string input, out DateTime result)
    {
        const string format1 = "yyyy年M月d日HH:mm";
        const string format2 = "yyyy年M月d日 HH:mm";

        input = input.Trim();

        if (DateTime.TryParseExact(input, format1, null, System.Globalization.DateTimeStyles.None, out result))
            return true;

        if (DateTime.TryParseExact(input, format2, null, System.Globalization.DateTimeStyles.None, out result))
            return true;

        return DateTime.TryParseExact(input, "yyyy年M月d日 H:mm", null, System.Globalization.DateTimeStyles.None, out result);
    }

    private void DrawUi() => WindowSystem.Draw();
    public void OpenConfig() => ConfigWindow.Toggle();
}

public class ActiveCenterResponse
{
    [JsonProperty("Code")]
    public string Code = "";

    [JsonProperty("Message")]
    public string Message = "";

    [JsonProperty("Data")]
    public ActiveCenterEvent[] Data = [];
}

public class ActiveCenterEvent
{
    [JsonProperty("Id")]
    public long Id;

    [JsonProperty("Title")]
    public string Title = "";

    [JsonProperty("Summary")]
    public string Summary = "";

    [JsonProperty("OutLink")]
    public string OutLink = "";

    [JsonProperty("HomeImagePath")]
    public string HomeImagePath = "";

    [JsonProperty("PublishDate")]
    public string PublishDate = "";

    [JsonProperty("SortIndex")]
    public int SortIndex;

    [JsonProperty("CategoryCode")]
    public int CategoryCode;
}

public struct ParsedEvent
{
    public long Id;

    public string Name = "";
    public DateTime Begin = DateTime.UnixEpoch;
    public DateTime End = DateTime.UnixEpoch;
    public string Url = "";
    public uint Color = 0;
    public uint Opacity = 0;
    public float Spacing = 0;

    public bool IsFirst = false;

    public ParsedEvent() {}
}
