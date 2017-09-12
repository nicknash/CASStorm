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
            int minWaitPower = Int32.Parse(args[6]);
            int maxWaitPower = Int32.Parse(args[7]);
            int maxQuiesceDelayPower = Int32.Parse(args[8]);

            Console.WriteLine("Running scaling test: ");
            RunScalingTest(numLockAcquires, minSizePower, maxSizePower, maxReleaseIterationsBound, minThreads, maxThreads, minWaitPower, maxWaitPower);
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("Running bus quiescensce test: ");
            RunQuiescensceTest(numLockAcquires, maxQuiesceDelayPower, minWaitPower, maxWaitPower, maxReleaseIterationsBound, minThreads, maxThreads);


        }

        private static void RunScalingTest(int numLockAcquires, int minSizePower, int maxSizePower, int maxReleaseIterationsBound, int minThreads, int maxThreads, int minWaitPower, int maxWaitPower)
        {
            var locks = new ILock[] { new NaiveAggressiveSpinLock(), new NaiveTestAndTestSpinLock(), new UnscalableTicketLock(), new KernelLock()};
            var workloads = new IWorkload[] { new ArrayFillWorkload(minSizePower, maxSizePower, maxReleaseIterationsBound, maxThreads)
                                            , new PureWaitWorkload(minWaitPower, maxWaitPower, 1, maxReleaseIterationsBound)
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
                                         new PureWaitWorkload(minWaitPower, maxWaitPower, 0, 0) 
                                        };
            var totalWorkloadSize = TotalWorkloadSize(workloads);
            var lockFactories = new Func<int, int, ILock>[]{(quiesceDelay, numThreads) => new FillQuiesceLock(quiesceDelay, numThreads)
                                                           ,(quiesceDelay, _) => new WaitQuiesceLock(quiesceDelay)};

            int maxQueisceDelay = 1 << maxQuiesceDelayPower;
            int numQuiesceResults = (1 + maxQuiesceDelayPower) * (1 + maxThreads - minThreads) * totalWorkloadSize;
            var quiesceResults = new TestResult[numQuiesceResults];
            int quiesceResultIdx = 0;
            for (int quiesceDelay = 1; quiesceDelay <= maxQueisceDelay; quiesceDelay <<= 1)
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
            return new TestResult(workloadName, numThreads, naiveLock.GetType().ToString(), numLockAcquires, workloadEntry.ReleaseSize, workloadEntry.AcquireSize, sw.Elapsed.TotalMilliseconds, quiesceDelay);
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
