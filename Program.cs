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
            
            var naiveLock = GetLock(lockType);
            RunTest(numLockAcquires, numReleaseIterations, naiveLock, minThreads, maxThreads);            
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

        private static void RunTest(int numLockAcquires, int numReleaseIterations, INaiveSpinLock naiveLock, int minThreads, int maxThreads)
        {
            var a = new int[256];
            Action fill = () => Fill(a);
            Console.WriteLine($"----- Beginning Test Run Lock={naiveLock.GetType()} minThreads={minThreads}, maxThreads={maxThreads}, numLockAcquires={numLockAcquires}, numReleaseIterations={numReleaseIterations} ------");
            Console.WriteLine($"NumThreads,NumAcquires,TotalMilliseconds,NormalizedNanseconds");
            for(int i = minThreads; i <= maxThreads; ++i)
            {
                var sw = new Stopwatch();
                var barrier = new Barrier(i + 1);
                var threads = new Thread[i];
                var acquireAction = new AcquireAction(numLockAcquires, fill);
                for(int j = 0; j < i; ++j)
                {
                    var p = new int[a.Length];
                    Action releaseAction = () =>
                    {
                        for (int n = 0; n < numReleaseIterations; ++n)
                        {
                            Fill(p);
                        }
                    };
                    threads[j] = new Thread(new ThreadStart(GetWorkload(naiveLock, barrier, acquireAction, releaseAction)));
                    threads[j].Start();
                }
                barrier.SignalAndWait();
                sw.Start();
                barrier.SignalAndWait();                
                sw.Stop();
                Console.WriteLine($"{i},{numLockAcquires},{sw.ElapsedMilliseconds},{sw.ElapsedMilliseconds*1e6/(acquireAction.AcquireCount*(1 + numReleaseIterations))}");
            }
        }

        private static void Fill(int[] a)
        {
            for (int i = 0; i < a.Length; ++i)
            {
                a[i] += i;
            }
        }

        class AcquireAction
        {
            private readonly int _numAcquires;
            private readonly Action _work;
            public int AcquireCount { get; private set; }

            public bool Finished => AcquireCount >= _numAcquires;

            public AcquireAction(int numAcquires, Action work)
            {
                _numAcquires = numAcquires;
                _work = work;
            }

            public void DoWork()
            {
                _work();
                AcquireCount++;
            }
        }

        private static Action GetWorkload(INaiveSpinLock naiveLock, Barrier barrier, AcquireAction acquireAction, Action releaseAction)
        {
            return () => 
            {
                barrier.SignalAndWait();  
                while(!acquireAction.Finished)
                {
                    naiveLock.Enter();
                    acquireAction.DoWork(); // Don't bother double checking here, only off by numThreads acquires.
                    naiveLock.Exit();
                    releaseAction();
                }
                barrier.SignalAndWait();
            };
        }
    }
}
