using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using CASStorm.Workloads;
using CASStorm.Locks;

namespace CASStorm
{
    public partial class Program
    {
        public static void Main(string[] args)
        {  
            int minThreads = Int32.Parse(args[0]);
            int maxThreads = Int32.Parse(args[1]);
            int numLockAcquires = Int32.Parse(args[2]);
            int maxReleaseIterationsBound = Int32.Parse(args[3]); // e.g. 10
            int minSizePower = Int32.Parse(args[4]); // e.g. 4
            int maxSizePower = Int32.Parse(args[5]); // e.g. 12
            int minWaitPower = Int32.Parse(args[6]); // e.g. 5
            int maxWaitPower = Int32.Parse(args[7]); // e.g. 16
            int maxQuiesceDelayPower = Int32.Parse(args[8]); // e.g. 16

            Console.WriteLine("Running TTAS Wake-up count test: ");
            RunWakeUpTest(minThreads, maxThreads, numLockAcquires);
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("Running critical section idle time test: ");                        
            RunIdleTimeTest(minThreads, maxThreads, numLockAcquires, minWaitPower, maxWaitPower);
            Console.WriteLine("Running scaling test: ");
            RunScalingTest(numLockAcquires, minSizePower, maxSizePower, maxReleaseIterationsBound, minThreads, maxThreads, minWaitPower, maxWaitPower);
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("Running bus quiescensce test: ");
            RunQuiescensceTest(numLockAcquires, maxQuiesceDelayPower, minWaitPower, maxWaitPower, maxReleaseIterationsBound, minThreads, maxThreads);
            Console.WriteLine("------------------------------------------------");
        }

