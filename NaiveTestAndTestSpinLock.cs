using System.Threading;

sealed class NaiveTestAndTestSpinLock : INaiveSpinLock
{
    private int _held;

    public void Enter(int unused)
    {
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