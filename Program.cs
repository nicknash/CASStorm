using System;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace ConsoleApplication
{
    class TestResult
    {
        public int NumThreads { get; }
        public string LockType { get; }
        public int NumLockAcquires { get; }
        public int NumReleaseIterations { get; }
        public int CriticalSectionSize { get; }
        public double TotalMilliseconds { get; }

        public TestResult(int numThreads, string lockType, int numLockAcquires, int numReleaseIterations, int criticalSectionSize, double totalMilliseconds)
        {
            NumThreads = numThreads;
            LockType = lockType;
            NumLockAcquires = numLockAcquires;
            NumReleaseIterations = numReleaseIterations;
            CriticalSectionSize = criticalSectionSize;
            TotalMilliseconds = totalMilliseconds;
        }
    }

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
            var locks = new ILock[] { new NaiveAggressiveSpinLock(), new NaiveTestAndTestSpinLock(), new KernelLock()};
            int numScalingTestResults = locks.Length * (1 + maxSizePower - minSizePower) * (1 + maxThreads - minThreads) * (1 + maxReleaseIterationsBound);  
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
                for (int sizePower = minSizePower; sizePower <= maxSizePower; ++sizePower)
                {
                    for (int releaseIterationsBound = 0; releaseIterationsBound <= maxReleaseIterationsBound; ++releaseIterationsBound)
                    {
                        for (int numThreads = minThreads; numThreads <= maxThreads; ++numThreads)
                        {
                            int size = 1 << sizePower;
                            Console.Write($"                             \r{1 + scalingResultIdx}/{numScalingTestResults}");
                            scalingTestResults[scalingResultIdx] = RunContendingTest(numLockAcquires, maxReleaseIterationsBound, theLock, numThreads, releaseData, size, () => Fill(contendedData, size));
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

        private static TestResult RunContendingTest(int numLockAcquires, int maxReleaseIterations, ILock naiveLock, int numThreads, int[][] releaseData, int size, Action acquireAction)
        {
            var sw = new Stopwatch();
            var barrier = new Barrier(numThreads + 1);
            var threads = new Thread[numThreads];
            for (int i = 0; i < numThreads; ++i)
            {
                var r = new Random(i);
                var thisReleaseData = releaseData[2 * i];
                Action releaseAction = () =>
                {
                    int numReleaseIterations = r.Next(1 + maxReleaseIterations);
                    for (int j = 0; j < numReleaseIterations; ++j)
                    {
                        Fill(thisReleaseData, size);
                    }
                };
                threads[i] = new Thread(new ThreadStart(GetWorkload(naiveLock, barrier, i, numLockAcquires / numThreads, acquireAction, releaseAction)));
                threads[i].Start();
            }
            barrier.SignalAndWait();
            sw.Start();
            barrier.SignalAndWait();
            sw.Stop();
            return new TestResult(numThreads, naiveLock.GetType().ToString(), numLockAcquires, maxReleaseIterations, size, sw.Elapsed.TotalMilliseconds);
        }

        private static void Fill(int[] a, int length)
        {
            for (int i = 0; i < length; ++i)
            {
                a[i] += i;
            }
        }
        private static Action GetWorkload(ILock naiveLock, Barrier barrier, int threadIdx, int numAcquires, Action acquireAction, Action releaseAction)
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
