using System.Collections.Generic;

namespace CASStorm.Workloads
{
    interface IWorkload 
    {
        string Name { get; }
        IReadOnlyList<WorkloadEntry> Entries { get; }
    }
}
