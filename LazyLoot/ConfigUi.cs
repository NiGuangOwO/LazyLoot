using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Numerics;

namespace LazyLoot;

public class ConfigUi : Window, IDisposable
{
    internal WindowSystem windowSystem = new();

    public ConfigUi() : base("Lazy Loot 配置")
    {
        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(400, 200),
            MaximumSize = new Vector2(99999, 99999),
        };
        windowSystem.AddWindow(this);
        Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
    }

    private int debugValue = 0;
    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        GC.SuppressFinalize(this);
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("配置"))
        {
            if (ImGui.BeginTabItem("功能"))
            {
                ImGui.BeginChild("generalFeatures");

                DrawFeatures();
                ImGui.Separator();
                DrawRollingDelay();
                ImGui.Separator();
                DrawChatAndToast();
                ImGui.Separator();
                DrawFulf();
                ImGui.Separator();
                DrawDiagnostics();

                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("用户限制"))
            {
                ImGui.BeginChild("generalFeatures");

                DrawUserRestriction();

                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("关于"))
            {
                PunishLib.ImGuiMethods.AboutTab.Draw("LazyLoot");
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Debug"))
            {
                DrawDebug();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawDebug()
    {
        ImGui.InputInt("Debug Value Tester", ref debugValue);

        ImGui.Text($"Is Unlocked: {Roller.IsItemUnlocked((uint)debugValue)}");

        //if (ImGui.Button("Faded Copy Converter Check?"))
        //{
        //    Roller.UpdateFadedCopy((uint)debugValue, out uint nonfaded);
        //    Svc.Log.Debug($"Non-Faded is {nonfaded}");
        //}

        //if (ImGui.Button("Check all Faded Copies"))
        //{
        //    foreach (var i in Svc.Data.GetExcelSheet<Item>().Where(x => x.FilterGroup == 12 && x.ItemUICategory.Row == 94))
        //    {
        //        Roller.UpdateFadedCopy((uint)i.RowId, out uint nonfaded);
        //        Svc.Log.Debug($"{i.Name}");
        //    }
        //}
    }

    private void DrawDiagnostics()
    {
        ImGuiEx.ImGuiLineCentered("诊断模式", () => ImGuiEx.TextUnderlined("诊断与故障排除"));

        if (ImGui.Checkbox($"诊断模式", ref LazyLoot.Config.DiagnosticsMode))
            LazyLoot.Config.Save();

        ImGuiComponents.HelpMarker($"每当放弃一个道具时，都会向聊天框输出附加信息，并说明原因。这对于帮助开发人员诊断问题或了解LazyLoot为何决定放弃道具非常有用。\r\n\r\n这些信息只会显示给您，游戏中的其他人无法看到。");

        if (ImGui.Checkbox("不要放弃未能投掷的道具。", ref LazyLoot.Config.NoPassEmergency))
            LazyLoot.Config.Save();

        ImGuiComponents.HelpMarker($"通常情况下，LazyLoot会放弃投掷失败的道具。启用此选项后，就不会在这些情况下放弃。需要提醒的是，这样做可能会有奇怪的副作用，只有在出现紧急状况时才可以使用。");
    }

    public override void OnClose()
    {
        LazyLoot.Config.Save();
        Notify.Success("配置已保存");
        base.OnClose();
    }

    private static void DrawFeatures()
    {
        ImGuiEx.ImGuiLineCentered("功能标签", () => ImGuiEx.TextUnderlined("LazyLoot Roll点指令"));
        ImGui.Columns(2, null, false);
        ImGui.SetColumnWidth(0, 80);
        ImGui.Text("/lazy need");
        ImGui.NextColumn();
        ImGui.Text("需求所有道具。无法需求的则贪婪，无法贪婪的则放弃。");
        ImGui.NextColumn();
        ImGui.Text("/lazy greed");
        ImGui.NextColumn();
        ImGui.Text("贪婪所有道具。无法贪婪的则放弃。");
        ImGui.NextColumn();
        ImGui.Text("/lazy pass");
        ImGui.NextColumn();
        ImGui.Text("放弃那些还没投掷的道具。");
        ImGui.NextColumn();
        ImGui.Columns(1);
    }

    private static void DrawRollingDelay()
    {
        ImGuiEx.ImGuiLineCentered("投掷延迟标签", () => ImGuiEx.TextUnderlined("道具之间投掷间隔"));
        ImGui.SetNextItemWidth(100);

        if (ImGui.DragFloatRange2("道具之间投掷间隔", ref LazyLoot.Config.MinRollDelayInSeconds, ref LazyLoot.Config.MaxRollDelayInSeconds, 0.1f))
        {
            LazyLoot.Config.MinRollDelayInSeconds = Math.Max(LazyLoot.Config.MinRollDelayInSeconds, 0.5f);

            LazyLoot.Config.MaxRollDelayInSeconds = Math.Max(LazyLoot.Config.MaxRollDelayInSeconds, LazyLoot.Config.MinRollDelayInSeconds + 0.1f);
        }
    }

    private static void DrawUserRestriction()
    {
        ImGui.Text("此页面中的设置将应用于每一个道具，即使它们是可交易的还是不可交易的。");
        ImGui.Separator();
        ImGui.Checkbox("忽略品级在此数值以下的道具", ref LazyLoot.Config.RestrictionIgnoreItemLevelBelow);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(50);
        ImGui.DragInt("###RestrictionIgnoreItemLevelBelowValue", ref LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue);
        if (LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue < 0) LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue = 0;

        ImGui.Checkbox("放弃已解锁的道具(幻卡、乐谱、陈旧的乐谱、宠物、坐骑、情感动作、发型)", ref LazyLoot.Config.RestrictionIgnoreItemUnlocked);

        if (!LazyLoot.Config.RestrictionIgnoreItemUnlocked)
        {
            ImGui.Checkbox("放弃已解锁的坐骑。", ref LazyLoot.Config.RestrictionIgnoreMounts);
            ImGui.Checkbox("放弃已解锁的宠物。", ref LazyLoot.Config.RestrictionIgnoreMinions);
            ImGui.Checkbox("放弃已解锁的鸟甲。", ref LazyLoot.Config.RestrictionIgnoreBardings);
            ImGui.Checkbox("放弃已解锁的幻卡。", ref LazyLoot.Config.RestrictionIgnoreTripleTriadCards);
            ImGui.Checkbox("放弃已解锁的情感动作和发型。", ref LazyLoot.Config.RestrictionIgnoreEmoteHairstyle);
            ImGui.Checkbox("放弃已解锁的乐谱。", ref LazyLoot.Config.RestrictionIgnoreOrchestrionRolls);
            ImGui.Checkbox("放弃已解锁的乐谱的陈旧的乐谱。", ref LazyLoot.Config.RestrictionIgnoreFadedCopy);
        }

        ImGui.Checkbox("放弃当前职业无法使用的道具。", ref LazyLoot.Config.RestrictionOtherJobItems);

        ImGui.Checkbox("不在具有周限的道具上投掷。", ref LazyLoot.Config.RestrictionWeeklyLockoutItems);

        ImGui.Checkbox("###RestrictionWeeklyLockoutItems", ref LazyLoot.Config.RestrictionLootLowerThanJobIlvl);
        ImGui.SameLine();
        ImGui.Text("投掷");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.Combo("###RestrictionLootLowerThanJobIlvlRollState", ref LazyLoot.Config.RestrictionLootLowerThanJobIlvlRollState, new string[] { "贪婪", "放弃" }, 2);
        ImGui.SameLine();
        ImGui.Text($"于那些低于你当前职业装备品级 (★ {Utils.GetPlayerIlevel()})");
        ImGui.SetNextItemWidth(50);
        ImGui.SameLine();
        ImGui.DragInt("###RestrictionLootLowerThanJobIlvlTreshold", ref LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold);
        if (LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold < 0) LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold = 0;
        ImGui.SameLine();
        ImGui.Text($"品级的道具 (\u2605 {Utils.GetPlayerIlevel()})。");
        ImGuiComponents.HelpMarker("此设置只适用于你需要的装备。");

        ImGui.Checkbox("###RestrictionLootIsJobUpgrade", ref LazyLoot.Config.RestrictionLootIsJobUpgrade);
        ImGui.SameLine();
        ImGui.Text("投掷");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.Combo("###RestrictionLootIsJobUpgradeRollState", ref LazyLoot.Config.RestrictionLootIsJobUpgradeRollState, new string[] { "贪婪", "放弃" }, 2);
        ImGui.SameLine();
        ImGui.Text($"于那些低于当前同类型装备的道具上");
        ImGuiComponents.HelpMarker("此设置只适用你需要的装备。");

        ImGui.Checkbox($"###RestrictionSeals", ref LazyLoot.Config.RestrictionSeals);
        ImGui.SameLine();
        ImGui.Text("放弃筹备稀有品价值低于");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.DragInt("###RestrictionSealsAmnt", ref LazyLoot.Config.RestrictionSealsAmnt);
        ImGui.SameLine();
        ImGui.Text($"的道具 (道具品级 {Roller.ConvertSealsToIlvl(LazyLoot.Config.RestrictionSealsAmnt)} 及以下)");
        ImGuiComponents.HelpMarker("此设置仅适用于可上交筹备稀有品的装备。");

    }

    private void DrawChatAndToast()
    {
        ImGuiEx.ImGuiLineCentered("聊天信息标签", () => ImGuiEx.TextUnderlined("投掷结果通知"));
        ImGui.Checkbox("在聊天栏中显示投掷信息。", ref LazyLoot.Config.EnableChatLogMessage);
        ImGui.Spacing();
        ImGuiEx.ImGuiLineCentered("悬浮通知标签", () => ImGuiEx.TextUnderlined("显示悬浮通知"));
        ImGuiComponents.HelpMarker("使用以下各种样式，作为悬浮通知来显示你的投掷。");
        ImGui.Checkbox("任务", ref LazyLoot.Config.EnableQuestToast);
        ImGui.SameLine();
        ImGui.Checkbox("通常", ref LazyLoot.Config.EnableNormalToast);
        ImGui.SameLine();
        ImGui.Checkbox("错误", ref LazyLoot.Config.EnableErrorToast);
    }

    private void DrawFulf()
    {
        ImGuiEx.ImGuiLineCentered("FULFLabel", () => ImGuiEx.TextUnderlined("梦幻终极懒人功能"));

        ImGui.TextWrapped($"梦幻终极懒人功能(FULF)是一个设置即忘功能，它将自动为您Roll点，而不必使用上面的命令。");
        ImGui.Separator();
        ImGui.Columns(2, null, false);
        ImGui.SetColumnWidth(0, 80);
        ImGui.Text("/fulf need");
        ImGui.NextColumn();
        ImGui.Text("设置 FULF 为需求模式，它会遵循/lzay need规则。");
        ImGui.NextColumn();
        ImGui.Text("/fulf greed");
        ImGui.NextColumn();
        ImGui.Text("设置 FULF 为贪婪模式，它会遵循/lazy greed规则。");
        ImGui.NextColumn();
        ImGui.Text("/fulf pass");
        ImGui.NextColumn();
        ImGui.Text("设置 FULF 为放弃模式，它会遵循/lazy pass规则。");
        ImGui.NextColumn();
        ImGui.Columns(1);
        ImGui.Separator();
        ImGui.Checkbox("###FulfEnabled", ref LazyLoot.Config.FulfEnabled);
        ImGui.SameLine();
        ImGui.TextColored(LazyLoot.Config.FulfEnabled ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed, LazyLoot.Config.FulfEnabled ? "FULF 启用" : "FULF 禁用");

        ImGui.SetNextItemWidth(100);

        if (ImGui.Combo("投掷选项", ref LazyLoot.Config.FulfRoll, new string[] { "需求", "贪婪", "放弃" }, 3))
        {
            LazyLoot.Config.Save();
        }

        ImGui.Text("第一次投掷的延迟范围 (秒)");
        ImGui.SetNextItemWidth(100);
        ImGui.DragFloat("最小秒数。 ", ref LazyLoot.Config.FulfMinRollDelayInSeconds, 0.1F);

        if (LazyLoot.Config.FulfMinRollDelayInSeconds >= LazyLoot.Config.FulfMaxRollDelayInSeconds)
        {
            LazyLoot.Config.FulfMinRollDelayInSeconds = LazyLoot.Config.FulfMaxRollDelayInSeconds - 0.1f;
        }

        if (LazyLoot.Config.FulfMinRollDelayInSeconds < 1.5f)
        {
            LazyLoot.Config.FulfMinRollDelayInSeconds = 1.5f;
        }

        ImGui.SetNextItemWidth(100);
        ImGui.DragFloat("最大秒数。 ", ref LazyLoot.Config.FulfMaxRollDelayInSeconds, 0.1F);

        if (LazyLoot.Config.FulfMaxRollDelayInSeconds <= LazyLoot.Config.FulfMinRollDelayInSeconds)
        {
            LazyLoot.Config.FulfMaxRollDelayInSeconds = LazyLoot.Config.FulfMinRollDelayInSeconds + 0.1f;
        }
    }
}
