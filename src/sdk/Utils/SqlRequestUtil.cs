//-----------------------------------------------------------------------------
// <copyright file="SqlRequestUtil.cs" company="Amazon.com">
//      Copyright 2020 Amazon.com, Inc. or its affiliates. All Rights Reserved.
//
//      Licensed under the Apache License, Version 2.0 (the "License").
//      You may not use this file except in compliance with the License.
//      A copy of the License is located at
//
//      http://aws.amazon.com/apache2.0
//
//      or in the "license" file accompanying this file. This file is distributed
//      on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
//      express or implied. See the License for the specific language governing
//      permissions and limitations under the License.
// </copyright>
//-----------------------------------------------------------------------------

using Amazon.Runtime.Internal.Util;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Exceptions;
using Amazon.XRay.Recorder.Core.Internal.Entities;
#if NET45
using Amazon.XRay.Recorder.Core.Internal.Utils;
#endif
using Amazon.XRay.Recorder.Core.Sampling;
using System;
using System.Data.Common;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Amazon.XRay.Recorder.AutoInstrumentation.Utils
{
    /// <summary>
    /// Utils for Sql request
    /// </summary>
    public static class SqlRequestUtil
    {
        // 1st alternative: (?:'([^']|'')*') matches single quoted literals, i.e. string, datatime.
        // Example:
        //      'apple'
        //      'very ''strong'''
        //      ''
        // 2nd alternative: (?:(-|\+)?\$?\d+(\.\d+)? matches number and money
        // Example:
        //      123.12
        //      -123
        //      +12
        //      $123.12
        //      -$123.12
        private static readonly Regex _sqlLiteralRegex = new Regex(@"(?:'([^']|'')*')|(?:(-|\+)?\$?\d+(\.\d+)?)");
        private static readonly Regex _portNumberRegex = new Regex(@"[,|:]\d+$");

        private static readonly Logger _logger = Logger.GetLogger(typeof(SqlRequestUtil));

        /// <summary>
        /// Sanitizes the TSQL query.
        /// </summary>
        internal static string SanitizeTsqlQuery(string query)
        {
            return _sqlLiteralRegex.Replace(query, "?");
        }

        /// <summary>
        /// Removes the port number from data source.
        /// </summary>
        internal static string RemovePortNumberFromDataSource(string dataSource)
        {
            return _portNumberRegex.Replace(dataSource, string.Empty);
        }

        /// <summary>
        /// Begin subsegment and add name space.
        /// </summary>
        internal static void BeginSubsegment(DbCommand command)
        {
            AWSXRayRecorder.Instance.BeginSubsegment(BuildSubsegmentName(command));
            AWSXRayRecorder.Instance.SetNamespace("remote");
        }

        /// <summary>
        /// Process command.
        /// </summary>                                                                                                                                                                           
        internal static void ProcessCommand(DbCommand command)
        {
            CollectSqlInformationDefault(command);
        }

        /// <summary>
        /// Process data from SqlEventListener
        /// </summary>
        internal static void ProcessEventData(EventWrittenEventArgs sqlEventData)
        {
            int id = Convert.ToInt32(sqlEventData.Payload[0], CultureInfo.InvariantCulture);
            string dataSource = Convert.ToString(sqlEventData.Payload[1], CultureInfo.InvariantCulture);
            string database = Convert.ToString(sqlEventData.Payload[2], CultureInfo.InvariantCulture);
            string commandText = Convert.ToString(sqlEventData.Payload[3], CultureInfo.InvariantCulture);

            if (string.IsNullOrEmpty(database) || string.IsNullOrEmpty(dataSource))
            {
                return;
            }

            string subsegmentName = database + "@" + RemovePortNumberFromDataSource(dataSource);

            var recorder = AWSXRayRecorder.Instance;
            recorder.BeginSubsegment(subsegmentName);
            recorder.SetNamespace("remote");
            recorder.AddSqlInformation("database_type", "sqlserver");

            if (ShouldCollectSqlText())
            {
                if (!string.IsNullOrEmpty(commandText))
                {
                    recorder.AddSqlInformation("sanitized_query", commandText);
                }
            }
        }

        /// <summary>
        /// Process exception.
        /// </summary>
        internal static void ProcessException(Exception exception)
        {
            AWSXRayRecorder.Instance.AddException(exception);
        }

        /// <summary>
        /// End subsegment.
        /// </summary>
        internal static void EndSubsegment()
        {
            AWSXRayRecorder.Instance.EndSubsegment();
        }

        /// <summary>
        /// End subsegment and emit it to Daemon.
        /// </summary>
        internal static void EndSubsegment(Subsegment subsegment)
        {
            var recorder = AWSXRayRecorder.Instance;
            if (recorder.IsTracingDisabled())
            {
                _logger.DebugFormat("X-Ray tracing is disabled, do not end subsegment");
                return;
            }

            if (subsegment.Sampled != SampleDecision.Sampled)
            {
                return;
            }

            subsegment.IsInProgress = false;

            // Restore parent segment to trace context
            if (subsegment.Parent != null)
            {
                recorder.TraceContext.SetEntity(subsegment.Parent);
            }

            // Drop ref count
            subsegment.Release();
            subsegment.SetEndTimeToNow();

            // Check emittable
            if (subsegment.IsEmittable())
            {
                // Emit
                recorder.Emitter.Send(subsegment.RootSegment);
            }
            else if (recorder.StreamingStrategy.ShouldStream(subsegment))
            {
                recorder.StreamingStrategy.Stream(subsegment.RootSegment, recorder.Emitter);
            }
        }

        /// <summary>
        /// Records the SQL information on the current subsegment.
        /// </summary>
        private static void CollectSqlInformationDefault(DbCommand command)
        {
            var recorder = AWSXRayRecorder.Instance;
            var databaseType = AgentUtil.GetDataBaseType(command);
            recorder.AddSqlInformation("database_type", databaseType);

            recorder.AddSqlInformation("database_version", command.Connection.ServerVersion);

            DbConnectionStringBuilder connectionStringBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = command.Connection.ConnectionString
            };

            // Remove sensitive information from connection string
            connectionStringBuilder.Remove("Password");

            var userId = AgentUtil.GetUserId(connectionStringBuilder);
            // Do a pre-check for user ID since in the case of TrustedConnection, a user ID may not be available.
            if (userId != null)
            {
                recorder.AddSqlInformation("user", userId.ToString());
            }

            recorder.AddSqlInformation("connection_string", connectionStringBuilder.ToString());

            if (ShouldCollectSqlText())
            {
                recorder.AddSqlInformation("sanitized_query", command.CommandText);
            }
        }

        /// <summary>
        /// Builds the name of the subsegment in the format database@datasource
        /// </summary>
        private static string BuildSubsegmentName(DbCommand command)
            => command.Connection.Database + "@" + RemovePortNumberFromDataSource(command.Connection.DataSource);

        /// <summary>
        /// Check if subsegment should collect Sql command text.
        /// </summary>
#if !NET45
        internal static bool ShouldCollectSqlText()
            => AWSXRayRecorder.Instance.XRayOptions.CollectSqlQueries;
#else
        internal static bool ShouldCollectSqlText()
            => AppSettings.CollectSqlQueries;
#endif

        /// <summary>
        /// Check if it's within an ef core subsegment, if so, skip it
        /// </summary>
        internal static bool IsTraceable()
        {
            try
            {
                var subsegment = AWSXRayRecorder.Instance.GetEntity() as Subsegment;
                if (subsegment == null || subsegment.Sql == null)
                {
                    return true;
                }
                if (subsegment.IsInProgress && subsegment.Sql.Count > 0)
                {
                    return false;
                }
            }
            catch (EntityNotAvailableException e)
            {
                AWSXRayRecorder.Instance.TraceContext.HandleEntityMissing(AWSXRayRecorder.Instance, e, "Failed to get entity since it is not available in trace context.");
            }
            
            return true;
        }
    }
}
