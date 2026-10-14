# EventyCN

再也不忘记任何季节活动。在服务器栏中显示当前进行中的国服 FFXIV 活动，点击服务器栏入口直达设置页面，支持标记活动为已完成。

## 功能

- 服务器栏（DTR）入口显示当前进行中的活动数量，鼠标悬停查看详情
- 点击服务器栏入口直达插件设置页面
- 活动数据从官方活动中心节庆列实时获取
- 支持标记活动为「已完成」，已完成活动可在服务器栏中隐藏
- 服务器栏支持完整文字和简短图标两种显示模式
- 无活动时可选择自动隐藏服务器栏入口

## 截图

### 服务器栏

![服务器栏](EventyCN/images/EventyServerBar.gif)

## 使用方法

| 命令 | 说明 |
|------|------|
| `/eventycn` | 打开插件设置 |

也可通过卫月插件列表中的「打开设置」按钮访问，或点击服务器栏中的活动入口直接打开设置页面。

## How to get

1. 卫月设置 → 测试版
2. 添加仓库 `https://raw.githubusercontent.com/yimo0908/DalamudPlugin/main/repo.json` 并启用
3. 从插件列表中安装

## 项目结构

```
Eventy/
├── EventyCN/                       # 插件主项目
│   ├── EventyCN.csproj             # 项目文件，SDK 为 Dalamud.CN.NET.Sdk
│   ├── EventyCN.json               # 插件元数据（名称、描述、图标等）
│   ├── Plugin.cs                   # 插件入口，注册命令、窗口与活动数据加载
│   ├── Configuration.cs            # 插件配置（服务器栏显示、已完成活动等）
│   ├── Globals.cs                  # 全局 using 引用
│   ├── ServerBar.cs                # 服务器栏 DTR 条目管理与刷新
│   ├── Updater.cs                  # 活动数据请求（官方活动中心接口）
│   ├── Utils.cs                    # 工具方法（日期遍历、打开链接）
│   ├── Attributes/                 # 命令管理特性与命令管理器
│   │   ├── CommandAttribute.cs     # 命令标记特性
│   │   ├── AliasesAttribute.cs     # 命令别名特性
│   │   ├── HelpMessageAttribute.cs # 帮助消息特性
│   │   ├── DoNotShowInHelpAttribute.cs
│   │   └── PluginCommandManager.cs # 反射注册命令管理器
│   ├── Windows/
│   │   ├── Helper.cs               # UI 工具方法（文本换行、颜色转换）
│   │   └── Config/
│   │       ├── ConfigWindow.cs            # 设置窗口入口（Tab 布局）
│   │       ├── ConfigWindow.Settings.cs   # 设置标签页（服务器栏选项）
│   │       └── ConfigWindow.Completed.cs  # 已完成标签页（活动完成标记）
│   └── images/                     # 截图与图标
│       ├── EventyCalender.gif
│       ├── EventyServerBar.gif
│       └── icon.png
├── EventyCN.sln                    # 解决方案文件
├── LICENSE                         # MIT 许可证
└── README.md
```

### 核心文件说明

| 文件 | 职责 |
|------|------|
| `Plugin.cs` | 插件入口点，注册 `/eventycn` 命令，初始化 `ConfigWindow`，启动活动数据加载，管理 `Events` 数据字典 |
| `Configuration.cs` | 持久化配置：服务器栏显示开关、简短模式、无活动时隐藏、已完成活动显示开关、已完成活动 ID 集合 |
| `ServerBar.cs` | 服务器栏 DTR 条目管理：每 5 秒刷新当前活动，构建 Tooltip 文本，处理点击打开设置页面，根据配置控制可见性 |
| `Updater.cs` | 通过 HTTP 请求官方活动中心接口 `cqnews.web.sdo.com` 获取活动节庆列数据（CategoryCode=7141） |
| `Utils.cs` | 工具方法：`EachDay` 遍历日期范围内的每一天（用于跨天活动展开），`OpenUrl` 安全打开外部链接 |
| `ConfigWindow.*.cs` | 设置窗口使用 partial class 拆分为三个文件，分别负责窗口入口/Tab 布局、设置选项和已完成活动管理（含点击活动名称跳转活动详情网页） |
| `Attributes/` | 命令管理框架：通过 `[Command]`、`[Aliases]`、`[HelpMessage]` 特性标注方法，`PluginCommandManager<T>` 反射注册和注销命令 |

## 维护说明

### 本地构建

前提：[.NET 10 SDK](https://dotnet.microsoft.com/download)，XIVLauncher / Dalamud 已安装（用于运行插件）

构建：

```powershell
dotnet build EventyCN\EventyCN.csproj -c Release
```

安装（开发者模式）：

1. 构建后获取 `EventyCN.dll`（位于 `bin\x64\Release\`）
2. 在 Dalamud 设置 → Experimental → Dev Plugin Locations 中添加 DLL 所在文件夹路径
3. 在 Dalamud 插件管理器的 Dev Tools 中启用已加载的开发插件

### 活动数据来源

活动数据来自官方活动中心：

- 页面地址：`https://actff1.web.sdo.com/Project/20181018ffactive/index.html`
- 数据接口：`https://cqnews.web.sdo.com/api/news/newsList?gameCode=ff&CategoryCode=7141&pageIndex=0&pageSize=50`
- 请求方式：HTTP GET（不带 `callback` 参数时返回纯 JSON）
- 返回格式：JSON，包含 `Code`（`"0"` 为成功）和 `Data`（活动数组）

每个活动包含以下字段：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | long | 活动唯一 ID |
| `Title` | string | 活动名称 |
| `Summary` | string | 活动摘要，包含活动时间文本（如「活动时间：2026年10月7日16:00～2026年10月27日22:59」） |
| `OutLink` | string | 活动详情页面链接 |
| `HomeImagePath` | string | 活动图片 URL |
| `PublishDate` | string | 发布日期 |
| `SortIndex` | int | 排序权重 |

### 数据加载机制

1. 插件启动时一次性拉取全部活动数据（不按月分页）
2. 从 `Summary` 字段中通过正则解析活动开始/结束时间
3. 跨天活动会在其持续期间的每一天都显示
4. 颜色由预定义颜色池按序分配（新数据源不提供颜色字段）
5. 数据加载完成后自动刷新服务器栏

### 开发约定

- **Dalamud 服务注入**：使用 `[PluginService]` 特性静态注入 `IDalamudPluginInterface`、`ICommandManager`、`IPluginLog` 等 Dalamud 服务
- **命令注册**：通过 `[Command]` / `[HelpMessage]` 特性标注方法，由 `PluginCommandManager<Plugin>` 反射注册，避免手动 `AddHandler`
- **配置修改**：直接修改 `Configuration` 字段后调用 `Save()`，设置窗口中统一使用 `changed` 标志位批量保存
- **颜色处理**：预定义颜色池通过 `Helper.HexToUint` 转换为 ImGui 使用的 ABGR uint 格式，同时生成半透明版本用于非当月日期
- **可见性**：`ParsedEvent` 为 `struct`，`ActiveCenterEvent` 为 API 反序列化类

### Credits

- 原版插件 [Eventy](https://github.com/Infiziert90/Eventy) 由 [Infi](https://github.com/Infiziert90) 开发
- 活动数据来自官方活动中心（[actff1.web.sdo.com](https://actff1.web.sdo.com/Project/20181018ffactive/index.html)）

### Icon Credit (Modified)

<a href="https://www.flaticon.com/free-icons/calendar" title="calendar icons">Calendar icons created by DinosoftLabs - Flaticon</a>

## License

[MIT](./LICENSE)
