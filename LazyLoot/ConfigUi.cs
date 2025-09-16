using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using PunishLib.ImGuiMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace LazyLoot;

public class ConfigUi : Window, IDisposable
{
    private static string clipboardText;
    private static int debugValue;
    private static string searchResultsQuery;
    private static double lastSearchTime;
    private static Item[] itemSearchResults;
    private static ContentFinderCondition[] dutySearchResults;

    internal WindowSystem windowSystem = new();

    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    public struct DebugLootItem
    {
        [FieldOffset(0x00)] public uint ChestObjectId;
        [FieldOffset(0x04)] public uint ChestItemIndex; // This loot item's index in the chest it came from
        [FieldOffset(0x08)] public uint ItemId;
        [FieldOffset(0x0C)] public ushort ItemCount;

        [FieldOffset(0x1C)] public uint GlamourItemId;
        [FieldOffset(0x20)] public RollState RollState;
        [FieldOffset(0x24)] public RollResult RollResult;
        [FieldOffset(0x28)] public byte RollValue;
        [FieldOffset(0x34)] public byte Unk1;
        [FieldOffset(0x38)] public byte Unk2;
        [FieldOffset(0x2C)] public float Time;
        [FieldOffset(0x30)] public float MaxTime;

        [FieldOffset(0x38)] public LootMode LootMode;
    }

    public ConfigUi() : base("Lazy Loot 配置")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 200),
            MaximumSize = new Vector2(99999, 99999)
        };
        windowSystem.AddWindow(this);
        Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
    }

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
                DrawFeatures();
                ImGui.Separator();
                DrawRollingDelay();
                ImGui.Separator();
                DrawChatAndToast();
                ImGui.Separator();
                DrawFulf();
                ImGui.Separator();
                DrawDiagnostics();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("用户限制"))
            {
                DrawUserRestriction();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("关于"))
            {
                AboutTab.Draw("LazyLoot");
                ImGui.EndTabItem();
            }

#if DEBUG
            if (ImGui.BeginTabItem("Debug"))
            {
                DrawDebug();
                ImGui.EndTabItem();
            }
