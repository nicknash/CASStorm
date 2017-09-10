using System.Collections.Generic;
using System.Diagnostics;

namespace CASStorm.Workloads
{    
    class PureWaitWorkload : IWorkload 
    {
        public string Name => "PureWait";

        public IReadOnlyList<WorkloadEntry> Entries => throw new System.NotImplementedException();

        public PureWaitWorkload(int minThreads, int maxThreads, int minWaitNanos, int maxWaitNanos)
        {
            
        }

        // TODO: Pure waits 
        private static void WaitNanos(double numNanos)
        {
            double numTicks = 1/Stopwatch.Frequency*numNanos*1e-9;
            var t = Stopwatch.GetTimestamp();
            while(Stopwatch.GetTimestamp() - t < numTicks) ;
        }

        public static void Test()
        {
            Console.WriteLine($"1 tick = {1e6/Stopwatch.Frequency} mics");
            for(int i = 0; i < 1000000; ++i)
            {
                WaitNanos(1000);
            }
            Console.WriteLine("wait over..");
            for (int size = 8; size <= 1024; size <<= 1)
            {
                var a = new int[size];
                var sw = new Stopwatch();
                sw.Start();
                int reps = 100000;
                for (int i = 0; i < reps; ++i)
                {
                    Fill(a, a.Length);
                }
                Console.WriteLine($"Filling size {size} took: {sw.Elapsed.TotalMilliseconds/reps*1e6} nanos.");
            }
            return;
        }
    }
}