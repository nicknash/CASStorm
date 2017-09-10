using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace CASStorm.Workloads
{    
    class PureWaitWorkload : IWorkload 
    {
        public string Name => "PureWait";

        public IReadOnlyList<WorkloadEntry> Entries { get; }

        public PureWaitWorkload(int minThreads, int maxThreads, int minWaitPower, int maxWaitPower, int minReleaseMultiplier, int maxReleaseMultiplier)
        {
            var entries = new List<WorkloadEntry>();
            for (int waitPower = minWaitPower; waitPower <= maxWaitPower; ++waitPower)
            {
                int numNanos = 1 << waitPower;
                Action acquireAction = () => WaitNanos(numNanos);
                for (int releaseMultiplier = 0; releaseMultiplier <= maxReleaseMultiplier; ++releaseMultiplier)
                {
                    int numReleaseNanos = releaseMultiplier * numNanos;
                    Action<int> releaseAction = _ => WaitNanos(numReleaseNanos); 
                    var entry = new WorkloadEntry(numNanos, acquireAction, releaseMultiplier, releaseAction);
                    entries.Add(entry);
                }
            }
            Entries = entries;           
        }

        private static void WaitNanos(double numNanos)
        {
            double numTicks = numNanos/Stopwatch.Frequency*1e-9;
            var t = Stopwatch.GetTimestamp();
            while(Stopwatch.GetTimestamp() - t < numTicks) ;
        }
    }
}