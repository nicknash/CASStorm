using System.Threading;

sealed class UnscalableTicketLock : INaiveSpinLock
{
    private int _currentTicket = 1;
    private int _nextTicket;

    public void Enter()
    {
        int myTicket = Interlocked.Increment(ref _nextTicket);
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