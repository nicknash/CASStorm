using System.Collections.Generic;
using System.Diagnostics;

namespace CASStorm.Workloads
{
    class IdleTimeWorkload : IWorkload
    {
        public List<long> IdleTimesInTicks { get; private set; } 
        public string Name { get; } = "IdleTime";
        public IReadOnlyList<WorkloadEntry> Entries { get; }

        private long _lastReleaseTicks;

        public IdleTimeWorkload()
        {
            var entry = new WorkloadEntry(0, AddIdleTime, 0, _ => _lastReleaseTicks = Stopwatch.GetTimestamp());
            Entries = new[]{entry};            
        }

        public void Reset()
        {
            _lastReleaseTicks = 0;
            IdleTimesInTicks.Clear();
        }

        private void AddIdleTime()
        {
            if(_lastReleaseTicks > 0)
            {
                IdleTimesInTicks.Add(Stopwatch.GetTimestamp() - _lastReleaseTicks);
            }
        }

    }
}
