using System.Collections.Generic;

namespace ConsoleApplication
{
    interface IWorkload 
    {
        string Name { get; }
        IReadOnlyList<WorkloadEntry> Entries { get; }
    }
}
