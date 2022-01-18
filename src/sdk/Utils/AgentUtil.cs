//-----------------------------------------------------------------------------
// <copyright file="AgentUtil.cs" company="Amazon.com">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;

namespace Amazon.XRay.Recorder.AutoInstrumentation.Utils
{
    public static class AgentUtil
    {
        private static readonly Logger _logger = Logger.GetLogger(typeof(AgentUtil));

        private static readonly string EntityFramework = "EntityFramework";

        private static readonly string[] UserIdFormatOptions = { "user id", "username", "user", "userid" }; // case insensitive

        private static readonly string[] DatabaseTypes = { "sqlserver", "sqlite", "postgresql", "mysql", "firebirdsql", 
                                                           "inmemory" , "cosmosdb" , "oracle" , "filecontextcore" ,
                                                           "jet" , "teradata" , "openedge" , "ibm" , "mycat" , "vfp"};
        
        private static readonly string SqlServerCompact35 = "sqlservercompact35";
        private static readonly string SqlServerCompact40 = "sqlservercompact40";
        private static readonly string SqlServer = "sqlserver";

        /// <summary>
        /// Extract database_type from <see cref="DbCommand"/>.
        /// </summary>
        public static string GetDataBaseType(DbCommand command)
        {
            var typeString = command?.Connection?.GetType()?.FullName?.ToLower();

            // Won't be the case for Sql query through System.Data.SqlClient and Microsoft.Data.SqlClient
            // only for the edge case of sql query through Entity Framework and Entity Framework Core
            if (string.IsNullOrEmpty(typeString))
            {
                return EntityFramework;
            }

            if (typeString.Contains("microsoft.data.sqlclient") || typeString.Contains("system.data.sqlclient"))
            {
                return SqlServer;
            }

            if (typeString.Contains(SqlServerCompact35))
            {
                return SqlServerCompact35;
            }

            if (typeString.Contains(SqlServerCompact40))
            {
                return SqlServerCompact40;
            }

            foreach (var databaseType in DatabaseTypes)
            {
                if (typeString.Contains(databaseType))
                {
                    return databaseType;
                }
            }

            return typeString;
        }

        /// <summary>
        /// Extract user id from <see cref="DbConnectionStringBuilder"/>.
        /// </summary>
        public static object GetUserId(DbConnectionStringBuilder builder)
        {
            object value = null;
            foreach (string key in UserIdFormatOptions)
            {
                if (builder.TryGetValue(key, out value))
                {
                    break;
                }
            }
            return value;
        }

        /// <summary>
        /// Fetch property from reflection
        /// </summary>
        public static object FetchPropertyUsingReflection(object value, string item)
        {
            return value.GetType().GetTypeInfo().GetDeclaredProperty(item)?.GetValue(value);
        }

        /// <summary>
        /// Add AutoInstrumentation mark on the Segment 
        /// </summary>
        public static void AddAutoInstrumentationMark()
        {

            try
            {
                var segment = AWSXRayRecorder.Instance.GetEntity() as Segment;

                if (segment == null)
                {
                    _logger.DebugFormat("Unable to retrieve Segment from trace context");
                    return;
                }

                IDictionary<string, object> awsAttribute = segment.Aws;

                if (awsAttribute == null)
                {
                    _logger.DebugFormat("Unable to retrieve AWS dictionary to set the auto instrumentation flag.");
                }
                else
                {
                    ConcurrentDictionary<string, string> xrayAttribute = (ConcurrentDictionary<string, string>)awsAttribute["xray"];

                    if (xrayAttribute == null)
                    {
                        _logger.DebugFormat("Unable to retrieve X-Ray dictionary from AWS dictionary of segment.");
                    }
                    else
                    {
                        // Set attribute "auto_instrumentation":true in the "xray" section of the segment
                        xrayAttribute["auto_instrumentation"] = true;
                    }
                }
            }
            catch (EntityNotAvailableException e)
            {
                AWSXRayRecorder.Instance.TraceContext.HandleEntityMissing(AWSXRayRecorder.Instance, e, "Failed to get entity since it is not available in trace context while processing ASPNET Core request.");
            }
        }

        /// <summary>
        /// Mark Segment/Subsegment from http status code
        /// </summary>
        /// <param name="statusCode"></param>
        public static void MarkEntityFromStatus(int statusCode)
        {
            if (statusCode >= 400 && statusCode <= 499)
            {
                AWSXRayRecorder.Instance.MarkError();

                if (statusCode == 429)
                {
                    AWSXRayRecorder.Instance.MarkThrottle();
                }
            }
            else if (statusCode >= 500 && statusCode <= 599)
            {
                AWSXRayRecorder.Instance.MarkFault();
            }
        }
    }
}
