using System.ComponentModel;
using System.Collections.Concurrent;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Quests
{
    /// <summary>
    /// Represents a quest entry with enable/disable functionality and progress tracking.
    /// </summary>
    public sealed class QuestEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string Id { get; }
        public string Name { get; }

        /// <summary>
        /// Completed objective condition IDs for this quest.
        /// </summary>
        public HashSet<string> CompletedConditions { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Objective progress counters (ObjectiveId -> (CurrentCount, TargetCount)).
        /// </summary>
        public ConcurrentDictionary<string, (int CurrentCount, int TargetCount)> ConditionCounters { get; } = new(StringComparer.OrdinalIgnoreCase);

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                if (value)
                    Program.Config.QuestHelper.BlacklistedQuests.TryRemove(Id, out _);
                else
                    Program.Config.QuestHelper.BlacklistedQuests.TryAdd(Id, 0);
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        public QuestEntry(string id)
        {
            Id = id;
            Name = TarkovDataManager.TaskData.TryGetValue(id, out var task)
                ? task.Name ?? id
                : id;
            _isEnabled = !Program.Config.QuestHelper.BlacklistedQuests.ContainsKey(id);
        }

        /// <summary>
        /// Check if a specific objective is completed.
        /// </summary>
        public bool IsObjectiveCompleted(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId))
                return false;
            return CompletedConditions.Contains(objectiveId);
        }

        /// <summary>
        /// Get the current progress count for an objective.
        /// </summary>
        public int GetObjectiveProgress(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId))
                return 0;
            return ConditionCounters.TryGetValue(objectiveId, out var count) ? count.CurrentCount : 0;
        }

        /// <summary>
        /// Get the target count for an objective from game memory.
        /// Returns 0 if not found (fallback to API value should be used).
        /// </summary>
        public int GetObjectiveTargetCount(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId))
                return 0;
            return ConditionCounters.TryGetValue(objectiveId, out var count) ? count.TargetCount : 0;
        }

        /// <summary>
        /// Update completed conditions from memory.
        /// </summary>
        internal void UpdateCompletedConditions(IEnumerable<string> completedIds)
        {
            CompletedConditions.Clear();
            foreach (var id in completedIds)
            {
                if (!string.IsNullOrEmpty(id))
                    CompletedConditions.Add(id);
            }
        }

        /// <summary>
        /// Update condition counters from memory.
        /// </summary>
        internal void UpdateConditionCounters(IEnumerable<KeyValuePair<string, (int CurrentCount, int TargetCount)>> counters)
        {
            ConditionCounters.Clear();
            foreach (var kvp in counters)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                    ConditionCounters[kvp.Key] = kvp.Value;
            }
        }

        public override string ToString() => Name;
    }
}