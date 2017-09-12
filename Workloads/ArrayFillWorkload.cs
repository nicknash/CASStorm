using System;
using System.Linq;
using System.Collections.Generic;

namespace CASStorm.Workloads
{
    class ArrayFillWorkload : IWorkload
    {
        public string Name { get; } = "ArrayFill";
        public IReadOnlyList<WorkloadEntry> Entries { get; }
        public ArrayFillWorkload(int minSizePower, int maxSizePower, int maxReleaseIterationsBound, int maxThreads)
        {
            const int SeparationPower = 7; // To avoid false sharing.
            var shared = new int[1 << maxSizePower];
            var releaseData = new int[maxThreads << SeparationPower];
            var random = Enumerable.Range(0, maxThreads).Select(idx => new Random(idx)).ToArray();
            var entries = new List<WorkloadEntry>();
            for (int sizePower = minSizePower; sizePower <= maxSizePower; ++sizePower)
            {
                int acquireSize = 1 << sizePower;
                Action acquireAction = () => Utils.Fill(shared, 0, acquireSize);
                for (int releaseIterationsBound = 0; releaseIterationsBound <= maxReleaseIterationsBound; ++releaseIterationsBound)
                {
                    int size = acquireSize;
                    Action<int> releaseAction = idx => ReleaseAction(idx, releaseData, maxReleaseIterationsBound, SeparationPower, size, random);
                    var entry = new WorkloadEntry(size, acquireAction, releaseIterationsBound, releaseAction);
                    entries.Add(entry);
                }
            }
            Entries = entries;
        }

        private void ReleaseAction(int threadIdx, int[] releaseData, int maxReleaseIterations, int separationPower, int size, Random[] random)
        {
            var r = random[threadIdx];
            int numReleaseIterations = r.Next(1 + maxReleaseIterations);
            int startIdx = threadIdx << separationPower;
            for (int i = 0; i < numReleaseIterations; ++i)
            {
                Utils.Fill(releaseData, startIdx, size);
            }
            return;
        }
    }
}
