
namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class ProcessResultWithStats
    {
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
        public int ExitCode { get; set; }
        public double AvgCpu { get; set; }
        public double AvgMemoryInMB { get; set; }
    }
}
