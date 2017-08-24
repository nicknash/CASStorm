using System.Threading;

namespace CASStorm.Locks
{
    sealed class NaiveAggressiveSpinLock : ILock
    {
        private int _held;

        public void Enter(int unused)
        {
            while (Interlocked.CompareExchange(ref _held, 1, 0) != 0)
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