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
            int numPassesPerAcquire = 0;//Int32.Parse(args[3]);
            var lockType = args[3];
            
            var naiveLock = GetLock(lockType);
            RunTest(numLockAcquires, numPassesPerAcquire, naiveLock, minThreads, maxThreads);            
        }

        private static INaiveSpinLock GetLock(string type)
        {
            switch(type)
            {
                case "na":
                    return new NaiveAggressiveSpinLock();
                case "ntat":
                    return new NaiveTestAndTestSpinLock();
            }
            throw new Exception($"Unknown lock type {type}");
        }

        private static void RunTest(int numLockAcquires, int numPassesPerAcquire, INaiveSpinLock naiveLock, int minThreads, int maxThreads)
        {
            var a = new int[256];
            Action fill = () => {
                for(int i = 0; i < a.Length; ++i)
                {
                    a[i] += i;
                }
            };
            Console.WriteLine($"----- Beginning Test Run Lock={naiveLock.GetType()} minThreads={minThreads}, maxThreads={maxThreads}, numLockAcquires={numLockAcquires}, numPassesPerAcqure={numPassesPerAcquire} ------");
            Console.WriteLine($"NumThreads,TotalMillisecons");
            for(int i = minThreads; i <= maxThreads; ++i)
            {
                var sw = new Stopwatch();
                var barrier = new Barrier(i + 1);
                var threads = new Thread[i];
                var acquireAction = new AcquireAction(numLockAcquires, fill);
                for(int j = 0; j < i; ++j)
                {
                    threads[j] = new Thread(new ThreadStart(GetWorkload(naiveLock, barrier, acquireAction)));
                    threads[j].Start();
                }
                barrier.SignalAndWait();
                sw.Start();
                barrier.SignalAndWait();                
                sw.Stop();
                Console.WriteLine($"{i},{sw.ElapsedMilliseconds}");
            }
        }

        class AcquireAction
        {
            private readonly int _numAcquires;
            private readonly Action _work;
            private int _acquireCount;

            public bool Finished => _acquireCount >= _numAcquires;

            public AcquireAction(int numAcquires, Action work)
            {
                _numAcquires = numAcquires;
                _work = work;
            }

            public void DoWork()
            {
                _work();
                _acquireCount++;
            }
        }

        private static Action GetWorkload(INaiveSpinLock naiveLock, Barrier barrier, AcquireAction acquireAction)
        {
            return () => 
            {
                barrier.SignalAndWait();        
                while(!acquireAction.Finished)
                {
                    naiveLock.Enter();
                    acquireAction.DoWork(); // Don't bother double checking here, only off by numThreads acquires.
                    naiveLock.Exit();
                }
                barrier.SignalAndWait();
            };
        }
    }
}
