//-----------------------------------------------------------------------------
// <copyright file="HttpOutDiagnosticListenerNetframework.cs" company="Amazon.com">
//      Copyright 2016 Amazon.com, Inc. or its affiliates. All Rights Reserved.
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
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Diagnostic listener for processing Http outgoing request
    /// </summary>
    public class HttpOutDiagnosticListenerNetframework : DiagnosticListenerBase
    {
        private readonly Logger _logger = Logger.GetLogger(typeof(HttpOutDiagnosticListenerNetframework));

        private readonly ConcurrentDictionary<HttpWebRequest, Subsegment> CurrentTraceEntity = new ConcurrentDictionary<HttpWebRequest, Subsegment>();

        internal override string Name => "System.Net.Http.Desktop";

        protected override void OnEvent(KeyValuePair<string, object> value)
        {
            try
            {
                switch (value.Key)
                {
                    case "System.Net.Http.Desktop.HttpRequestOut.Start":
                        {
                            OnEventStart(value.Value);
                        }
                        break;
                    case "System.Net.Http.Desktop.HttpRequestOut.Stop":
                        {
                            OnEventStop(value.Value);
                        }
                        break;
                    case "System.Net.Http.Desktop.HttpRequestOut.Ex.Stop":
                        {
                            OnEventException(value.Value);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Invalid diagnostic source key ({0})", value.Key);
            }
        }

        /// <summary>
        /// Process http outgoing request
        /// </summary>
        private void OnEventStart(object value)
        {
            var request = Fetch(value, "Request");
            if (request is HttpWebRequest webRequest)
            {
                // Skip AWS Request
                if (HttpRequestUtil.IsTraceable(webRequest))
                {
                    HttpRequestUtil.ProcessRequest(webRequest);

                    try
                    {
                        var currentSubsegment = AWSXRayRecorder.Instance.GetEntity() as Subsegment;
                        if (currentSubsegment != null)
                        {
                            CurrentTraceEntity.TryAdd(webRequest, currentSubsegment);
                        }
                    }
                    catch (EntityNotAvailableException e)
                    {
                        AWSXRayRecorder.Instance.TraceContext.HandleEntityMissing(AWSXRayRecorder.Instance, e, "Subsegment is not available in trace context.");
                    }
                    catch (InvalidCastException e)
                    {
                        _logger.Error(new EntityNotAvailableException("Failed to cast the entity to Subsegment.", e), "Failed to  get the Subsegment from trace context.");
                    }
                }
            }
        }

        /// <summary>
        /// Process http response
        /// </summary>
        private void OnEventStop(object value)
        {
            var request = Fetch(value, "Request");
            var response = Fetch(value, "Response");
            if (request is HttpWebRequest webRequest && response is HttpWebResponse webResponse)
            {
                if (CurrentTraceEntity.TryRemove(webRequest, out var currentSubsegment))
                {
                    if (webResponse != null)
                    {
                        HttpRequestUtil.ProcessResponse(webResponse.StatusCode, webResponse.ContentLength, currentSubsegment);
                    }
                    HttpRequestUtil.EndSubsegment(currentSubsegment);
                }
            }
        }

        /// <summary>
        /// Process exception
        /// </summary>
        private void OnEventException(object value)
        {
            var request = Fetch(value, "Request");
            var status = Fetch(value, "StatusCode");
            if (request is HttpWebRequest webRequest && status is HttpStatusCode httpStatusCode)
            {
                if (CurrentTraceEntity.TryRemove(webRequest, out var currentSubsegment))
                {
                    var statusCode = (int)httpStatusCode;
                    if (statusCode >= 300)
                    {
                        var exceptionMessage = new Exception("Outgoing Http Exception, status code: " + statusCode);
                        currentSubsegment.AddException(exceptionMessage);
                    }
                    HttpRequestUtil.EndSubsegment(currentSubsegment);
                }
            }
        }

        /// <summary>
        /// Fetch value
        /// </summary>
        private object Fetch(object value, string item)
        {
            return value.GetType().GetTypeInfo().GetDeclaredProperty(item)?.GetValue(value);
        }
    }
}
#endif