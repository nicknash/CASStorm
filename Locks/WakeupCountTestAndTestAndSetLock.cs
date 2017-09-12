using System.Threading;

namespace CASStorm.Locks
{
    sealed class WakeupCountTestAndTestAndSetLock : ILock
    {
        private int _held;

        public int NumWakeUps;

        public void Enter(int unused)
        {
            while(true)
            {
                if (Volatile.Read(ref _held) == 0)
                {
                    Interlocked.Increment(ref NumWakeUps);
                    if (Interlocked.CompareExchange(ref _held, 1, 0) == 0)
                    {
                        break;
                    }
                }
            }
            
            while (Volatile.Read(ref _held) == 1 || Interlocked.CompareExchange(ref _held, 1, 0) == 1)
            {
                continue;
            }
        }

        public void Exit()
        {
            Volatile.Write(ref _held, 0);
        }
    }    
}