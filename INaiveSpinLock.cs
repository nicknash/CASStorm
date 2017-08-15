interface INaiveSpinLock
{
    void Enter(int threadIdx);
    void Exit();
}