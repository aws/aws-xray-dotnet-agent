//-----------------------------------------------------------------------------
// <copyright file="AspNetCoreTracingHandlers.cs" company="Amazon.com">
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

#if !NET45
using System.Collections.Generic;
using System.Diagnostics;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Initialize the XRay tracing configurations and listeners for AspNet Core application
    /// </summary>
    public class AspNetCoreTracingHandlers
    {
        /// <summary>
        /// 1.Instrument configurations from appsettings.json
        /// 2.Subscribe AspNetCoreDiagnosticListener, HttpOutDiagnosticListener, SqlDiagnosticListener(SDS and MDS),
        /// EntityFrameworkCoreDiagnosticListener for tacing AspNetCore incoming request, Http outgpingrequest, and Sql request.
        /// 3.Register XRay for AWS services
        /// </summary>
        internal static void Initialize()
        {
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();

            var serviceName = xrayAutoInstrumentationOptions.ServiceName;

            var subscriptions = new List<DiagnosticListenerBase>()
            { 
                // Subscribe diagnostic listener for tracing Asp.Net Core request
                new AspNetCoreDiagnosticListener(serviceName),
                
                // Subscribe diagnostic listener for tracing Http outgoing request
                xrayAutoInstrumentationOptions.TraceHttpRequests ? new HttpOutDiagnosticListenerNetstandard() : null,
                
                // Subscribe diagnostic listener for tracing Sql request
                xrayAutoInstrumentationOptions.TraceSqlRequests ? new SqlDiagnosticListener() : null,
                
                // Subscribe diagnostic listener for tracing EF Core request
                xrayAutoInstrumentationOptions.TraceEFRequests ? new EntityFrameworkCoreDiagnosticListener() : null
            };

            DiagnosticListener.AllListeners.Subscribe(new DiagnosticListenerObserver(subscriptions));
            
            // Enable tracing for AWS request
            if (xrayAutoInstrumentationOptions.TraceAWSRequests)
            {
                AWSSDKRequestRegister.Register();
            }
        }
    }
}
#endif