using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using PromSQLWrapper.Services;

namespace PromSQLWrapper.Models

{
    public class MetricCollection
    {
        private Dictionary<string, (string, Dictionary<string, double>)> metricDict;

        private List<string> _labelNames;

        private ILogger<DbContext> _logger;

        public MetricCollection(ILogger<DbContext> logger, List<string> labelNames)
        {
            metricDict = new Dictionary<string, (string, Dictionary<string, double>)>();
            _labelNames = labelNames;
            _logger = logger;
        }

        public void Add(string fullName, string metricType, double value, List<string> labelVals)
        {
            Dictionary<string, double> dict;
            
                        
            if (metricDict.ContainsKey(fullName))
            {
                string mType;
                _logger.LogTrace($"{fullName} found");
                (mType , dict) = metricDict[fullName];
                if (mType != metricType)
                {
                    throw new Exception($"Metric type of {fullName} has been changed from {metricType} to {mType}");
                }
            }
            else
            {
                _logger.LogTrace($"{fullName} NOT found");
                dict = new Dictionary<string, double>();
                metricDict.Add(fullName, (metricType, dict));
            }

            var labBuilder = new StringBuilder();
            
            if (_labelNames.Count > 0)
            {
                _logger.LogTrace($"Labels count: {_labelNames.Count}");
                labBuilder.Append("{");
                var isFirst = true;
                for (var i = 0; i < _labelNames.Count; i++)
                {
                    _logger.LogTrace($"i: {i}");
                    _logger.LogTrace($"isFirst: {isFirst}");
                    
                    if (!isFirst)
                    {
                        labBuilder.Append(",");
                    }
                    else
                    {
                        isFirst = false;
                    }
                    labBuilder.Append($"{_labelNames[i]}=\"{labelVals[i]}\"");
                    _logger.LogTrace($"labBuilder: {labBuilder}");
                }
                labBuilder.Append("}");
            }

            var s = labBuilder.ToString();
            
            if (!dict.ContainsKey(s))
            {
                dict.Add(s, value);
                _logger.LogDebug($"Labels [{s}] added for value {value}");
            }
            else
            {
                _logger.LogDebug($"Labels [{s}] for value {value} already exist");
            }
        }

        public StringBuilder getMetricSB()
        {
            var result = new StringBuilder();
            foreach (var kvPair in metricDict)
            {
                var (metType, dict) = kvPair.Value;
                result.Append($"# HELP {kvPair.Key} {kvPair.Key}\n");
                result.Append($"# TYPE {kvPair.Key} {metType}\n");

                foreach (var labMetric in dict)
                {
                    result.Append($"{kvPair.Key}{labMetric.Key} {labMetric.Value}\n");
                }
            }

            return result;
        }

    }
}