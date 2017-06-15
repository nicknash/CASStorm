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

            RunTest(numIterations, new NaiveAggressiveSpinLock(), minThreads, maxThreads);            
            RunTest(numIterations, new NaiveTestAndTestSpinLock(), minThreads, maxThreads);            

            RunTest(numIterations, new NaiveTestAndTestSpinLock(), minThreads, maxThreads);            
            RunTest(numIterations, new NaiveAggressiveSpinLock(), minThreads, maxThreads);      
            /*
            Astonishing output: 2nd iteration of naive aggressive is quadratic in num threads! 
            heat / throttling effect? Results generally not easily replicable.
            minThreads=1, maxThreads=8, numIterations=100000

            1 --> 190
2 --> 249
3 --> 377
4 --> 527
5 --> 804
6 --> 1095
7 --> 1307
8 --> 1559
1 --> 192
2 --> 265
3 --> 412
4 --> 557
5 --> 914
6 --> 1239
7 --> 1454
8 --> 1641
1 --> 196
2 --> 250
3 --> 383
4 --> 522
5 --> 895
6 --> 1244
7 --> 1511
8 --> 1803
1 --> 192
2 --> 2803
3 --> 9407
4 --> 16251
5 --> 25920
6 --> 35598
7 --> 46149
8 --> 57302
            */      
        }

        private static void RunTest(int numIterations, INaiveSpinLock naiveLock, int minThreads, int maxThreads)
        {
            var a = new int[256];
            Action fill = () => {
                for(int i = 0; i < a.Length; ++i)
                {
                    a[i] += i;
                }
            };
            Console.WriteLine($"----- Beginning Test Run {naiveLock.GetType()} minThreads={minThreads}, maxThreads={maxThreads}, numIterations={numIterations} ------");
            for(int i = minThreads; i <= maxThreads; ++i)
            {
                var sw = new Stopwatch();
                var barrier = new Barrier(i + 1);
                var threads = new Thread[i];
                for(int j = 0; j < i; ++j)
                {
                    threads[j] = new Thread(new ThreadStart(GetWorkload(numIterations, naiveLock, barrier, fill)));
                    threads[j].Start();
                }
                barrier.SignalAndWait();
                sw.Start();
                barrier.SignalAndWait();                
                sw.Stop();
                Console.WriteLine($"{i} --> {sw.ElapsedMilliseconds}");
            }
        }

        private static Action GetWorkload(int numIterations, INaiveSpinLock naiveLock, Barrier barrier, Action doIteration)
        {
            return () => 
            {
                barrier.SignalAndWait();        
                var s = new Stopwatch();
                var r = new Random();
                for(int i = 0; i < numIterations; ++i)
                {
                    naiveLock.Enter();
                    doIteration();
                    naiveLock.Exit();
                    s.Reset();
                    s.Start();
                    int ticks = r.Next(1500);
                    while(s.ElapsedTicks < ticks) Thread.Sleep(0);
                }
                barrier.SignalAndWait();
            };
        }
    }
}
