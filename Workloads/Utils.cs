using System.Diagnostics;

namespace CASStorm.Workloads
{
    class Utils
    {
        public static void Fill(int[] a, int start, int length)
        {
            for (int i = 0; i < length; ++i)
            {
                a[i] += i;
            }
        }
        public static void WaitNanos(double numNanos)
        {
            double numTicks = numNanos / Stopwatch.Frequency * 1e-9;
            var t = Stopwatch.GetTimestamp();
            while (Stopwatch.GetTimestamp() - t < numTicks) ;
        }
    }
}