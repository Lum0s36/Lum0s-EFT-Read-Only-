/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using ImGuiNET;
using LoneEftDmaRadar.Tarkov;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;
using LoneEftDmaRadar.Web.TarkovDev.Data;

namespace LoneEftDmaRadar.UI.Widgets
{
    public static class QuestHelperWidget
    {
        private static readonly System.Numerics.Vector4 TraderColor = new(1f, 0.84f, 0f, 1f);
        private static readonly System.Numerics.Vector4 QuestTitleColor = new(1f, 1f, 1f, 1f);
        private static readonly System.Numerics.Vector4 ObjectiveActiveColor = new(1f, 1f, 1f, 1f);
        private static readonly System.Numerics.Vector4 ObjectiveCompletedColor = new(0.2f, 0.8f, 0.2f, 1f);
        private static readonly System.Numerics.Vector4 ObjectiveLockedColor = new(0.5f, 0.5f, 0.5f, 1f);
        private static readonly System.Numerics.Vector4 ProgressColor = new(0.5f, 0.8f, 1f, 1f);
        private static readonly System.Numerics.Vector4 LocationColor = new(1f, 0.4f, 0.7f, 1f);
        private static readonly System.Numerics.Vector4 TypeIconColor = new(1f, 0.65f, 0f, 1f);
        private static readonly System.Numerics.Vector4 KeyItemColor = new(0.5f, 0.75f, 1f, 1f);

        private static QuestHelperConfig Config => Program.Config.QuestHelper;
        private static QuestManager QuestManager => Memory.QuestManager;
        private static bool InRaid => Memory.InRaid;
        private static string CurrentMapId => Memory.MapID ?? "";

        private static string _searchFilter = "";
        private static string _selectedTrader = "All";
        private static readonly string[] TraderNames = { "All", "Prapor", "Therapist", "Fence", "Skier", "Peacekeeper", "Mechanic", "Ragman", "Jaeger", "Lightkeeper" };

        /// <summary>
        /// Whether the Quest Helper Widget is visible.
        /// </summary>
        public static bool IsOpen
        {
            get => Config.ShowWidget;
            set => Config.ShowWidget = value;
        }

        /// <summary>
        /// Draw the Quest Helper Widget.
        /// </summary>
        public static void Draw()
        {
            if (!IsOpen)
                return;
            if (!Config.Enabled)
                return;

            ImGui.SetNextWindowSize(new System.Numerics.Vector2(500, 600), ImGuiCond.FirstUseEver);
            bool isOpen = IsOpen;
            if (!ImGui.Begin("Quest Helper", ref isOpen, ImGuiWindowFlags.None))
            {
                IsOpen = isOpen;
                ImGui.End();
                return;
            }
            IsOpen = isOpen;

            if (InRaid)
                DrawInRaidMode();
            else
                DrawOffRaidMode();

            ImGui.End();
        }

        private static void DrawOffRaidMode()
        {
            // Header
            ImGui.TextColored(TraderColor, "Quest Browser (Off-Raid)");
            ImGui.Separator();

            // Search and filter bar
            ImGui.SetNextItemWidth(200);
            ImGui.InputTextWithHint("##search", "Search...", ref _searchFilter, 128);
            ImGui.SameLine();

            ImGui.SetNextItemWidth(120);
            if (ImGui.BeginCombo("Trader", _selectedTrader))
            {
                foreach (var t in TraderNames)
                    if (ImGui.Selectable(t, _selectedTrader == t))
                        _selectedTrader = t;
                ImGui.EndCombo();
            }

            ImGui.Separator();

            // Quest count
            var all = TarkovDataManager.TaskData.Values.ToList();
            var filtered = FilterOffRaidTasks(all);
            ImGui.Text("Showing " + filtered.Count + " of " + all.Count + " quests");

            ImGui.BeginChild("QuestBrowserList", new System.Numerics.Vector2(0, 0), ImGuiChildFlags.None);

            // Group by trader
            foreach (var g in filtered.GroupBy(GetTraderName).OrderBy(x => x.Key))
            {
                // Trader header
                ImGui.PushStyleColor(ImGuiCol.Text, TraderColor);
                bool open = ImGui.TreeNodeEx(g.Key + " (" + g.Count() + ")", ImGuiTreeNodeFlags.SpanAvailWidth);
                ImGui.PopStyleColor();

                if (open)
                {
                    foreach (var t in g.OrderBy(x => x.Name))
                        DrawOffRaidQuestEntry(t);
                    ImGui.TreePop();
                }
            }

            ImGui.EndChild();
        }

