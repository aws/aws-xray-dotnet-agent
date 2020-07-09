//-----------------------------------------------------------------------------
// <copyright file="HttpOutDiagnosticListenerNetstandard.cs" company="Amazon.com">
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

#if !NET45
using Amazon.Runtime.Internal.Util;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Amazon.XRay.Recorder.AutoInstrumentation.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Diagnostic listener for processing Http outgoing request
    /// </summary>
    public class HttpOutDiagnosticListenerNetstandard : DiagnosticListenerBase
    {
        private static readonly Logger _logger = Logger.GetLogger(typeof(HttpOutDiagnosticListenerNetstandard));

        internal override string Name => "HttpHandlerDiagnosticListener";

        private static readonly ConcurrentDictionary<HttpRequestMessage, Subsegment> CurrentHttpRequestMessages = new ConcurrentDictionary<HttpRequestMessage, Subsegment>();

        protected override void OnEvent(KeyValuePair<string, object> value)
        {
            try
            {
                switch (value.Key)
                {
                    case "System.Net.Http.HttpRequestOut.Start":
                        {
                            OnEventStart(value.Value);
                        }
                        break;
                    case "System.Net.Http.HttpRequestOut.Stop":
                        {
                            OnEventStop(value.Value);
                        }
                        break;
                    case "System.Net.Http.Exception":
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

        private void OnEventStart(object value)
        {
            // The value passed in is not castable, use fetch from reflection instead.
            var request = AgentUtil.FetchPropertyUsingReflection(value, "Request");
            if (request is HttpRequestMessage httpRequestMessage)
            {
                // Skip AWS SDK Request since it is instrumented using the SDK
                if (HttpRequestUtil.IsTraceable(httpRequestMessage) && CurrentHttpRequestMessages.TryAdd(httpRequestMessage, null))
                {
                    HttpRequestUtil.ProcessRequest(httpRequestMessage);
                }
            }
        }

        private void OnEventStop(object value)
        {
            // The value passed in is not castable, use fetch from reflection instead.
            var request = AgentUtil.FetchPropertyUsingReflection(value, "Request");
            var response = AgentUtil.FetchPropertyUsingReflection(value, "Response");
            if (request is HttpRequestMessage httpRequestMessage && response is HttpResponseMessage httpResponseMessage)
            {
                if (CurrentHttpRequestMessages.TryRemove(httpRequestMessage, out _))
                {
                    HttpRequestUtil.ProcessResponse(httpResponseMessage);
                    // End subsegment here
                    HttpRequestUtil.EndSubsegment();
                }
            }
        }

        private void OnEventException(object value)
        {
            // The value passed in is not castable, use fetch from reflection instead.
            var request = AgentUtil.FetchPropertyUsingReflection(value, "Request");
            var exc = AgentUtil.FetchPropertyUsingReflection(value, "Exception");
            if (request is HttpRequestMessage httpRequestMessage && exc is Exception exception)
            {
                if (CurrentHttpRequestMessages.TryRemove(httpRequestMessage, out _))
                {
                    HttpRequestUtil.ProcessException(exception);
                }
            }
        }
    }
}
#endif
