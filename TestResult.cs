namespace CASStorm
{
    class TestResult
    {
        public string WorkloadName { get; }
        public int NumThreads { get; }
        public string LockType { get; }
        public int NumLockAcquires { get; }
        public int NumReleaseIterations { get; }
        public int CriticalSectionSize { get; }
        public double TotalMilliseconds { get; }

        public TestResult(string workloadName, int numThreads, string lockType, int numLockAcquires, int numReleaseIterations, int criticalSectionSize, double totalMilliseconds)
        {
            WorkloadName = workloadName;
            NumThreads = numThreads;
            LockType = lockType;
            NumLockAcquires = numLockAcquires;
            NumReleaseIterations = numReleaseIterations;
            CriticalSectionSize = criticalSectionSize;
            TotalMilliseconds = totalMilliseconds;
        }
    }
}