        private static List<TarkovDevTypes.TaskElement> FilterOffRaidTasks(List<TarkovDevTypes.TaskElement> tasks)
        {
            var r = tasks.AsEnumerable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                string s = _searchFilter.ToLowerInvariant();
                r = r.Where(t => (t.Name != null && t.Name.ToLowerInvariant().Contains(s)) ||
                                 (t.Id != null && t.Id.ToLowerInvariant().Contains(s)));
            }

            // Trader filter
            if (_selectedTrader != "All")
                r = r.Where(t => GetTraderName(t).Equals(_selectedTrader, StringComparison.OrdinalIgnoreCase));

            return r.ToList();
        }

        private static string GetTraderName(TarkovDevTypes.TaskElement t)
        {
            // Try to determine trader from task ID or name patterns
            // This is a heuristic since we don't have trader info in the current API response
            string id = t.Id?.ToLowerInvariant() ?? "";
            string n = t.Name?.ToLowerInvariant() ?? "";

            if (id.Contains("prapor") || n.Contains("prapor")) return "Prapor";
            if (id.Contains("therapist") || n.Contains("therapist")) return "Therapist";
            if (id.Contains("skier") || n.Contains("skier")) return "Skier";
            if (id.Contains("peacekeeper") || n.Contains("peacekeeper")) return "Peacekeeper";
            if (id.Contains("mechanic") || n.Contains("mechanic")) return "Mechanic";
            if (id.Contains("ragman") || n.Contains("ragman")) return "Ragman";
            if (id.Contains("jaeger") || n.Contains("jaeger")) return "Jaeger";
            if (id.Contains("fence") || n.Contains("fence")) return "Fence";
            if (id.Contains("lightkeeper") || n.Contains("lightkeeper")) return "Lightkeeper";

            return "Unknown";
        }

        private static void DrawOffRaidQuestEntry(TarkovDevTypes.TaskElement task)
        {
            ImGui.PushID(task.Id);

            bool tracked = Config.TrackedQuests.Contains(task.Id);
            var col = tracked ? TraderColor : QuestTitleColor;

            // Quest header with tracking indicator
            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.SpanAvailWidth;

            string prefix = tracked ? "[T] " : "";
            var headerColor = tracked ? TraderColor : QuestTitleColor;

            ImGui.PushStyleColor(ImGuiCol.Text, col);
            bool exp = ImGui.TreeNodeEx(prefix + (task.Name ?? task.Id) + "##q", flags);
            ImGui.PopStyleColor();

            // Context menu
            if (ImGui.BeginPopupContextItem())
            {
                if (tracked)
                {
                    if (ImGui.MenuItem("Untrack"))
                        Config.TrackedQuests.Remove(task.Id);
                }
                else
                {
                    if (ImGui.MenuItem("Track"))
                        Config.TrackedQuests.Add(task.Id);
                }
                ImGui.EndPopup();
            }

            if (exp)
            {
                ImGui.Indent();

                // Show required keys/items at top
                DrawQuestRequirements(task);

                // Show objectives
                ImGui.TextColored(ProgressColor, "Objectives:");
                if (task.Objectives != null && task.Objectives.Count > 0)
                {
                    foreach (var o in task.Objectives)
                    {
                        DrawOffRaidObjective(o);
                    }
                }
                else
                {
                    ImGui.TextColored(ObjectiveLockedColor, "  No objective data");
                }

                ImGui.Unindent();
                ImGui.TreePop();
            }

            ImGui.PopID();
        }

