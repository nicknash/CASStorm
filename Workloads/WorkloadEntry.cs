using System;

namespace CASStorm.Workloads
{
    class WorkloadEntry
    {
        public readonly Action AcquireAction;
        public readonly Action<int> ReleaseAction;
        public readonly int AcquireSize;
        public readonly int ReleaseSize;

        public WorkloadEntry(int acquireSize, Action acquireAction, int releaseSize, Action<int> releaseAction)
        {
            AcquireSize = acquireSize;
            AcquireAction = acquireAction;
            ReleaseSize = releaseSize;
            ReleaseAction = releaseAction;
        }
    }
}
