using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using PunishLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LazyLoot;

public class LazyLoot : IDalamudPlugin, IDisposable
{
    static readonly RollResult[] _rollArray = new RollResult[]
    {
        RollResult.Needed,
        RollResult.Greeded,
        RollResult.Passed,
    };

    public string Name => "LazyLoot";

    internal static Configuration Config;
    internal static ConfigUi ConfigUi;
    internal static IDtrBarEntry DtrEntry;

    internal static LazyLoot P;
    public LazyLoot(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        PunishLibMain.Init(pluginInterface, "LazyLoot", new AboutPlugin() { Developer = "53m1k0l0n/Gidedin", Translator = "NiGuangOwO", Afdian = "https://afdian.com/a/NiGuangOwO" });
        P = this;
#if !DEBUG
        if (Svc.PluginInterface.IsDev || !Svc.PluginInterface.SourceRepository.Contains("NiGuangOwO/DalamudPlugins"))
        {
            Svc.NotificationManager.AddNotification(new Notification()
            {
                Type = NotificationType.Error,
                Title = "加载验证",
                Content = "由于本地加载或安装来源仓库非NiGuangOwO个人仓库，插件加载失败",
            });
            return;
        }
#endif
        Config = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigUi = new ConfigUi();
        DtrEntry ??= Svc.DtrBar.Get("LazyLoot");
        DtrEntry.OnClick = new(() => CycleFulf());


        Svc.PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        Svc.Chat.CheckMessageHandled += NoticeLoot;

        Svc.Commands.AddHandler("/lazyloot", new CommandInfo(LazyCommand)
        {
            HelpMessage = "打开 Lazy Loot 配置。",
            ShowInHelp = true,
        });

        Svc.Commands.AddHandler("/lazy", new CommandInfo(LazyCommand)
        {
            HelpMessage = "默认情况下打开LazyLoot配置。添加参数 need | greed | pass 来对当前道具投掷。",
            ShowInHelp = true,
        });

        Svc.Commands.AddHandler("/fulf", new CommandInfo(FulfCommand)
        {
            HelpMessage = "通过/fulf 启用/禁用 FULF 或通过/fulf need | greed | pass 改变投掷规则。",
            ShowInHelp = true,
        });

        Svc.Framework.Update += OnFrameworkUpdate;
    }

    private void LazyCommand(string command, string arguments)
    {
        var args = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0)
        {
            OnOpenConfigUi();
            return;
        }
        else
        {
            RollingCommand(null, arguments);
            return;
        }
    }

    private void CycleFulf()
    {
        if (!Config.FulfEnabled)
        {
            Config.FulfEnabled = true;
            Config.FulfRoll = 2;
        }
        else
        {
            Config.FulfRoll = Config.FulfRoll switch
            {
                0 => 2,
                1 => 0,
                2 => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(Config.FulfRoll)),
            };

            if (Config.FulfRoll == 2)
                Config.FulfEnabled = false;
        }

        Config.Save();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
            return;
#if !DEBUG
        if (Svc.PluginInterface.IsDev || !Svc.PluginInterface.SourceRepository.Contains("NiGuangOwO/DalamudPlugins"))
        {
            ECommonsMain.Dispose();
            PunishLibMain.Dispose();
            return;
        }
