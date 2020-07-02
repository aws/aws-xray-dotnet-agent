//-----------------------------------------------------------------------------
// <copyright file="AspNetAutoInstrumentationModule.cs" company="Amazon.com">
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
using Amazon.XRay.Recorder.AutoInstrumentation.Utils;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Web;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// This class aims to intercept Http incoming request for ASP.NET Framework.
    /// </summary>
    public class AspNetAutoInstrumentationModule : IHttpModule
    {
        private HttpApplication currentHttpApplication;
        
        private readonly ConcurrentDictionary<HttpApplication, HttpApplication> CurrentTraceEntity = new ConcurrentDictionary<HttpApplication, HttpApplication>();

        static AspNetAutoInstrumentationModule()
        {
            // Load the satellite dependencies of AWSXRayRecorder.AutoInstrumentation.dll at runtime
            // This can avoid duplicate introducing dependency when the same dependency's already in user's application
            AppDomain.CurrentDomain.AssemblyResolve += AWSXRayAutoInstrumentationDependency;
            AspNetRequestUtil.InitializeAspNet();
        }

        private static Assembly AWSXRayAutoInstrumentationDependency(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name).Name;

            // DotNet agent installer installs the required dependencies in user's "C:\ProgramFiles\AWSXRayAgent\Net45" folder (64bit)
            var agentFolderRootPath = Environment.Is64BitOperatingSystem ? Environment.ExpandEnvironmentVariables("%ProgramW6432%") : Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%");

            var agentFolderPath = agentFolderRootPath + "\\AWSXRayAgent\\Net45"; 

            var path = Path.Combine(agentFolderPath, $"{assemblyName}.dll");

            // Can allow multiple applications to read and load the same assembly in the same folder
            byte[] assemblyBytes = LoadBytesFromAssembly(path);

            var assembly = Assembly.Load(assemblyBytes);

            return assembly;
        }

        private static byte[] LoadBytesFromAssembly(string path, FileAccess fileAccess = FileAccess.Read, FileShare shareMode = FileShare.ReadWrite)
        {
            using (var fileStream = new FileStream(path, FileMode.Open, fileAccess, shareMode))
            {
                using (var memoryStream = new MemoryStream())
                {
                    fileStream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
        }

        public void Init(HttpApplication httpApplication)
        {
            if (CurrentTraceEntity.TryAdd(httpApplication, null))
            {
                currentHttpApplication = httpApplication;
                httpApplication.BeginRequest += AspNetRequestUtil.ProcessHTTPRequest;
                httpApplication.EndRequest += AspNetRequestUtil.ProcessHTTPResponse;
                httpApplication.Error += AspNetRequestUtil.ProcessHTTPError;
            }
        }

        public void Dispose()
        {
            if (currentHttpApplication != null)
            {
                CurrentTraceEntity.TryRemove(currentHttpApplication, out _);
                currentHttpApplication = null;
            }
        }
    }
}
#endif