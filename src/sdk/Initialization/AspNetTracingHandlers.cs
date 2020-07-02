//-----------------------------------------------------------------------------
// <copyright file="AspNetTracingHandlers.cs" company="Amazon.com">
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
using System.Collections.Generic;
using System.Data.Entity.Infrastructure.Interception;
using System.Diagnostics;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Initialize the XRay tracing handlers for Asp.Net application
    /// </summary>
    public class AspNetTracingHandlers
    {
        /// <summary>
        /// 1.Add HttpOutDiagnosticListenerNetframework, EntityFramework and Sql tracing handler for tracing Http outgping request and Sql query.
        /// 2.Register XRay for AWS services
        /// </summary>
        internal static void Initialize(XRayAutoInstrumentationOptions options)
        {
            if (options.TraceHttpRequests)
            {
                var subscription = new List<DiagnosticListenerBase>()
                {
                    new HttpOutDiagnosticListenerNetframework()
                };

                DiagnosticListener.AllListeners.Subscribe(new DiagnosticListenerObserver(subscription));
            }

            if (options.TraceSqlRequests)
            {
                var sqlListener = new SqlEventListener();
            }
            
            if (options.TraceEFRequests)
            {
                DbInterception.Add(new EntityFrameworkHandler());
            }

            if (options.TraceAWSRequests)
            {
                AWSSDKRequestRegister.Register();
            }
        }
    }
}
#endif