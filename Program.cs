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
            int numIterations = Int32.Parse(args[2]);
            Console.WriteLine(Stopwatch.Frequency/1e6);
            Console.WriteLine(Stopwatch.IsHighResolution);

            RunTest(numIterations, new NaiveAggressiveSpinLock(), minThreads, maxThreads);            
            RunTest(numIterations, new NaiveTestAndTestSpinLock(), minThreads, maxThreads);            
        }

        private static void RunTest(int numIterations, INaiveSpinLock naiveLock, int minThreads, int maxThreads)
        {
            for(int i = minThreads; i <= maxThreads; ++i)
            {
                var sw = new Stopwatch();
                var barrier = new Barrier(i);
                var threads = new Thread[i];
                sw.Start();
                for(int j = 0; j < i; ++j)
                {
                    threads[j] = new Thread(new ThreadStart(GetWorkload(numIterations, naiveLock, barrier)));
                    threads[j].Start();
                }
                for(int j = 0; j < i; ++j)
                {
                    threads[j].Join();
                }
                sw.Stop();
                Console.WriteLine($"{i} --> {sw.ElapsedMilliseconds}");
            }
        }

        private static Action GetWorkload(int numIterations, INaiveSpinLock naiveLock, Barrier barrier)
        {
            return () => 
            {
                barrier.SignalAndWait();        
                double x = 100, y = 200;
                var s = new Stopwatch();
                for(int i = 0; i < numIterations; ++i)
                {
                    s.Reset();
                    s.Start();
                    naiveLock.Enter();/* 
                    for(int j = 0; j < 10000 + i % 3; ++j)
                    {
                        x += j / y;
                        y -= i * j;
                    }*/
                    while(s.ElapsedTicks < 15000) ;
                    naiveLock.Exit();
                }
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} computed: {x}, {y}");
                barrier.SignalAndWait();                
            };
        }
    }
}
