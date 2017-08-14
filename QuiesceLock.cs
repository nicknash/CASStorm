using System.Threading;

sealed class QuiesceLock : INaiveSpinLock
{
    private readonly int _acquireAttemptDelayIterations;

    private int _held;

    public int Canary;

    public QuiesceLock(int acquireAttemptDelayIterations)
    {
        _acquireAttemptDelayIterations = acquireAttemptDelayIterations;
    }

    public void Enter()
    {
        while(true)
        {
            if(Volatile.Read(ref _held) == 1)
            {
                continue;
            }
            for(int i = 0; i < _acquireAttemptDelayIterations; ++i)
            {
                Canary += (Canary & i) * (i + _acquireAttemptDelayIterations);
            }
            if(Interlocked.CompareExchange(ref _held, 1, 0) == 0)
            {
                break;
            }
        }
    }

    public void Exit()
    {
         Volatile.Write(ref _held, 0);
    }
}