        private static void DrawQuestRequirements(TarkovDevTypes.TaskElement task)
        {
            if (task.Objectives == null) return;

            var keys = new List<string>();
            var items = new List<string>();
            var questItems = new List<string>();

            foreach (var obj in task.Objectives)
            {
                // Required keys
                if (obj.RequiredKeys != null)
                {
                    foreach (var keyList in obj.RequiredKeys)
                    {
                        foreach (var key in keyList)
                        {
                            if (!string.IsNullOrEmpty(key.Name))
                                keys.Add(key.Name);
                            else if (!string.IsNullOrEmpty(key.ShortName))
                                keys.Add(key.ShortName);
                        }
                    }
                }

                // Items to find/give
                if (obj.Item != null)
                {
                    string itemName = obj.Item.Name ?? obj.Item.ShortName ?? "Item";
                    if (obj.Count > 1)
                        itemName += $" x{obj.Count}";
                    if (obj.FoundInRaid)
                        itemName += " (FIR)";
                    items.Add(itemName);
                }

                // Quest items
                if (obj.QuestItem != null)
                {
                    questItems.Add(obj.QuestItem.Name ?? obj.QuestItem.ShortName ?? "Quest Item");
                }

                // Marker items
                if (obj.MarkerItem != null)
                {
                    items.Add($"Marker: {obj.MarkerItem.Name ?? obj.MarkerItem.ShortName}");
                }
            }

            // Display requirements
            if (keys.Count > 0)
            {
                ImGui.TextColored(KeyItemColor, $"?? Keys: {string.Join(", ", keys.Distinct())}");
            }
            if (items.Count > 0)
            {
                ImGui.TextColored(TypeIconColor, $"?? Items: {string.Join(", ", items.Distinct().Take(5))}");
                if (items.Distinct().Count() > 5)
                    ImGui.TextColored(ObjectiveLockedColor, $"    ... and {items.Distinct().Count() - 5} more");
            }
            if (questItems.Count > 0)
            {
                ImGui.TextColored(LocationColor, $"? Quest Items: {string.Join(", ", questItems.Distinct())}");
            }

            if (keys.Count > 0 || items.Count > 0 || questItems.Count > 0)
                ImGui.Spacing();
        }

        private static void DrawOffRaidObjective(TarkovDevTypes.TaskElement.ObjectiveElement obj)
        {
            string typeIcon = GetObjectiveTypeIcon(obj.Type);
            string description = obj.Description ?? obj.Type.ToString();

            // Type badge
            ImGui.Text("  ");
            ImGui.SameLine();
            ImGui.TextColored(TypeIconColor, $"[{typeIcon}]");
            ImGui.SameLine();

            // Description (wrapped)
            ImGui.PushTextWrapPos(ImGui.GetWindowWidth() - 20);
            ImGui.TextColored(ObjectiveActiveColor, description);
            ImGui.PopTextWrapPos();

            // Show count if applicable
            if (obj.Count > 1)
            {
                ImGui.SameLine();
                ImGui.TextColored(ProgressColor, $"(x{obj.Count})");
            }

            // Show maps if specified
            if (obj.Maps != null && obj.Maps.Count > 0)
            {
                var mapNames = obj.Maps.Where(m => m?.Name != null).Select(m => m.Name);
                if (mapNames.Any())
                {
                    ImGui.TextColored(ObjectiveLockedColor, $"      ?? Maps: {string.Join(", ", mapNames)}");
                }
            }

            // Show zones
            if (obj.Zones != null && obj.Zones.Count > 0)
            {
                var zoneInfo = obj.Zones.Where(z => z?.Map?.Name != null).Select(z => z.Map.Name).Distinct();
                if (zoneInfo.Any())
                {
                    ImGui.TextColored(ObjectiveLockedColor, $"      ?? Zones on: {string.Join(", ", zoneInfo)}");
                }
            }
        }

        private static void DrawInRaidMode()
        {
            // Header
            ImGui.TextColored(TraderColor, "Active Quests (In-Raid)");

            // Show current map
            if (!string.IsNullOrEmpty(CurrentMapId))
            {
                ImGui.SameLine();
                ImGui.TextColored(LocationColor, "[" + CurrentMapId + "]");
            }

            ImGui.Separator();

            if (QuestManager?.Quests is not IReadOnlyDictionary<string, QuestEntry> quests || quests.Count == 0)
            {
                ImGui.TextColored(ObjectiveLockedColor, "No active quests found");
                return;
            }

            // Filter quests based on config
            var filtered = GetFilteredQuests(quests).ToList();

            // Separate tracked vs untracked
            var tracked = filtered.Where(q => Config.TrackedQuests.Contains(q.Id)).OrderBy(q => q.Name).ToList();
            var untracked = filtered.Where(q => !Config.TrackedQuests.Contains(q.Id)).OrderBy(q => q.Name).ToList();

            ImGui.Text("Active: " + filtered.Count);

            ImGui.BeginChild("QuestList", new System.Numerics.Vector2(0, 0), ImGuiChildFlags.None);

            // Draw tracked quests first (always expanded)
            if (tracked.Count > 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, TraderColor);
                ImGui.Text("TRACKED");
                ImGui.PopStyleColor();
                ImGui.Separator();

                foreach (var q in tracked)
                    DrawInRaidQuestEntry(q, true);

                ImGui.Spacing();
                ImGui.Spacing();
            }

