using BenchmarkDotNet.Running;

namespace DjvuNet.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<ImageCacheBenchmark>();
        }
    }
}
