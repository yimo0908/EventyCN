namespace EventyCN.Windows.Config;

public partial class ConfigWindow
{
    private void Completed()
    {
        using var tabItem = ImRaii.TabItem("已完成");
        if (!tabItem.Success)
            return;

        var changed = false;

        changed |= ImGui.Checkbox("显示已完成活动", ref Plugin.Configuration.ShowCompletedEvents);

        var date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
        if (!Plugin.Events.TryGetValue(date.Ticks, out var events))
        {
            Helper.WrappedTextWithColor(ImGuiColors.DalamudViolet, "当前无活动。");
            return;
        }

        using var table = ImRaii.Table("EventList", 2, ImGuiTableFlags.BordersInner);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("活动");
        var rowHeaderText = "已完成?";
        var width = ImGui.CalcTextSize(rowHeaderText).X + (ImGui.GetStyle().ItemInnerSpacing.X * 2);
        ImGui.TableSetupColumn(rowHeaderText, ImGuiTableColumnFlags.WidthFixed, width);

        ImGui.TableHeadersRow();
        foreach (var ev in events)
        {
            ImGui.TableNextColumn();
            ImGui.Selectable(ev.Name);
            if (ImGui.IsItemHovered() && ev.Url != "")
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsItemClicked())
                    Utils.OpenUrl(ev.Url);
            }

            ImGui.TableNextColumn();
            var pos = ImGui.GetCursorPos();
            ImGui.SetCursorPos(pos with { X = pos.X + (ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight()) * 0.5f });

            var isCompleted = Plugin.Configuration.CompletedEvents.Contains(ev.Id);
            if (ImGui.Checkbox($"##{ev.Id}", ref isCompleted))
            {
                changed = true;
                if (isCompleted)
                    Plugin.Configuration.CompletedEvents.Add(ev.Id);
                else
                    Plugin.Configuration.CompletedEvents.Remove(ev.Id);
            }

            ImGui.TableNextRow();
        }

        if (changed)
        {
            Plugin.Configuration.Save();
            Plugin.ServerBar.Refresh();
        }
    }
}
