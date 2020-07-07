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
        
        private static readonly ConcurrentDictionary<HttpApplication, byte> CurrentHttpModules = new ConcurrentDictionary<HttpApplication, byte>();

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

            // DotNet agent installer installs the required dependencies in user's "C:\ProgramFiles\AWSXRayAgent\Net45" folder (64bit) or "C:\ProgramFiles(x86)\AWSXRayAgent\Net45" (32bit)
            var agentFolderRootPath = GetAgentFolderPath();

            var agentFolderPath = Path.Combine(agentFolderRootPath, "AWSXRayAgent\\Net45"); 

            var path = Path.Combine(agentFolderPath, $"{assemblyName}.dll");

            // Can allow multiple applications to read and load the same assembly in the same folder
            byte[] assemblyBytes = LoadBytesFromAssembly(path);

            var assembly = Assembly.Load(assemblyBytes);

            return assembly;
        }

        /// <summary>
        /// Get the path to the AWS Agent folder
        /// </summary>
        private static string GetAgentFolderPath()
        {
            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                return Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%");
            }

            return Environment.ExpandEnvironmentVariables("%ProgramFiles%");
        }

        private static byte[] LoadBytesFromAssembly(string path)
        {
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
            if (CurrentHttpModules.TryAdd(httpApplication, 0))
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
                CurrentHttpModules.TryRemove(currentHttpApplication, out _);
                currentHttpApplication = null;
            }
        }
    }
}
#endif