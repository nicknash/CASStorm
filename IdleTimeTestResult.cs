namespace CASStorm
{
    class IdleTimeTestResult
    {
        public string LockName { get; }
        public int NumThreads { get; }
        public int HoldTimeNanos { get;}
        public double IdleTimeMinMics { get; }
        public double IdleTime25Mics { get; }
        public double IdleTime50Mics { get; }
        public double IdleTime75Mics { get; }
        public double IdleTime99Mics { get; }
        public double IdleTimeMaxMics { get; }

        public IdleTimeTestResult(string lockName, int numThreads, int holdTimeNanos, double idleTimeMinMics, double idleTime25Mics, double idleTime50Mics, double idleTime75Mics, double idleTime99Mics, double idleTimeMaxMics)
        {
            LockName = lockName;
            NumThreads = numThreads;
            HoldTimeNanos = holdTimeNanos;
            IdleTimeMinMics = idleTimeMinMics;
            IdleTime25Mics = idleTime25Mics;
            IdleTime50Mics = idleTime50Mics;
            IdleTime75Mics = idleTime75Mics;
            IdleTime99Mics = idleTime99Mics;
            IdleTimeMaxMics = idleTimeMaxMics;
        }

    }
}
