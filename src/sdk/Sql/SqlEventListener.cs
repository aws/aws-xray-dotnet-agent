//-----------------------------------------------------------------------------
// <copyright file="SqlEventListener.cs" company="Amazon.com">
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

#if NET45
using Amazon.Runtime.Internal.Util;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Exceptions;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Amazon.XRay.Recorder.AutoInstrumentation.Utils;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Globalization;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Sql event listener for tracing Sql query from System.Data.SqlClient
    /// </summary>
    public class SqlEventListener : EventListener
    {
        private const string SqlEventSourceName = "Microsoft-AdoNet-SystemData";
        private const string EventSourceTypeName = "System.Data.SqlEventSource";
        private const int SqlCommandExecutedBeforeId = 1;
        private const int SqlCommandExecutedAfterId = 2;

        private static readonly AWSXRayRecorder _recorder = AWSXRayRecorder.Instance;
        private static readonly Logger _logger = Logger.GetLogger(typeof(SqlEventListener));

        private static readonly ConcurrentDictionary<int, Subsegment> CurrentSqlEvents = new ConcurrentDictionary<int, Subsegment>();

        /// <summary>
        /// Enable receiving events
        /// </summary>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource != null && eventSource.Name == SqlEventSourceName && eventSource.GetType().FullName == EventSourceTypeName)
            {
                EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)1);
            }
            base.OnEventSourceCreated(eventSource);
        }

        /// <summary>
        /// Receive events
        /// </summary>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData?.Payload == null)
            {
                return;
            }

            try
            {
                switch (eventData.EventId)
                {
                    case SqlCommandExecutedBeforeId:
                        {
                            OnEventStart(eventData);
                        }
                        break;
                    case SqlCommandExecutedAfterId:
                        {
                            OnEventStop(eventData);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Invalid Event Id ({0})", eventData.EventId);
            }
        }

        /// <summary>
        /// Trace before executing Sql command
        /// </summary>
        private void OnEventStart(EventWrittenEventArgs sqlEventData)
        {
            if (sqlEventData.Payload.Count != 4)
            {
                return;
            }

            // Skip EF request
            if (SqlRequestUtil.IsTraceable())
            {
                SqlRequestUtil.ProcessEventData(sqlEventData);

                try
                {
                    var currentSubsegment = _recorder.GetEntity() as Subsegment;
                    int id = Convert.ToInt32(sqlEventData.Payload[0], CultureInfo.InvariantCulture);
                    if (currentSubsegment != null)
                    {
                        CurrentSqlEvents.TryAdd(id, currentSubsegment);
                    }
                }
                catch (EntityNotAvailableException e)
                {
                    AWSXRayRecorder.Instance.TraceContext.HandleEntityMissing(AWSXRayRecorder.Instance, e, "Subsegment is not available in trace context.");
                }
            }
        }

        /// <summary>
        /// Trace after executing Sql command
        /// </summary>
        private void OnEventStop(EventWrittenEventArgs sqlEventData)
        {
            if (sqlEventData.Payload.Count != 3)
            {
                return;
            }

            int id = Convert.ToInt32(sqlEventData.Payload[0], CultureInfo.InvariantCulture);
            int state = Convert.ToInt32(sqlEventData.Payload[1], CultureInfo.InvariantCulture);
            int exceptionNumber = Convert.ToInt32(sqlEventData.Payload[2], CultureInfo.InvariantCulture);

            if (CurrentSqlEvents.TryRemove(id, out var currentSubsegment))
            {
                if ((state & 2) == 2)
                {
                    currentSubsegment.HasFault = true;
                }
                SqlRequestUtil.EndSubsegment(currentSubsegment);
            } 
        }
    }
}
#endif
