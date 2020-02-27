using System;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PromSQLWrapper.Services
{
    public class SystemMetrics
    {
        private readonly Process _process;
        private ILogger<SystemMetrics> _logger;

        public SystemMetrics(ILogger<SystemMetrics> logger)
        {
            _process = Process.GetCurrentProcess();
            _logger = logger;
        }

        private StringBuilder addMetric(string name, string help, string metricType, long value)
        {
            var sb = new StringBuilder();
            sb.Append($"# HELP {name} {help}\n");
            sb.Append($"# TYPE {name} {metricType}\n");
            sb.Append($"{name} {value}\n");
            
            return sb;
        }

        public StringBuilder GetMetrics()
        {
            var sb = new StringBuilder();
            try
            {
                long l;
                _process.Refresh();
                // Working Set
                l = _process.WorkingSet64;
                sb.Append(addMetric("process_working_set_bytes", "Process working set", "gauge", l));
                
                //Private Memory
                l = _process.PrivateMemorySize64;
                sb.Append(addMetric("process_private_memory_bytes", "Process private memory size", "gauge", l));
                
                //Virtual Memory Size
                l = _process.VirtualMemorySize64;
                sb.Append(addMetric("process_virtual_memory_bytes", "Virtual memory size in bytes", "gauge", l));
                
                //Open Handles
                l = _process.HandleCount;
                sb.Append(addMetric("process_open_handles", "Number of open handles", "gauge", l));
                
                //Number of threads
                l = _process.Threads.Count;
                sb.Append(addMetric("process_num_threads", "Total number of threads", "gauge", l));
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }

            return sb;
        }
    }
}