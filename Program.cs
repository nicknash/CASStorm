using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace ConsoleApplication
{
/* 
    class IncrementWorkload : IWorkload 
    {
        // TODO: single counter only (shouldn't be very interesting, as should be limited case / sanity check of ArrayFill)
    }

    class PureWaitWorkload : IWorkload 
    {
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
*/
    public class Program
    {

        public static void Main(string[] args)
        {  
            int minThreads = Int32.Parse(args[0]);
            int maxThreads = Int32.Parse(args[1]);
            int numLockAcquires = Int32.Parse(args[2]);
            int maxReleaseIterationsBound = Int32.Parse(args[3]);
            int minSizePower = Int32.Parse(args[4]);
            int maxSizePower = Int32.Parse(args[5]);
            int maxQueisceDelayPower = Int32.Parse(args[6]);
            bool runQueisceTest = args[7] == "q";

            Console.WriteLine("Running scalability test: ");
            var locks = new ILock[] { new NaiveAggressiveSpinLock(), new NaiveTestAndTestSpinLock(), new UnscalableTicketLock(), new KernelLock()};
            var workloads = new IWorkload[] { new ArrayFillWorkload(minSizePower, maxSizePower, maxReleaseIterationsBound, maxThreads)};
            var totalWorkloadSize = workloads.Select(i => i.Entries.Count).Sum();
            int numScalingTestResults = locks.Length *  (1 + maxThreads - minThreads) * totalWorkloadSize;  
            int maxSize = 1 << maxSizePower;
            var contendedData = new int[maxSize];
            int[][] releaseData = new int[maxThreads * 2][];
            for(int i = 0; i < maxThreads * 2; ++i)
            {
                releaseData[i] = new int[maxSize];
            }
            var scalingTestResults = new TestResult[numScalingTestResults];
            int scalingResultIdx = 0;
            foreach (ILock theLock in locks)
            {
                foreach (var workload in workloads)
                {
                    for (int numThreads = minThreads; numThreads <= maxThreads; ++numThreads)
                    {
                        foreach (var workLoadEntry in workload.Entries)
                        {
                            Console.Write($"                             \r{1 + scalingResultIdx}/{numScalingTestResults}");
                            scalingTestResults[scalingResultIdx] = RunContendingTest(numLockAcquires, theLock, numThreads, workLoadEntry, workload.Name);
                            ++scalingResultIdx;
                        }
                    }
                }
            }

            Console.WriteLine();
            var scalingResultsFileName = $"ScalingResults-{Process.GetCurrentProcess().Id}.csv";
            Console.WriteLine($"Writing test results to {scalingResultsFileName}");
            WriteTestResults(scalingResultsFileName, scalingTestResults);

            if (runQueisceTest)
            {
                Console.WriteLine("------------------------------------------------");
                Console.WriteLine("Running bus quiescensce test: ");
                int maxQueisceDelay = 1 << maxQueisceDelayPower;
                int numQuiesceResults = (1 + maxQueisceDelayPower) * (1 + maxThreads - minThreads);
                var quiesceResults = new TestResult[numQuiesceResults];
                int quiesceResultIdx = 0;
                int criticalSectonSize = 256;
                for (int quiesceDelay = 1; quiesceDelay <= maxQueisceDelay; quiesceDelay <<= 1)
                {
                    for (int numThreads = minThreads; numThreads <= maxThreads; ++numThreads)
                    {
                        var quiesceLock = new QuiesceLock(quiesceDelay, numThreads);
                        Console.Write($"                                    \r{1 + quiesceResultIdx}/{numQuiesceResults}");
                        quiesceResults[quiesceResultIdx] = RunContendingTest(numLockAcquires, maxReleaseIterationsBound, quiesceLock, numThreads, releaseData, contendedData.Length, () => Fill(contendedData, criticalSectonSize));
                        ++quiesceResultIdx;
                    }
                }
                Console.WriteLine();
                var quiesceResultsFileName = $"QuiesceResults-{Process.GetCurrentProcess().Id}.csv";
                Console.WriteLine($"Writing test results to {quiesceResultsFileName}");
                WriteTestResults(quiesceResultsFileName, quiesceResults);
            }
        }

        private static void WriteTestResults(string fileName, TestResult[] results)
        {
            using(var writer = File.CreateText(fileName))
            {
                writer.WriteLine("LockType,NumThreads,NumLockAcquires,NumReleaseIterations,CriticalSectionSize,TotalMilliseconds");
                for(int i = 0; i < results.Length; ++i)
                {
                    var r = results[i];
                    writer.WriteLine($"{r.LockType},{r.NumThreads},{r.NumLockAcquires},{r.NumReleaseIterations},{r.CriticalSectionSize},{r.TotalMilliseconds}");
                }
            }
        }

        private static TestResult RunContendingTest(int numLockAcquires, ILock naiveLock, int numThreads, WorkloadEntry workloadEntry, string workloadName)
        {
            var sw = new Stopwatch();
            var barrier = new Barrier(numThreads + 1);
            var threads = new Thread[numThreads];
            for (int i = 0; i < numThreads; ++i)
            {
                int threadIdx = i;
                Action releaseAction = () => workloadEntry.ReleaseAction(threadIdx);
                threads[i] = new Thread(new ThreadStart(GetThreadFunction(naiveLock, barrier, threadIdx, numLockAcquires / numThreads, workloadEntry.AcquireAction, releaseAction)));
                threads[i].Start();
            }
            barrier.SignalAndWait();
            sw.Start();
            barrier.SignalAndWait();
            sw.Stop();
            return new TestResult(workloadName, numThreads, naiveLock.GetType().ToString(), numLockAcquires, workloadEntry.ReleaseSize, workloadEntry.AcquireSize, sw.Elapsed.TotalMilliseconds);
        }

        private static Action GetThreadFunction(ILock naiveLock, Barrier barrier, int threadIdx, int numAcquires, Action acquireAction, Action releaseAction)
        {
            return () => 
            {
                barrier.SignalAndWait();  
                for(int i = 0; i < numAcquires; ++i)
                {
                    naiveLock.Enter(threadIdx);
                    acquireAction();
                    naiveLock.Exit();
                    releaseAction();
                }
                barrier.SignalAndWait();
            };
        }
    }
}