#endif

            ImGui.EndTabBar();
        }
    }

    private static IDalamudTextureWrap? GetItemIcon(uint id)
    {
        return Svc.Texture.GetFromGameIcon(new GameIconLookup
        {
            IconId = id
        }).GetWrapOrDefault();
    }

    private unsafe void DrawDebug()
    {
        if (ImGui.CollapsingHeader("Is Item Unlocked?"))
        {
            ImGui.InputInt("Debug Value Tester", ref debugValue);
            ImGui.Text($"Is Unlocked: {Roller.IsItemUnlocked((uint)debugValue)}");
        }

        if (ImGui.CollapsingHeader("Loot"))
        {
            var loot = Loot.Instance();
            if (loot != null)
            {
                foreach (var item in loot->Items)
                {
                    if (item.ItemId == 0)
                        continue;
                    var casted = (DebugLootItem*)&item;
                    ImGui.PushID($"{casted->ItemId}");
                    Dalamud.Utility.Util.ShowStruct(casted);
                }
            }
        }

        //if (ImGui.Button("Faded Copy Converter Check?"))
        //{
        //    Roller.UpdateFadedCopy((uint)debugValue, out uint nonfaded);
        //    Svc.Log.Debug($"Non-Faded is {nonfaded}");\
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
        ImGuiEx.LineCentered("诊断模式", () => ImGuiEx.TextUnderlined("诊断与故障排除"));

        if (ImGui.Checkbox("诊断模式", ref LazyLoot.Config.DiagnosticsMode))
            LazyLoot.Config.Save();

        ImGuiComponents.HelpMarker(
            "每当放弃一个道具时，都会向聊天框输出附加信息，并说明原因。这对于帮助开发人员诊断问题或了解LazyLoot为何决定放弃道具非常有用。\r\n\r\n这些信息只会显示给您，游戏中的其他人无法看到。");

        if (ImGui.Checkbox("不要放弃未能投掷的道具", ref LazyLoot.Config.NoPassEmergency))
            LazyLoot.Config.Save();

        ImGuiComponents.HelpMarker(
            "通常情况下，LazyLoot会放弃投掷失败的道具。启用此选项后，就不会在这些情况下放弃。需要提醒的是，这样做可能会有奇怪的副作用，只有在出现紧急状况时才可以使用。");
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
        ImGui.Columns(2, ImU8String.Empty, false);
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
        ImGuiEx.LineCentered("投掷延迟标签", () => ImGuiEx.TextUnderlined("道具之间投掷间隔"));
        ImGui.SetNextItemWidth(100);

        if (ImGui.DragFloatRange2("道具之间投掷间隔", ref LazyLoot.Config.MinRollDelayInSeconds,
                ref LazyLoot.Config.MaxRollDelayInSeconds, 0.1f))
        {
            LazyLoot.Config.MinRollDelayInSeconds = Math.Max(LazyLoot.Config.MinRollDelayInSeconds, 0.5f);

            LazyLoot.Config.MaxRollDelayInSeconds = Math.Max(LazyLoot.Config.MaxRollDelayInSeconds,
                LazyLoot.Config.MinRollDelayInSeconds + 0.1f);
        }
    }

    private static void DrawUserRestrictionEverywhere()
    {
        ImGui.Text("此页面中的设置将应用于每一个道具，即使它们是可交易的还是不可交易的。");
        ImGui.Separator();
        ImGui.Checkbox("忽略品级在此数值以下的道具",
            ref LazyLoot.Config.RestrictionIgnoreItemLevelBelow);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(50);
        ImGui.DragInt("###RestrictionIgnoreItemLevelBelowValue",
            ref LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue);
        if (LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue < 0)
            LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue = 0;

        ImGui.Checkbox(
            "放弃已解锁的道具(幻卡、乐谱、陈旧的乐谱、宠物、坐骑、情感动作、发型)",
            ref LazyLoot.Config.RestrictionIgnoreItemUnlocked);

        if (!LazyLoot.Config.RestrictionIgnoreItemUnlocked)
        {
            ImGui.Checkbox("放弃已解锁的坐骑", ref LazyLoot.Config.RestrictionIgnoreMounts);
            ImGui.Checkbox("放弃已解锁的宠物", ref LazyLoot.Config.RestrictionIgnoreMinions);
            ImGui.Checkbox("放弃已解锁的鸟甲", ref LazyLoot.Config.RestrictionIgnoreBardings);
            ImGui.Checkbox("放弃已解锁的幻卡",
                ref LazyLoot.Config.RestrictionIgnoreTripleTriadCards);
            ImGui.Checkbox("放弃已解锁的情感动作和发型",
                ref LazyLoot.Config.RestrictionIgnoreEmoteHairstyle);
            ImGui.Checkbox("放弃已解锁的乐谱",
                ref LazyLoot.Config.RestrictionIgnoreOrchestrionRolls);
            ImGui.Checkbox("放弃已解锁的乐谱的陈旧的乐谱", ref LazyLoot.Config.RestrictionIgnoreFadedCopy);
        }

        ImGui.Checkbox("放弃当前职业无法使用的道具",
            ref LazyLoot.Config.RestrictionOtherJobItems);

        ImGui.Checkbox("不在具有周限的道具上投掷",
            ref LazyLoot.Config.RestrictionWeeklyLockoutItems);

        ImGui.Checkbox("###RestrictionWeeklyLockoutItems", ref LazyLoot.Config.RestrictionLootLowerThanJobIlvl);
        ImGui.SameLine();
        ImGui.Text("投掷");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.Combo("###RestrictionLootLowerThanJobIlvlRollState",
            ref LazyLoot.Config.RestrictionLootLowerThanJobIlvlRollState, new[] { "贪婪", "放弃" }, 2);
        ImGui.SameLine();
        ImGui.Text($"于那些低于你当前职业装备品级 (★ {Utils.GetPlayerIlevel()})");
        ImGui.SetNextItemWidth(50);
        ImGui.SameLine();
        ImGui.DragInt("###RestrictionLootLowerThanJobIlvlTreshold",
            ref LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold);
        if (LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold < 0)
            LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold = 0;
        ImGui.SameLine();
        ImGui.Text($"品级的道具 (\u2605 {Utils.GetPlayerIlevel()})");
        ImGuiComponents.HelpMarker("此设置只适用于你需要的装备。");

        ImGui.Checkbox("###RestrictionLootIsJobUpgrade", ref LazyLoot.Config.RestrictionLootIsJobUpgrade);
        ImGui.SameLine();
        ImGui.Text("投掷");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.Combo("###RestrictionLootIsJobUpgradeRollState",
            ref LazyLoot.Config.RestrictionLootIsJobUpgradeRollState,
            new[] { "贪婪", "放弃" }, 2);
        ImGui.SameLine();
        ImGui.Text("于那些低于当前同类型装备的道具上");
        ImGuiComponents.HelpMarker("此设置只适用你需要的装备。");

        ImGui.Checkbox("###RestrictionSeals", ref LazyLoot.Config.RestrictionSeals);
        ImGui.SameLine();
        ImGui.Text("放弃筹备稀有品价值低于");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.DragInt("###RestrictionSealsAmnt", ref LazyLoot.Config.RestrictionSealsAmnt);
        ImGui.SameLine();
        ImGui.Text($"的道具 (道具品级 {Roller.ConvertSealsToIlvl(LazyLoot.Config.RestrictionSealsAmnt)} 及以下)");
        ImGuiComponents.HelpMarker(
            "此设置仅适用于可上交筹备稀有品的装备。");
    }

    private static void CenterText()
    {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetColumnWidth() - ImGui.GetFrameHeight()) * 0.5f);
    }

    private static ImTextureID GetDutyIcon(ContentFinderCondition duty)
    {
        var icon = duty is { HighEndDuty: true, ContentType.Value.RowId: 5 }
            ? Svc.Data.GetExcelSheet<ContentType>()
                .FirstOrDefault(x => x.RowId == 28).Icon
            : duty.ContentType.Value.Icon;
        if (icon == 0)
        {
            return 0;
        }

        var itemIcon = GetItemIcon(icon);
        return itemIcon?.Handle ?? default;
    }

    private static void DrawUserRestrictionItems()
    {
        ImGuiEx.LineCentered("ItemRestrictionWarning",
            () => ImGui.TextColored(ImGuiColors.DalamudYellow, "These rules override any other restriction settings"));
        ImGui.Separator();

        if (ImGui.BeginTable("UserRestrictionItemsTable", 8, ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 32f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Need", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Greed", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Pass", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Nothing", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 60f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < LazyLoot.Config.Restrictions.Items.Count; i++)
            {
                var item = LazyLoot.Config.Restrictions.Items[i];
                var restrictedItem = Svc.Data.GetExcelSheet<Item>().GetRow(item.Id);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                var enabled = item.Enabled;
                CenterText();
                if (ImGui.Checkbox($"##{item.Id}", ref enabled))
                {
                    item.Enabled = enabled;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();

                var icon = GetItemIcon(restrictedItem.Icon);
                if (icon != null)
                    ImGui.Image(icon.Handle, new Vector2(24, 24));
                else
                    ImGui.Text("-");

                ImGui.TableNextColumn();
                ImGui.Text(restrictedItem.Name.ToString());
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(restrictedItem.Name.ToString());

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##need{item.Id}", item.RollRule == RollResult.Needed))
                {
                    item.RollRule = RollResult.Needed;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##greed{item.Id}", item.RollRule == RollResult.Greeded))
                {
                    item.RollRule = RollResult.Greeded;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##pass{item.Id}", item.RollRule == RollResult.Passed))
                {
                    item.RollRule = RollResult.Passed;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##doNothing{item.Id}", item.RollRule == RollResult.UnAwarded))
                {
                    item.RollRule = RollResult.UnAwarded;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                if (ImGui.Button($"Remove##{item.Id}"))
                {
                    LazyLoot.Config.Restrictions.Items.RemoveAt(i);
                    LazyLoot.Config.Save();
                    break;
                }
            }

            ImGui.EndTable();
        }

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2, 0));
        if (ImGui.Button("Export", new Vector2(60, 0)))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(LazyLoot.Config.Restrictions.Items);
            ImGui.SetClipboardText(json);
            Notify.Success("Item Restrictions settings copied to clipboard!");
        }

        ImGui.SameLine();
        if (ImGui.Button("Import", new Vector2(60, 0)))
        {
            try
            {
                clipboardText = ImGui.GetClipboardText();
                if (string.IsNullOrEmpty(clipboardText))
                {
                    Notify.Error("Nothing to import on your clipboard");
                    return;
                }

                ImGui.OpenPopup("import_item_confirmation");
            }
            catch
            {
                Notify.Error("Failed to import item restriction settings - invalid format");
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Add Item", new Vector2(-1, 0)))
        {
            searchResultsQuery = "";
            ImGui.OpenPopup("item_search_add");
        }

        ImGui.PopStyleVar();

        var itemSheet = Svc.Data.GetExcelSheet<Item>();

        if (ImGui.BeginPopup("import_item_confirmation", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Are you sure you want to replace your current item restrictions configuration?");
            ImGuiEx.LineCentered(() => ImGuiEx.TextUnderlined("This action cannot be undone."));
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(40 / 255f, 167 / 255f, 69 / 255f, 1.0f));
            if (ImGui.Button("YES", new Vector2(100f, 0)))
            {
                var items = System.Text.Json.JsonSerializer.Deserialize<List<CustomRestriction>>(clipboardText);
                if (items != null)
                {
                    LazyLoot.Config.Restrictions.Items = items;
                    LazyLoot.Config.Save();
                    Notify.Success("Imported Item Restrictions successfully!");
                }

                ImGui.CloseCurrentPopup();
            }

            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(220 / 255f, 53 / 255f, 69 / 255f, 1.0f));
            if (ImGui.Button("NO", new Vector2(-1, 0)))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.PopStyleColor();
            ImGui.EndPopup();
        }

        if (ImGui.BeginPopup("item_search_add"))
        {
            ImGui.Text("Search for item:");
            var currentTime = ImGui.GetTime();
            if (ImGui.GetTime() > lastSearchTime + 0.1f)
            {
                lastSearchTime = currentTime;
                itemSearchResults = !string.IsNullOrEmpty(searchResultsQuery)
                    ? uint.TryParse(searchResultsQuery, out var searchId)
                        ? itemSheet.Where(x =>
                                x.RowId == searchId &&
                                LazyLoot.Config.Restrictions.Items.All(i => i.Id != x.RowId))
                            .Take(20)
                            .ToArray()
                        : itemSheet.Where(x =>
                                x.Name.ToString().Contains(searchResultsQuery, StringComparison.OrdinalIgnoreCase) &&
                                LazyLoot.Config.Restrictions.Items.All(i => i.Id != x.RowId))
                            .Take(20)
                            .ToArray()
                    : [];
            }

            var maxWidth = Math.Max(300f, itemSearchResults.Select(item =>
                ImGui.CalcTextSize($"{item.Name} (ID: {item.RowId})").X).DefaultIfEmpty(200f).Max() + 30);
            ImGui.SetNextItemWidth(maxWidth);
            ImGui.InputText("##itemSearch", ref searchResultsQuery, 100);
            if (!string.IsNullOrEmpty(searchResultsQuery))
                if (ImGui.BeginChild("itemSearchResults", new Vector2(maxWidth, 200), true))
                {
                    foreach (var item in itemSearchResults)
                    {
                        var icon = GetItemIcon(item.Icon);
                        if (icon != null)
                        {
                            ImGui.Image(icon.Handle, new Vector2(16, 16));
                            ImGui.SameLine();
                        }

                        if (!ImGui.Selectable($" {item.Name} (ID: {item.RowId})"))
                            continue;
                        LazyLoot.Config.Restrictions.Items.Add(new CustomRestriction
                        {
                            Id = itemSheet.GetRow(item.RowId).RowId,
                            Enabled = true,
                            RollRule = RollResult.UnAwarded
                        });
                        LazyLoot.Config.Save();
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndChild();
                }

            ImGui.EndPopup();
        }
    }

    private static void DrawUserRestrictionDuties()
    {
        ImGuiEx.LineCentered("ItemRestrictionWarning",
            () =>
            {
                var width = ImGui.GetWindowWidth() - 30;
                ImGui.PushTextWrapPos(width);
                ImGui.TextColored(ImGuiColors.DalamudYellow,
                    "These rules override the main restriction settings, but is overriden by the item restriction settings if they happen to collide.");
                ImGui.PopTextWrapPos();
            });
        ImGui.Separator();

        if (ImGui.BeginTable("UserRestrictionDutiesTable", 8, ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 32f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Need", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Greed", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Pass", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Nothing", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 60f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < LazyLoot.Config.Restrictions.Duties.Count; i++)
            {
                var duty = LazyLoot.Config.Restrictions.Duties[i];
                var restrictedDuty = Svc.Data.GetExcelSheet<ContentFinderCondition>().GetRow(duty.Id);
                var enabled = duty.Enabled;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.Checkbox($"##{duty.Id}", ref enabled))
                {
                    duty.Enabled = enabled;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();

                ImGui.Image(GetDutyIcon(restrictedDuty), new Vector2(24, 24));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip((restrictedDuty is { HighEndDuty: true, ContentType.Value.RowId: 5 }
                        ? Svc.Data.GetExcelSheet<ContentType>()
                            .FirstOrDefault(x => x.RowId == 28).Name
                        : restrictedDuty.ContentType.Value.Name).ToString());

                ImGui.TableNextColumn();
                ImGui.Text(restrictedDuty.Name.ToString());
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(restrictedDuty.Name.ToString());

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##need{duty.Id}", duty.RollRule == RollResult.Needed))
                {
                    duty.RollRule = RollResult.Needed;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##greed{duty.Id}", duty.RollRule == RollResult.Greeded))
                {
                    duty.RollRule = RollResult.Greeded;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##pass{duty.Id}", duty.RollRule == RollResult.Passed))
                {
                    duty.RollRule = RollResult.Passed;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##doNothing{duty.Id}", duty.RollRule == RollResult.UnAwarded))
                {
                    duty.RollRule = RollResult.UnAwarded;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                if (ImGui.Button($"Remove##{duty.Id}"))
                {
                    LazyLoot.Config.Restrictions.Duties.RemoveAt(i);
                    LazyLoot.Config.Save();
                    break;
                }
            }

            ImGui.EndTable();
        }

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2, 0));
        if (ImGui.Button("Export", new Vector2(60, 0)))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(LazyLoot.Config.Restrictions.Duties);
            ImGui.SetClipboardText(json);
            Notify.Success("Duty Restrictions copied to clipboard!");
        }

        ImGui.SameLine();
        if (ImGui.Button("Import", new Vector2(60, 0)))
        {
            try
            {
                clipboardText = ImGui.GetClipboardText();
                if (string.IsNullOrEmpty(clipboardText))
                {
                    Notify.Error("Nothing to import on your clipboard");
                    return;
                }

                ImGui.OpenPopup("import_duty_confirmation");
            }
            catch
            {
                Notify.Error("Failed to import duty restriction settings - invalid format");
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Add Duty", new Vector2(-1, 0)))
        {
            searchResultsQuery = "";
            ImGui.OpenPopup("duty_search_add");
        }

        ImGui.PopStyleVar();

        var dutySheet = Svc.Data.GetExcelSheet<ContentFinderCondition>();

        if (ImGui.BeginPopup("import_duty_confirmation", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Are you sure you want to replace your current duty restrictions configuration?");
            ImGuiEx.LineCentered(() => ImGuiEx.TextUnderlined("This action cannot be undone."));
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(40 / 255f, 167 / 255f, 69 / 255f, 1.0f));
            if (ImGui.Button("YES", new Vector2(100f, 0)))
            {
                var duties = System.Text.Json.JsonSerializer.Deserialize<List<CustomRestriction>>(clipboardText);
                if (duties != null)
                {
                    LazyLoot.Config.Restrictions.Duties = duties;
                    LazyLoot.Config.Save();
                    Notify.Success("Imported Duty Restrictions successfully!");
                }

                ImGui.CloseCurrentPopup();
            }

            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(220 / 255f, 53 / 255f, 69 / 255f, 1.0f));
            if (ImGui.Button("NO", new Vector2(-1, 0)))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.PopStyleColor();
            ImGui.EndPopup();
        }

        if (ImGui.BeginPopup("duty_search_add"))
        {
            ImGui.Text("Search for duty:");
            var currentTime = ImGui.GetTime();
            if (ImGui.GetTime() > lastSearchTime + 0.1f)
            {
                lastSearchTime = currentTime;
                dutySearchResults = !string.IsNullOrEmpty(searchResultsQuery)
                    ? uint.TryParse(searchResultsQuery, out var searchId)
                        ? dutySheet.Where(x =>
                                x.RowId == searchId &&
                                LazyLoot.Config.Restrictions.Items.All(i => i.Id != x.RowId))
                            .Take(20)
                            .ToArray()
                        : dutySheet.Where(x =>
                                x.Name.ToString().Contains(searchResultsQuery,
                                    StringComparison.OrdinalIgnoreCase) &&
                                LazyLoot.Config.Restrictions.Items.All(i => i.Id != x.RowId))
                            .Take(20)
                            .ToArray()
                    : [];
            }

            var maxWidth = Math.Max(300f, dutySearchResults.Select(duty =>
                    ImGui.CalcTextSize($"{duty.Name} (ID: {duty.RowId})").X).DefaultIfEmpty(200f)
                .Max() + 30);
            ImGui.SetNextItemWidth(maxWidth);
            ImGui.InputText("##dutySearch", ref searchResultsQuery, 100);
            if (!string.IsNullOrEmpty(searchResultsQuery))
                if (ImGui.BeginChild("dutySearchResults", new Vector2(maxWidth, 200), true))
                {
                    foreach (var duty in dutySearchResults)
                    {
                        ImGui.Image(GetDutyIcon(duty), new Vector2(16, 16));
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Test");
                        ImGui.SameLine();
                        if (!ImGui.Selectable($" {duty.Name} (ID: {duty.RowId})"))
                            continue;
                        LazyLoot.Config.Restrictions.Duties.Add(new CustomRestriction
                        {
                            Id = dutySheet.GetRow(duty.RowId).RowId,
                            Enabled = true,
                            RollRule = RollResult.UnAwarded
                        });
                        LazyLoot.Config.Save();
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndChild();
                }

            ImGui.EndPopup();
        }
    }

    private static void DrawUserRestriction()
    {
        if (ImGui.BeginTabBar("PerItemDutyConfigTabs"))
        {
            if (ImGui.BeginTabItem("Everywhere..."))
            {
                DrawUserRestrictionEverywhere();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("... but for these Items"))
            {
                DrawUserRestrictionItems();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("... but for these Duties"))
            {
                DrawUserRestrictionDuties();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawChatAndToast()
    {
        ImGuiEx.LineCentered("聊天信息标签", () => ImGuiEx.TextUnderlined("投掷结果通知"));
        ImGui.Checkbox("在聊天栏中显示投掷信息。", ref LazyLoot.Config.EnableChatLogMessage);
        ImGui.Spacing();
        ImGuiEx.LineCentered("悬浮通知标签", () => ImGuiEx.TextUnderlined("显示悬浮通知"));
        ImGuiComponents.HelpMarker("使用以下各种样式，作为悬浮通知来显示你的投掷。");
        ImGui.Checkbox("任务", ref LazyLoot.Config.EnableQuestToast);
        ImGui.SameLine();
        ImGui.Checkbox("通常", ref LazyLoot.Config.EnableNormalToast);
        ImGui.SameLine();
        ImGui.Checkbox("错误", ref LazyLoot.Config.EnableErrorToast);
    }

    private void DrawFulf()
    {
        ImGuiEx.LineCentered("FULFLabel", () => ImGuiEx.TextUnderlined("梦幻终极懒人功能"));

        ImGui.TextWrapped(
            "梦幻终极懒人功能(FULF)是一个设置即忘功能，它将自动为您Roll点，而不必使用上面的命令。");
        ImGui.Separator();
        ImGui.Columns(2, ImU8String.Empty, false);
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
        ImGui.TextColored(LazyLoot.Config.FulfEnabled ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed,
            LazyLoot.Config.FulfEnabled ? "FULF 启用" : "FULF 禁用");

        ImGui.SetNextItemWidth(100);

        if (ImGui.Combo("投掷选项", ref LazyLoot.Config.FulfRoll, new[] { "需求", "贪婪", "放弃" }, 3))
            LazyLoot.Config.Save();

        ImGui.Text("第一次投掷的延迟范围 (秒)");
        ImGui.SetNextItemWidth(100);
        ImGui.DragFloat("最小秒数 ", ref LazyLoot.Config.FulfMinRollDelayInSeconds, 0.1F);

        if (LazyLoot.Config.FulfMinRollDelayInSeconds >= LazyLoot.Config.FulfMaxRollDelayInSeconds)
            LazyLoot.Config.FulfMinRollDelayInSeconds = LazyLoot.Config.FulfMaxRollDelayInSeconds - 0.1f;

        if (LazyLoot.Config.FulfMinRollDelayInSeconds < 1.5f)
            LazyLoot.Config.FulfMinRollDelayInSeconds = 1.5f;

        ImGui.SetNextItemWidth(100);
        ImGui.DragFloat("最大秒数 ", ref LazyLoot.Config.FulfMaxRollDelayInSeconds, 0.1F);

        if (LazyLoot.Config.FulfMaxRollDelayInSeconds <= LazyLoot.Config.FulfMinRollDelayInSeconds)
            LazyLoot.Config.FulfMaxRollDelayInSeconds = LazyLoot.Config.FulfMinRollDelayInSeconds + 0.1f;
    }
}