using Code.Renderer;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Code.Simulation
{
    [BurstCompile]
    public struct DirectRaycast : IJobParallelFor
    {
        [ReadOnly] public BinauralAudioFilter filter;
        
        public void Execute(int index)
        {
            throw new System.NotImplementedException();
        }
    }
}