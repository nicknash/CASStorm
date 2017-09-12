using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace CASStorm.Workloads
{    
    class PureWaitWorkload : IWorkload 
    {
        public string Name => "PureWait";

        public IReadOnlyList<WorkloadEntry> Entries { get; }

        public PureWaitWorkload(int minWaitPower, int maxWaitPower, int minReleaseMultiplier, int maxReleaseMultiplier)
        {
            var entries = new List<WorkloadEntry>();
            for (int waitPower = minWaitPower; waitPower <= maxWaitPower; ++waitPower)
            {
                int numNanos = 1 << waitPower;
                Action acquireAction = () => Utils.WaitNanos(numNanos);
                for (int releaseMultiplier = minReleaseMultiplier; releaseMultiplier <= maxReleaseMultiplier; ++releaseMultiplier)
                {
                    int numReleaseNanos = releaseMultiplier * numNanos;
                    Action<int> releaseAction = _ => Utils.WaitNanos(numReleaseNanos); 
                    var entry = new WorkloadEntry(numNanos, acquireAction, numReleaseNanos, releaseAction);
                    entries.Add(entry);
                }
            }
            Entries = entries;           
        }
    }
}