#endif
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        Svc.Chat.CheckMessageHandled -= NoticeLoot;

        Svc.Commands.RemoveHandler("/lazyloot");
        Svc.Commands.RemoveHandler("/lazy");
        Svc.Commands.RemoveHandler("/fulf");

        ECommonsMain.Dispose();
        PunishLibMain.Dispose();
        Svc.Log.Information(">>Stop LazyLoot<<");
        DtrEntry.Remove();

        Svc.Framework.Update -= OnFrameworkUpdate;
        Config.Save();
    }

    private void FulfCommand(string command, string arguments)
    {
        var res = GetResult(arguments);
        if (res.HasValue)
        {
            Config.FulfRoll = res.Value;
        }
        else
        {
            Config.FulfEnabled = !Config.FulfEnabled;
        }
    }

    private void RollingCommand(string command, string arguments)
    {
        var res = GetResult(arguments);
        if (res.HasValue)
        {
            _rollOption = _rollArray[res.Value % 3];
        }
    }

    private int? GetResult(string str)
    {
        if (str.Contains("need", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }
        else if (str.Contains("greed", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }
        else if (str.Contains("pass", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }
        return null;
    }

    private void OnOpenConfigUi()
    {
        ConfigUi.IsOpen = !ConfigUi.IsOpen;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (Config.FulfEnabled)
        {
            string fulfMode = Config.FulfRoll switch
            {
                0 => "需求",
                1 => "贪婪",
                2 => "放弃"
            };

            DtrEntry.Text = new SeString(
                new IconPayload(BitmapFontIcon.Dice),
                new TextPayload(fulfMode));

        }
        else
        {
            DtrEntry.Text = new SeString(
            new IconPayload(BitmapFontIcon.Dice),
            new TextPayload("FULF 禁用"));
        }

        DtrEntry.Shown = true;

        //Not sure why the below line is here? You can only roll on loot in duties anyway, plus it helps when SE changes which flag a duty has (such as Keeper of the Lake using BoundByDuty56)
        //if (!Svc.Condition[ConditionFlag.BoundByDuty]) return;
        RollLoot();
    }

    static DateTime _nextRollTime = DateTime.Now;
    static RollResult _rollOption = RollResult.UnAwarded;
    static int _need = 0, _greed = 0, _pass = 0;
    private static void RollLoot()
    {
        if (_rollOption == RollResult.UnAwarded) return;
        if (DateTime.Now < _nextRollTime) return;

        //No rolling in cutscene.
        if (Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]) return;

        _nextRollTime = DateTime.Now.AddMilliseconds(Math.Max(1500, new Random()
            .Next((int)(Config.MinRollDelayInSeconds * 1000),
            (int)(Config.MaxRollDelayInSeconds * 1000))));

        try
        {
            if (!Roller.RollOneItem(_rollOption, ref _need, ref _greed, ref _pass))//Finish the loot
            {
                ShowResult(_need, _greed, _pass);
                _need = _greed = _pass = 0;
                _rollOption = RollResult.UnAwarded;
                Roller.Clear();
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Something Wrong with rolling!");
        }
    }

    private static void ShowResult(int need, int greed, int pass)
    {
        SeString seString = new(new List<Payload>()
        {
            new TextPayload("需求 "),
            new UIForegroundPayload(575),
            new TextPayload(need.ToString()),
            new UIForegroundPayload(0),
            new TextPayload(" 件 " +"， 贪婪 "),
            new UIForegroundPayload(575),
            new TextPayload(greed.ToString()),
            new UIForegroundPayload(0),
            new TextPayload(" 件 " + "， 放弃 "),
            new UIForegroundPayload(575),
            new TextPayload(pass.ToString()),
            new UIForegroundPayload(0),
            new TextPayload(" 件 "+ "。")
        });

        if (Config.EnableChatLogMessage)
        {
            Svc.Chat.Print(seString);
        }
        if (Config.EnableErrorToast)
        {
            Svc.Toasts.ShowError(seString);
        }
        if (Config.EnableNormalToast)
        {
            Svc.Toasts.ShowNormal(seString);
        }
        if (Config.EnableQuestToast)
        {
            Svc.Toasts.ShowQuest(seString);
        }
    }

    private void NoticeLoot(XivChatType type, int senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!Config.FulfEnabled || type != (XivChatType)2105) return;

        string textValue = message.TextValue;
        if (textValue == Svc.Data.GetExcelSheet<LogMessage>()!.First(x => x.RowId == 5194).Text)
        {
            _nextRollTime = DateTime.Now.AddMilliseconds(new Random()
                .Next((int)(Config.FulfMinRollDelayInSeconds * 1000),
                (int)(Config.FulfMaxRollDelayInSeconds * 1000)));

            _rollOption = _rollArray[Config.FulfRoll];
        }
    }
}