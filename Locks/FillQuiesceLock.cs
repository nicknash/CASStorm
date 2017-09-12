namespace CASStorm.Locks
{
    sealed class FillQuiesceLock : ILock
    {
        private readonly int _acquireAttemptDelayIterations;
        private readonly ILock _naiveTTASLock;

        public int[] _canary;

        public FillQuiesceLock(int acquireAttemptDelayIterations, int numThreads)
        {
            _naiveTTASLock = new NaiveTestAndTestSpinLock();
            _acquireAttemptDelayIterations = acquireAttemptDelayIterations;
            _canary = new int[numThreads << 7]; // Conservatively large spacing to try and avoid false sharing.
        }

        public void Enter(int threadIdx)
        {
            _naiveTTASLock.Enter(0);
            for (int i = 0; i < _acquireAttemptDelayIterations; ++i)
            {
                _canary[threadIdx << 7] += (_canary[threadIdx << 7] & i) * (i + _acquireAttemptDelayIterations);
            }
        }

        public void Exit()
        {
            _naiveTTASLock.Exit();
        }
    }
}