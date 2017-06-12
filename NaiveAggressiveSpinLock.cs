using System.Threading;

sealed class NaiveAggressiveSpinLock : INaiveSpinLock
{
    // private bool _held;
    private int _held;

    public void Enter()
    {
        while(Interlocked.CompareExchange(ref _held, 1, 0) != 1)
        {
            continue;
        }
    }

    public void Exit()
    {
        Volatile.Write(ref _held, 0);
    }
}