﻿using System;
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
            int numReleaseIterations = Int32.Parse(args[3]);
            var lockType = args[4];
            int minSizePower = Int32.Parse(args[5]);
            int maxSizePower = Int32.Parse(args[6]);
            int maxQueisceDelayPower = Int32.Parse(args[7]);

            Console.WriteLine("Running scalability test: ");
            var naiveLock = GetLock(lockType); 
            int numScalingTestResults = (1 + maxSizePower - minSizePower) * (1 + maxThreads - minThreads);  
            var scalingTestResults = new TestResult[numScalingTestResults];
            int scalingResultIdx = 0;
            Console.Write($"[{new string(' ', numScalingTestResults)}]\r[");
            for (int sizePower = minSizePower; sizePower <= maxSizePower; ++sizePower)
            {
                for (int numThreads = minThreads; numThreads <= maxThreads; ++numThreads)
                {
                    int size = 1 << sizePower;
                    var a = new int[size];
                    scalingTestResults[scalingResultIdx] = RunContendingTest(numLockAcquires, numReleaseIterations, naiveLock, numThreads, size, () => Fill(a));
                    Console.Write("*");
                    ++scalingResultIdx;
                }
            }
            Console.WriteLine();
            var scalingResultsFileName = $"ScalingResults-{Process.GetCurrentProcess().Id}.csv";
            Console.WriteLine($"Writing test results to {scalingResultsFileName}");
            WriteTestResults(scalingResultsFileName, scalingTestResults);

            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("Running bus quiescensce test: ");
            int maxQueisceDelay = 1 << maxQueisceDelayPower;
            int numQuiesceResults = (1 + maxQueisceDelayPower) * (1 + maxThreads - minThreads);
            Console.Write($"[{new string(' ', numQuiesceResults)}]\r[");            
            var quiesceResults = new TestResult[numQuiesceResults];
            int quiesceResultIdx = 0;
            for(int quiesceDelay = 1; quiesceDelay <= maxQueisceDelay; quiesceDelay <<= 1)
            {
                for(int numThreads = minThreads; numThreads <= maxThreads; ++numThreads)
                {
                    var quiesceLock = new QuiesceLock(quiesceDelay, numThreads);
                    var a = new int[256];
                    quiesceResults[quiesceResultIdx] = RunContendingTest(numLockAcquires, numReleaseIterations, quiesceLock, numThreads, a.Length, () => Fill(a));
                    Console.Write("*");
                    ++quiesceResultIdx;
                }                
            }
            Console.WriteLine();
            var quiesceResultsFileName = $"QuiesceResults-{Process.GetCurrentProcess().Id}.csv";
            Console.WriteLine($"Writing test results to {quiesceResultsFileName}");
            WriteTestResults(quiesceResultsFileName, quiesceResults);
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

        private static INaiveSpinLock GetLock(string type)
        {
            switch(type)
            {
                case "na":
                    return new NaiveAggressiveSpinLock();
                case "ntat":
                    return new NaiveTestAndTestSpinLock();
                case "ut":
                    return new UnscalableTicketLock();
            }
            throw new Exception($"Unknown lock type {type}");
        }

        private static TestResult RunContendingTest(int numLockAcquires, int numReleaseIterations, INaiveSpinLock naiveLock, int numThreads, int size, Action acquireAction)
        {
            var sw = new Stopwatch();
            var barrier = new Barrier(numThreads + 1);
            var threads = new Thread[numThreads];
            for (int i = 0; i < numThreads; ++i)
            {
                var p = new int[size];
                Action releaseAction = () =>
                {
                    for (int j = 0; j < numReleaseIterations; ++j)
                    {
                        Fill(p);
                    }
                };
                threads[i] = new Thread(new ThreadStart(GetWorkload(naiveLock, barrier, i, numLockAcquires / numThreads, acquireAction, releaseAction)));
                threads[i].Start();
            }
            barrier.SignalAndWait();
            sw.Start();
            barrier.SignalAndWait();
            sw.Stop();
            return new TestResult(numThreads, naiveLock.GetType().ToString(), numLockAcquires, numReleaseIterations, size, sw.Elapsed.TotalMilliseconds);
        }

        private static void Fill(int[] a)
        {
            for (int i = 0; i < a.Length; ++i)
            {
                a[i] += i;
            }
        }
        private static Action GetWorkload(INaiveSpinLock naiveLock, Barrier barrier, int threadIdx, int numAcquires, Action acquireAction, Action releaseAction)
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
