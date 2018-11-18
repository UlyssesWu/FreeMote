using System.Runtime.InteropServices;

namespace FreeMote.Tools.Viewer
{
    /// <summary>
    /// 精确计时器类
    /// </summary>
    public class PreciseTimer
    {
        [System.Security.SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32")]
        private static extern bool QueryPerformanceFrequency(ref long PerformanceFrequency);
        [System.Security.SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32")]
        private static extern bool QueryPerformanceCounter(ref long PerformanceCount);
        long _ticksPerSecond = 0;
        long _previousElapsedTime = 0;
        public PreciseTimer()
        {
            QueryPerformanceFrequency(ref _ticksPerSecond);
            GetElaspedTime();//Get rid of first rubbish result
        }

        public double GetElaspedTime()
        {
            long time = 0;
            QueryPerformanceCounter(ref time);
            double elapsedTime = (double)(time - _previousElapsedTime) / (double)_ticksPerSecond;
            _previousElapsedTime = time;
            return elapsedTime;
        }
        //QueryPerformanceFrequency用于获取高分辨率性能计时器的频率。
        //QueryPerformanceCounter用于获取计时器的当前值。
        //结合起来确定最后一帧用了多长时间。
        //GetElaspedTime应该每帧调用一次，它会记录帧之间经过的时间。
    }
}
