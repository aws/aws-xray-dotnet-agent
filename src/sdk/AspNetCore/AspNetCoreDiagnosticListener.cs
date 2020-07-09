//-----------------------------------------------------------------------------
// <copyright file="AspNetCoreDiagnosticListener.cs" company="Amazon.com">
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
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Strategies;
using Amazon.XRay.Recorder.AutoInstrumentation.Utils;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Diagnostic listener for processing Asp.Net Core incoming request
    /// </summary>
    public class AspNetCoreDiagnosticListener : DiagnosticListenerBase
    {
        private static readonly Logger _logger = Logger.GetLogger(typeof(AspNetCoreDiagnosticListener));
        
        internal override string Name => "Microsoft.AspNetCore";

        internal AspNetCoreDiagnosticListener(string serviceName)
        {
            AspNetCoreRequestUtil.SetAWSXRayRecorder(AWSXRayRecorder.Instance);
            AspNetCoreRequestUtil.SetSegmentNamingStrategy(new FixedSegmentNamingStrategy(serviceName));
        }

        protected override void OnEvent(KeyValuePair<string, object> value)
        {
            try
            {
                switch (value.Key)
                {
                    case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start":
                        {
                            OnEventStart(value.Value);
                        }
                        break;
                    case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop":
                        {
                            OnEventStop(value.Value);
                        }
                        break;
                    case "Microsoft.AspNetCore.Diagnostics.UnhandledException":
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
            var context = AgentUtil.FetchPropertyFromReflection(value, "HttpContext");
            if (context is HttpContext httpContext)
            {
                AspNetCoreRequestUtil.ProcessRequest(httpContext);
            }
        }

        private void OnEventStop(object value)
        {
            var context = AgentUtil.FetchPropertyFromReflection(value, "HttpContext");
            if (context is HttpContext httpContext)
            {
                AspNetCoreRequestUtil.ProcessResponse(httpContext);
            }
        }

        private void OnEventException(object value)
        {
            // The value passed in is not castable, use fetch from reflection.
            var exc = AgentUtil.FetchPropertyFromReflection(value, "Exception"); 
            if (exc is Exception exception)
            {
                AspNetCoreRequestUtil.ProcessException(exception);
            }
        }
    }
}
#endif
