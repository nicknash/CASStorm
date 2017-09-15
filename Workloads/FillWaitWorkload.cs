using System;
using System.Linq;
using System.Collections.Generic;

namespace CASStorm.Workloads
{
    class FillWaitWorkload : IWorkload
    {
        public string Name { get; } = "FillWait";
        public IReadOnlyList<WorkloadEntry> Entries { get; }
        public FillWaitWorkload(int minSizePower, int maxSizePower, int minWaitPower, int maxWaitPower)
          {
            var shared = new int[1 << maxSizePower];
            var entries = new List<WorkloadEntry>();
            for (int sizePower = minSizePower; sizePower <= maxSizePower; ++sizePower)
            {
                int acquireSize = 1 << sizePower;
                Action acquireAction = () => Utils.Fill(shared, 0, acquireSize);
                for (int waitPower = minWaitPower; waitPower <= maxWaitPower; waitPower += 2)
                {
                    int numNanos = 1 << waitPower;
                    Action<int> releaseAction = _ => Utils.WaitNanos(numNanos);
                    var entry = new WorkloadEntry(acquireSize, acquireAction, numNanos, releaseAction);
                    entries.Add(entry);
                }
            }
            Entries = entries;
        }
    }
}
