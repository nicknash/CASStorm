using System.Threading;

sealed class UnscalableTicketLock : INaiveSpinLock
{
    private long _currentTicket = 1;
    private long _nextTicket;

    public void Enter()
    {
        long myTicket = Interlocked.Increment(ref _nextTicket);
        while (myTicket != Volatile.Read(ref _currentTicket))
        {
            continue;
        }
    }

    public void Exit()
    {
         Volatile.Write(ref _currentTicket, _currentTicket + 1);
    }
}