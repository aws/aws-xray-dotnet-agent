//-----------------------------------------------------------------------------
// <copyright file="AspNetAutoInstrumentationModule.cs" company="Amazon.com">
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
using Amazon.XRay.Recorder.AutoInstrumentation.Utils;
using System.Collections.Concurrent;
using System.Web;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// This class aims to intercept Http incoming request for ASP.NET Framework.
    /// </summary>
    public class AspNetAutoInstrumentationModule : IHttpModule
    {
        private HttpApplication currentHttpApplication;
        
        private static readonly ConcurrentDictionary<HttpApplication, byte> ActiveHttpApplications = new ConcurrentDictionary<HttpApplication, byte>();

        static AspNetAutoInstrumentationModule()
        {
            AspNetRequestUtil.InitializeAspNet();
        }

        public void Init(HttpApplication httpApplication)
        {
            if (ActiveHttpApplications.TryAdd(httpApplication, 0))
            {
                currentHttpApplication = httpApplication;
                currentHttpApplication.BeginRequest += AspNetRequestUtil.ProcessHTTPRequest;
                currentHttpApplication.EndRequest += AspNetRequestUtil.ProcessHTTPResponse;
                currentHttpApplication.Error += AspNetRequestUtil.ProcessHTTPError;
            }
        }

        public void Dispose()
        {
            if (currentHttpApplication != null)
            {
                ActiveHttpApplications.TryRemove(currentHttpApplication, out _);
                currentHttpApplication = null;
            }
        }
    }
}
#endif
