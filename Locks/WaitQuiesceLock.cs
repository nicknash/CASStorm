namespace CASStorm.Locks
{
    sealed class WaitQuiesceLock : ILock
    {
        private readonly int _acquireDelayNanos;
        private readonly ILock _naiveTTASLock;

        public WaitQuiesceLock(int acquireDelayNanos)
        {
            _naiveTTASLock = new NaiveTestAndTestSpinLock();
            _acquireDelayNanos = acquireDelayNanos;
        }

        public void Enter(int threadIdx)
        {
            _naiveTTASLock.Enter(0);
            Utils.WaitNanos(_acquireDelayNanos);
        }

        public void Exit()
        {
            _naiveTTASLock.Exit();
        }
    }
}