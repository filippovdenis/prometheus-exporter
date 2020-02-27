using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PromSQLWrapper.Models;
using System.Data.SqlClient;

namespace PromSQLWrapper.Services
{
    public class DbContext
    {
        private readonly ILogger<DbContext> _logger;
        
        private readonly string _connectionString;

        private readonly MetricCollection _metricCollection;

        private readonly ColumnsConfig _columnsConfig;
        

        public DbContext(ILogger<DbContext> logger, IConfiguration config, MetricCollection metricCollection)
        {
            _logger = logger;
            
            _metricCollection = metricCollection; 
            
            _connectionString = config["ConnectionString"];
            
            _logger.LogDebug($"ConnectionString: {_connectionString}");
            
            var metricsFile = config["MetricsFile"];

            _columnsConfig = new ColumnsConfig(this, metricsFile);
        }

        private StringBuilder ExecuteSqlForConnection(SqlConnection conn, MetricConfig config)
        {
            var result = new StringBuilder();
            using (var command = new SqlCommand(config.Sql, conn))
            {
                using (var reader = command.ExecuteReader())
                {
                    try
                    {
                        //First, trying to parse the structure of the dataset and initialize 
                        // nameColumnIdx, valueColumnIdx and metricTypeColumnIdx variables
                        // and labelNames array
                        var fieldFlag = 0;
                        var nameColumnIdx = 0;
                        var valueColumnIdx = 0;
                        var metricTypeColumnIdx = 0;
                        var labelNames = new List<String>();
                        
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var fieldName = reader.GetName(i);

                            if (fieldName == _columnsConfig.NameColumn)
                            {
                                fieldFlag |= 1;
                                nameColumnIdx = i;
                            }
                            else if (fieldName == _columnsConfig.ValueColumn)
                            {
                                fieldFlag |= 2;
                                valueColumnIdx = i;
                            }
                            else if (fieldName == _columnsConfig.MetricTypeColumn)
                            {
                                fieldFlag |= 4;
                                metricTypeColumnIdx = i;
                            }
                            else
                            {
                                labelNames.Add(reader.GetName(i));                            
                            }
                        }     
                        
                        //if there are any of required columns are absent, prepare log msg and throw exception
                        var message = new StringBuilder();

                        if (nameColumnIdx == 0)
                            message.Append($"{_columnsConfig.NameColumn} is absent\n");

                        if (valueColumnIdx == 0)
                            message.Append($"{_columnsConfig.ValueColumn} is absent\n");
                        
                        if (metricTypeColumnIdx == 0)
                            message.Append($"{_columnsConfig.MetricTypeColumn} is absent\n");                        

                        if (fieldFlag != 7)
                        {
                            message.Append($"at {config.Sql}\n");
                            
                            throw new Exception(message.ToString());
                        }                       
                        
                        //now reading dataset string by string and preparing output immediately
                        var oldMetric = "";
                        
                        while (reader.Read())
                        {
                            double value = 0;

                            var name = reader.GetString(nameColumnIdx);

                            var fullName = $"{config.NamePrefix}_{name}";
                            
                            value = (double)reader.GetDecimal(valueColumnIdx);                   
                        
                            var metricType = reader.GetString(metricTypeColumnIdx);

                            if (!name.Equals(oldMetric))
                            {
                                //write #TYPE and #HELP strings
                                result.Append($"# HELP {fullName} {fullName}\n");
                                result.Append($"# TYPE {fullName} {metricType}\n");

                                oldMetric = name;
                            }

                            result.Append($"{fullName}");

                            //preparing labels
                            if (labelNames.Count > 0)
                            {
                                result.Append("{");
                                
                                var labelNum = 0;
                                
                                for (var i = 0; i < reader.FieldCount; i++)
                                {
                                    var fieldName = reader.GetName(i);

                                    if (fieldName != _columnsConfig.NameColumn && fieldName != _columnsConfig.ValueColumn && fieldName != _columnsConfig.MetricTypeColumn)
                                    {
                                        //next label value
                                        if (labelNum > 0)
                                            result.Append(",");
                                        
                                        result.Append($"{labelNames[labelNum]}=\"{reader.GetString(i)}\"");
                                        labelNum++;
                                    }
                                } 
                                result.Append("}");
                            }

                            result.Append($" {value.ToString()}\n");
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e.Message);
                    }
                }
            }

            return result;
        }

        private StringBuilder ExecuteSqlPrimary(MetricConfig config)
        {
            StringBuilder result = null;
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();

                    try
                    {
                        result = ExecuteSqlForConnection(connection, config);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }
            }

            return result;
        }

        public StringBuilder executeSqlOnROReplica(MetricConfig config)
        {
            StringBuilder result = null;
            var connString = $"{_connectionString};ApplicationIntent=ReadOnly";
            _logger.LogDebug($"Connection String: {connString}");

            using (var connection = new SqlConnection(connString))
            {
                try
                {
                    connection.Open();

                    try
                    {
                        result = ExecuteSqlForConnection(connection, config);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }                
            }
        
            return result;
        }

        public StringBuilder GetMetrics()
        {
            var sb = new StringBuilder();
            StringBuilder s = null;

            foreach (var cm in _metricCollection.ConfigList)
            {
                if (!cm.ReadOnlyFlag)
                    s = ExecuteSqlPrimary(cm);
                else
                {
                    s = executeSqlOnROReplica(cm);
                }

                sb.Append(s);
            }

            return sb;
        }


        
        private class ColumnsConfig
        {
            private DbContext _parent;
            
            public string NameColumn { get; private set; }
        
            public string ValueColumn { get; private set; }
            public string MetricTypeColumn { get; private set; }

            public ColumnsConfig(DbContext parent, string metricsFile)
            {
                _parent = parent;
                XDocument xdoc;
                try
                {
                    xdoc = XDocument.Load(metricsFile);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Cannot load xml config from ${metricsFile}\nOriginal message: {ex.Message}" );
                }

                foreach (var element in xdoc.Element("configuration").Element("columns").Elements("column"))
                {
                    XAttribute nameAttr = element.Attribute("name");
                
                    switch (nameAttr.Value)
                    {
                        case "NameColumn":
                            NameColumn = element.Value;
                            break;
                        case "ValueColumn":
                            ValueColumn = element.Value;
                            break;
                        case "MetricTypeColumn":
                            MetricTypeColumn = element.Value;
                            break;
                    }                
                }
            
                if (NameColumn == "")
                {
                    _parent._logger.LogError("There is no NameColumn setting in xml file");
                    throw new Exception("There is no NameColumn setting in file " + metricsFile);
                }

                if (ValueColumn == "")
                {
                    _parent._logger.LogError("There is no ValueColumn setting in xml file");
                    throw new Exception("There is no ValueColumn setting in file " + metricsFile);
                }

                if (MetricTypeColumn == "")
                {
                    _parent._logger.LogError("There is no MetricTypeColumn setting in xml file");
                    throw new Exception("There is no MetricTypeColumn setting in file " + metricsFile);
                }                            
            }
        }
    }
}