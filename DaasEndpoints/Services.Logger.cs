﻿using System;
using System.Collections.Generic;
using System.IO;
using Executor;
using Microsoft.WindowsAzure.StorageClient;
using Orchestrator;
using RunnerInterfaces;
using System.Linq;
using Newtonsoft.Json;
using System.Threading;
using AzureTables;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure;
using SimpleBatch;

namespace DaasEndpoints
{
    // Services related to logging
    public partial class Services
    {
        public IFunctionUpdatedLogger GetFunctionInvokeLogger()
        {
            return new FunctionInvokeLogger
            {
                _account = _account,
                _tableName = EndpointNames.FunctionInvokeLogTableName,
            };
        }

        public IFunctionInstanceQuery GetFunctionInvokeQuery()
        {
            IAzureTableReader<ExecutionInstanceLogEntity> tableLookup = GetFunctionLookupTable();
            return new ExecutionStatsAggregator(tableLookup);
        }

        // Used by Executors to notify of completed functions
        // Will send a message to orchestrator to aggregate stats together.
        public ExecutionStatsAggregatorBridge GetStatsAggregatorBridge()
        {
            var queue = this.GetExecutionCompleteQueue();
            return new ExecutionStatsAggregatorBridge(queue);
        }
        
        // Actually does the aggregation. Receives a message from the bridge.
        public IFunctionCompleteLogger GetStatsAggregator()
        {
            IAzureTableReader<ExecutionInstanceLogEntity> tableLookup = GetFunctionLookupTable();
            var tableStatsSummary = GetInvokeStatsTable();
            var tableMru = GetIndexTable(EndpointNames.FunctionInvokeLogIndexMru);
            var tableMruByFunction = GetIndexTable(EndpointNames.FunctionInvokeLogIndexMruFunction);
            var tableMruByFunctionSucceeded = GetIndexTable(EndpointNames.FunctionInvokeLogIndexMruFunctionSucceeded);
            var tableMruFunctionFailed = GetIndexTable(EndpointNames.FunctionInvokeLogIndexMruFunctionFailed);

            return new ExecutionStatsAggregator(
                tableLookup,
                tableStatsSummary,
                tableMru,
                tableMruByFunction,
                tableMruByFunctionSucceeded,
                tableMruFunctionFailed);
        }

        // Table that maps function types to summary statistics. 
        // Table is populated by the ExecutionStatsAggregator
        public AzureTable<FunctionLocation, FunctionStatsEntity> GetInvokeStatsTable()
        {
            return new AzureTable<FunctionLocation, FunctionStatsEntity>(
                _account,
                EndpointNames.FunctionInvokeStatsTableName,
                 row => Tuple.Create("1", row.ToString()));
        }

        private IAzureTable<FunctionIndexPointer> GetIndexTable(string tableName)
        {
            return new AzureTable<FunctionIndexPointer>(_account, tableName);
        }

        private IAzureTableReader<ExecutionInstanceLogEntity> GetFunctionLookupTable()
        {
            return new AzureTable<ExecutionInstanceLogEntity>(
                  _account,
                  EndpointNames.FunctionInvokeLogTableName);
        }
    }
}