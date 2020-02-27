using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PromSQLWrapper.Models;

namespace PromSQLWrapper.Services
{
    public class MetricCollection
    {
        private readonly ILogger<MetricCollection> _logger;

        private readonly IConfiguration AppConfiguration;

        public List<MetricConfig> ConfigList { get; }

        public MetricCollection(ILogger<MetricCollection> logger, IConfiguration config)
        {
            _logger = logger;
            AppConfiguration = config;

            ConfigList = new List<MetricConfig>();
            string metricsFile = AppConfiguration["MetricsFile"];

            try
            {
                XDocument xdoc = XDocument.Load(metricsFile);

                foreach (var element in xdoc.Element("configuration").Element("export_sqls").Elements("export_sql"))
                {
                    XAttribute prefixAttr = element.Attribute("name_prefix");
                    XAttribute roAttr = element.Attribute("readonlyconnecton");

                    bool ro = false;

                    if (roAttr != null)
                        ro = roAttr.Value.ToLower() == "true" || roAttr.Value.ToLower() == "yes" || roAttr.Value == "1";

                    var metricConfig = new MetricConfig
                        {NamePrefix = prefixAttr.Value, ReadOnlyFlag = ro, Sql = element.Element("sql").Value};

                    ConfigList.Add(metricConfig);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw new Exception("An error occurs while reading xml config file: " + ex.Message);
            }
        }
    }
}