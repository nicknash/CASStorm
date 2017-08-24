namespace CASStorm.Locks
{
    interface ILock
    {
        void Enter(int threadIdx);
        void Exit();
    }
}