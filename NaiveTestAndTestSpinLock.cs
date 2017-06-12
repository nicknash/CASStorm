using System.Threading;

sealed class NaiveTestAndTestSpinLock : INaiveSpinLock
{
    private int _held;

    public void Enter()
    {
        while (true)
        {
            while (Volatile.Read(ref _held) == 1)
            {
                continue;
            }
            if (Interlocked.CompareExchange(ref _held, 1, 0) == 1)
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