            // Draw other quests
            if (untracked.Count > 0)
            {
                ImGui.TextColored(ObjectiveLockedColor, "Other Active Quests");
                ImGui.Separator();

                foreach (var q in untracked)
                    DrawInRaidQuestEntry(q, false);
            }

            ImGui.EndChild();
        }

        private static IEnumerable<QuestEntry> GetFilteredQuests(IReadOnlyDictionary<string, QuestEntry> quests)
        {
            var r = quests.Values.Where(q => !Config.BlacklistedQuests.ContainsKey(q.Id));

            // If ActiveOnly is enabled and we have tracked quests, only show tracked
            if (Config.ActiveOnly && Config.TrackedQuests.Count > 0)
                r = r.Where(q => Config.TrackedQuests.Contains(q.Id));

            return r;
        }

        private static void DrawInRaidQuestEntry(QuestEntry q, bool tracked)
        {
            if (q == null) return;

            ImGui.PushID(q.Id);

            TarkovDataManager.TaskData.TryGetValue(q.Id, out var td);
            ImGuiTreeNodeFlags f = ImGuiTreeNodeFlags.SpanAvailWidth;
            if (tracked)
                f |= ImGuiTreeNodeFlags.DefaultOpen;

            bool exp = ImGui.TreeNodeEx(q.Name ?? q.Id, f);

            if (ImGui.BeginPopupContextItem())
            {
                if (tracked)
                {
                    if (ImGui.MenuItem("Untrack"))
                        Config.TrackedQuests.Remove(q.Id);
                }
                else
                {
                    if (ImGui.MenuItem("Track"))
                        Config.TrackedQuests.Add(q.Id);
                }
                ImGui.EndPopup();
            }

            if (exp)
            {
                ImGui.Indent();

                if (td?.Objectives != null)
                {
                    foreach (var o in td.Objectives)
                    {
                        bool done = q.IsObjectiveCompleted(o.Id);
                        int cur = q.GetObjectiveProgress(o.Id);
                        int tgt = q.GetObjectiveTargetCount(o.Id);
                        if (tgt <= 0) tgt = o.Count;
                        if (!done && tgt > 0 && cur >= tgt) done = true;

                        string icon = GetObjectiveTypeIcon(o.Type);
                        var col = done ? ObjectiveCompletedColor : ObjectiveActiveColor;
                        string pre = done ? "[OK]" : "[ ]";

                        ImGui.TextColored(col, pre);
                        ImGui.SameLine();
                        ImGui.TextColored(TypeIconColor, "[" + icon + "]");
                        ImGui.SameLine();

                        string d = o.Description ?? o.Type.ToString();
                        if (d.Length > 50)
                            d = d.Substring(0, 47) + "...";

                        ImGui.TextColored(col, d);

                        if (tgt > 0)
                        {
                            ImGui.SameLine();
                            ImGui.TextColored(done ? ObjectiveCompletedColor : ProgressColor, cur + "/" + tgt);
                        }
                    }
                }

                ImGui.Unindent();
                ImGui.TreePop();
            }

            ImGui.PopID();
        }

        private static string GetObjectiveTypeIcon(QuestObjectiveType t)
        {
            return t switch
            {
                QuestObjectiveType.FindItem => "FIND",
                QuestObjectiveType.FindQuestItem => "FIR",
                QuestObjectiveType.GiveItem => "GIVE",
                QuestObjectiveType.GiveQuestItem => "GIVE",
                QuestObjectiveType.Visit => "VISIT",
                QuestObjectiveType.Mark => "MARK",
                QuestObjectiveType.PlantItem => "PLANT",
                QuestObjectiveType.PlantQuestItem => "PLANT",
                QuestObjectiveType.Extract => "EXFIL",
                QuestObjectiveType.Shoot => "KILL",
                QuestObjectiveType.Skill => "SKILL",
                QuestObjectiveType.TraderLevel => "LVL",
                QuestObjectiveType.TraderStanding => "REP",
                QuestObjectiveType.BuildWeapon => "BUILD",
                QuestObjectiveType.Experience => "XP",
                QuestObjectiveType.UseItem => "USE",
                QuestObjectiveType.SellItem => "SELL",
                QuestObjectiveType.TaskStatus => "TASK",
                _ => "?"
            };
        }
    }
}
