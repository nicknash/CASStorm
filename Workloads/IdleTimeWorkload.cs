using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System;

namespace CASStorm.Workloads
{
    class IdleTimeWorkload : IWorkload
    {
        private List<long> _idleTimesInTicks;
        public string Name { get; } = "IdleTime";
        public IReadOnlyList<WorkloadEntry> Entries { get; }

        private long _lastReleaseTicks;

        public IdleTimeWorkload(int minWaitPower, int maxWaitPower, List<long> idleTimesInTicks)
        {
            _idleTimesInTicks = idleTimesInTicks;
            var entries = new List<WorkloadEntry>(maxWaitPower - minWaitPower + 1);
            for (int waitPower = minWaitPower; waitPower <= maxWaitPower; waitPower += 2)
            {
                int numNanos = 1 << waitPower;
                Action acquireAction = () => AcquireAction(numNanos); 
                var entry = new WorkloadEntry(numNanos, acquireAction, 0, ReleaseAction);            
                entries.Add(entry);
            }
            Entries = entries;
        }

        public void Reset()
        {
            Volatile.Write(ref _lastReleaseTicks, 0);
            _idleTimesInTicks.Clear();
        }

        private void ReleaseAction(int unused)
        {
            long now = Stopwatch.GetTimestamp();
            Volatile.Write(ref _lastReleaseTicks, now);
        }

        private void AcquireAction(double waitNanos)
        {
            long ticks = Volatile.Read(ref _lastReleaseTicks);
            if(ticks > 0)
            {
                _idleTimesInTicks.Add(Stopwatch.GetTimestamp() - ticks);
            }
            Utils.WaitNanos(waitNanos);
        }

    }
}