        private static void RunScalingTest(int numLockAcquires, int minSizePower, int maxSizePower, int maxReleaseIterationsBound, int minThreads, int maxThreads, int minWaitPower, int maxWaitPower)
        {
            var locks = new ILock[] { new NaiveAggressiveSpinLock(), new NaiveTestAndTestSpinLock(), new UnscalableTicketLock(), new KernelLock()};
            var workloads = new IWorkload[] { new ArrayFillWorkload(minSizePower, maxSizePower, maxReleaseIterationsBound, maxThreads)
                                            , new PureWaitWorkload(minWaitPower, maxWaitPower, 0, maxReleaseIterationsBound)
                                            , new FillWaitWorkload(minSizePower, maxSizePower, minWaitPower, maxWaitPower)
                                            };
            var totalWorkloadSize = TotalWorkloadSize(workloads);
            int numScalingTestResults = locks.Length *  (1 + maxThreads - minThreads) * totalWorkloadSize;  
            int maxSize = 1 << maxSizePower;
            
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
                            scalingTestResults[scalingResultIdx] = GetTestResult(numLockAcquires, theLock, numThreads, workLoadEntry, workload.Name, 0);
                            ++scalingResultIdx;
                        }
                    }
                }
            }
 
            Console.WriteLine();
            var scalingResultsFileName = $"ScalingResults-{Process.GetCurrentProcess().Id}.csv";
            Console.WriteLine($"Writing test results to {scalingResultsFileName}");
            WriteTestResults(scalingResultsFileName, scalingTestResults);
        }

        private static void RunQuiescensceTest(int numLockAcquires, int maxQuiesceDelayPower, int minWaitPower, int maxWaitPower, int maxReleaseIterationsBound, int minThreads, int maxThreads)
        {
            var workloads = new IWorkload[] {new ArrayFillWorkload(8, 8, maxReleaseIterationsBound, maxThreads), 
                                             new PureWaitWorkload(minWaitPower, maxWaitPower, 0, 0),
                                             new FillWaitWorkload(8, 8, minWaitPower, maxWaitPower)
                                            };
            var totalWorkloadSize = TotalWorkloadSize(workloads);
            var lockFactories = new Func<int, int, ILock>[]{(quiesceDelay, numThreads) => new FillQuiesceLock(quiesceDelay, numThreads)
                                                           ,(quiesceDelay, _) => new WaitQuiesceLock(quiesceDelay)};

            int maxQueisceDelay = 1 << maxQuiesceDelayPower;
            int numQuiesceResults = lockFactories.Length * (1 + maxQuiesceDelayPower) * (1 + maxThreads - minThreads) * totalWorkloadSize;
            var quiesceResults = new TestResult[numQuiesceResults];
            int quiesceResultIdx = 0;
            for (int quiesceDelay = 4; quiesceDelay <= maxQueisceDelay; quiesceDelay <<= 2)
            {
                foreach (var getLock in lockFactories)
                {
                    foreach (var workload in workloads)
                    {
                        for (int numThreads = minThreads; numThreads <= maxThreads; ++numThreads)
                        {
                            var quiesceLock = getLock(quiesceDelay, numThreads);
                            foreach (var workloadEntry in workload.Entries)
                            {
                                Console.Write($"                                    \r{1 + quiesceResultIdx}/{numQuiesceResults}");
                                quiesceResults[quiesceResultIdx] = GetTestResult(numLockAcquires, quiesceLock, numThreads, workloadEntry, workload.Name, quiesceDelay);
                                ++quiesceResultIdx;
                            }
                        }
                    }
                }
            }
            Console.WriteLine();
            var quiesceResultsFileName = $"QuiesceResults-{Process.GetCurrentProcess().Id}.csv";
            Console.WriteLine($"Writing test results to {quiesceResultsFileName}");
            WriteTestResults(quiesceResultsFileName, quiesceResults);
        }

        private static void RunWakeUpTest(int minThreads, int maxThreads, int numLockAcquires)
        {
            var workload = new ArrayFillWorkload(0, 0, 0, maxThreads);
            var entry = workload.Entries[0];
            int numTestResults = maxThreads - minThreads + 1;
            var results = new int[maxThreads + 1];
            for(int numThreads = minThreads; numThreads <= maxThreads; ++numThreads)
            {
                var wakeupCountLock = new WakeupCountTestAndTestAndSetLock();
                Console.Write($"                                    \r{1 + numThreads - minThreads}/{numTestResults}");
                GetTestResult(numLockAcquires, wakeupCountLock, numThreads, entry, "WakeUp", 0);
                results[numThreads] = wakeupCountLock.NumWakeUps;
            }
            var resultsFileName = $"WakeupResults-{Process.GetCurrentProcess().Id}.csv";
            Console.WriteLine();
            Console.WriteLine($"Writing test results to {resultsFileName}");
            using(var writer = File.CreateText(resultsFileName))
            {
                writer.WriteLine("NumThreads,NumLockAcquires,NumWakeUps");
                for(int i = minThreads; i <= maxThreads; ++i)
                {
                    writer.WriteLine($"{i},{numLockAcquires},{results[i]}");
                }
            }
        }

        private static void RunIdleTimeTest(int minThreads, int maxThreads, int numLockAcquires, int minWaitPower, int maxWaitPower)
        {
            var locks = new ILock[] { new NaiveAggressiveSpinLock(), new NaiveTestAndTestSpinLock(), new UnscalableTicketLock(), new KernelLock()};
            Func<long, double> ticksToMics = ticks => 1e6*ticks/Stopwatch.Frequency;
            var idleTimes = new List<long>();
            var workload = new IdleTimeWorkload(minWaitPower, maxWaitPower, idleTimes);
            var results = new IdleTimeTestResult[locks.Length * (1 + maxThreads - minThreads) * workload.Entries.Count];
            int resultIdx = 0;
            foreach (var theLock in locks)
            {
                for (int numThreads = minThreads; numThreads <= maxThreads; ++numThreads)
                {
                    foreach(var entry in workload.Entries)
                    {
                        Console.Write($"                                    \r{1 + resultIdx}/{results.Length}");
                        GetTestResult(numLockAcquires, theLock, numThreads, entry, "IdleTime", 0);
                        int n = idleTimes.Count;
                        idleTimes.Sort();
                        results[resultIdx] = new IdleTimeTestResult(theLock.GetType().Name, numThreads, entry.AcquireSize, 
                                                                                                        ticksToMics(idleTimes[0]),
                                                                                                        ticksToMics(idleTimes[(int)(n * 0.25)]),
                                                                                                        ticksToMics(idleTimes[(int)(n * 0.5)]),
                                                                                                        ticksToMics(idleTimes[(int)(n * 0.75)]),
                                                                                                        ticksToMics(idleTimes[(int)(n * 0.99)]),
                                                                                                        ticksToMics(idleTimes[n - 1]));
                        ++resultIdx;
                        workload.Reset();
                    }
                }
            }

            var resultsFileName = $"IdleTimeResults-{Process.GetCurrentProcess().Id}.csv";
            Console.WriteLine();
            Console.WriteLine($"Writing test results to {resultsFileName}");
            using(var writer = File.CreateText(resultsFileName))
            {
                writer.WriteLine("NumThreads,LockType,HoldTimeNanos,MinIdleTimeMics,25PercIdleTimeMics,50PercIdleTimeMics,75PercIdleTimeMics,99PercIdleTimeMics,MaxIdleTimeMics");
                foreach(var r in results)
                {
                    writer.WriteLine($"{r.NumThreads},{r.LockName},{r.HoldTimeNanos},{r.IdleTimeMinMics},{r.IdleTime25Mics},{r.IdleTime50Mics},{r.IdleTime75Mics},{r.IdleTime99Mics},{r.IdleTimeMaxMics}");
                }
            }
        }

        private static int TotalWorkloadSize(IReadOnlyList<IWorkload> workloads) => workloads.Select(i => i.Entries.Count).Sum();
      
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

        private static TestResult GetTestResult(int numLockAcquires, ILock naiveLock, int numThreads, WorkloadEntry workloadEntry, string workloadName, int quiesceDelay)
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
            return new TestResult(workloadName, numThreads, naiveLock.GetType().Name, numLockAcquires, workloadEntry.ReleaseSize, workloadEntry.AcquireSize, sw.Elapsed.TotalMilliseconds, quiesceDelay);
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
