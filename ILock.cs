interface ILock
{
    void Enter(int threadIdx);
    void Exit();
}