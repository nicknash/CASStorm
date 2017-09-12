using System.Threading;

namespace CASStorm.Locks
{
    sealed class KernelLock : ILock
    {
        private object _syncObject = new object();

        public void Enter(int unused)
        {
            Monitor.Enter(_syncObject);
        }

        public void Exit()
        {
            Monitor.Exit(_syncObject);
        }
    }
}