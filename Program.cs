using System;
using System.Threading;
using System.Diagnostics;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            int minThreads = Int32.Parse(args[0]);
            int maxThreads = Int32.Parse(args[1]);
            int numLockAcquires = Int32.Parse(args[2]);
            int numReleaseIterations = Int32.Parse(args[3]);
            var lockType = args[4];
            int minSize = Int32.Parse(args[5]);
            int maxSize = Int32.Parse(args[6]);
            
            var naiveLock = GetLock(lockType);
            for(int size = minSize; size <= maxSize; size <<= 1)
            {
                for(int numThreads = minThreads; numThreads <= maxThreads; ++numThreads)
                {
                    RunContendingTest(numLockAcquires, numReleaseIterations, naiveLock, numThreads, size);
                }
            }
            // Bus quiescensce test:
            for(int quiesceDelay = 0; quiesceDelay <= 4096; quiesceDelay <<= 1)
            {
                for(int numThreads = minThreads; numThreads <= maxThreads; ++numThreads)
                {
                    // TODO.
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

        private static void RunContendingTest(int numLockAcquires, int numReleaseIterations, INaiveSpinLock naiveLock, int numThreads, int size)
        {
            var a = new int[256];
            Action fill = () => Fill(a);
            Console.WriteLine($"NumThreads,LockType,Size,NumAcquires,TotalMilliseconds,NormalizedNanoseconds");
            var sw = new Stopwatch();
            var barrier = new Barrier(numThreads + 1);
            var threads = new Thread[numThreads];
            for (int j = 0; j < numThreads; ++j)
            {
                var p = new int[a.Length];
                Action releaseAction = () =>
                {
                    for (int n = 0; n < numReleaseIterations; ++n)
                    {
                        Fill(p);
                    }
                };
                threads[j] = new Thread(new ThreadStart(GetWorkload(naiveLock, barrier, numLockAcquires / numThreads, fill, releaseAction)));
                threads[j].Start();
            }
            barrier.SignalAndWait();
            sw.Start();
            barrier.SignalAndWait();
            sw.Stop();
            Console.WriteLine($"{numThreads},{naiveLock.GetType()},{numLockAcquires},{size},{sw.ElapsedMilliseconds},{sw.ElapsedMilliseconds * 1e6 / (numLockAcquires * (1 + numReleaseIterations))}");
        }

        private static void Fill(int[] a)
        {
            for (int i = 0; i < a.Length; ++i)
            {
                a[i] += i;
            }
        }

        private static Action GetWorkload(INaiveSpinLock naiveLock, Barrier barrier, int numAcquires, Action acquireAction, Action releaseAction)
        {
            return () => 
            {
                barrier.SignalAndWait();  
                for(int i = 0; i < numAcquires; ++i)
                {
                    naiveLock.Enter();
                    acquireAction();
                    naiveLock.Exit();
                    releaseAction();
                }
                barrier.SignalAndWait();
            };
        }
    }
}
