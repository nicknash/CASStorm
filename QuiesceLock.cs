sealed class QuiesceLock : INaiveSpinLock
{
    private readonly int _acquireAttemptDelayIterations;
    private readonly INaiveSpinLock _naiveTTASLock;

    public int[] _canary;

    public QuiesceLock(int acquireAttemptDelayIterations, int numThreads)
    {
        _naiveTTASLock = new NaiveTestAndTestSpinLock();
        _acquireAttemptDelayIterations = acquireAttemptDelayIterations;
        _canary = new int[numThreads << 7]; // Very
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