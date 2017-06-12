using System;
using System.Threading;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            int maxThreads = Int32.Parse(args[0]);
            int numIterations = Int32.Parse(args[1]);
            
            for(int i = 2; i <= maxThreads; ++i)
            {
                var barrier = new Barrier(i);
                var threads = new Thread[i];
                var naiveLock = new NaiveAggressiveSpinLock();
                for(int j = 0; j < i; ++j)
                {
                    threads[j] = new Thread(new ThreadStart(GetWorkload(numIterations, naiveLock, barrier)));
                    threads[j].Start();
                }
                for(int j = 0; j < i; ++j)
                {
                    threads[j].Join();
                }
            }
        }

        private static Action GetWorkload(int numIterations, INaiveSpinLock naiveLock, Barrier barrier)
        {
            return () => 
            {
                barrier.SignalAndWait();        
                double x = 100, y = 200;
                for(int i = 0; i < numIterations; ++i)
                {
                    naiveLock.Enter();
                    for(int j = 0; j < 100 + i % 3; ++j)
                    {
                        x += j / y;
                        y -= i * j;
                    }
                    naiveLock.Exit();
                }
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} computed: {x}, {y}");
                barrier.SignalAndWait();                
            };
        }
    }
}
