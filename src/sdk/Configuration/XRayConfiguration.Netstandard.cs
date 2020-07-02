//-----------------------------------------------------------------------------
// <copyright file="XRayConfiguration.Netstandard.cs" company="Amazon.com">
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
using Amazon.Runtime.Internal.Util;
using Amazon.XRay.Recorder.Core;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Class for register X-Ray .Net SDK and Auto-Instrumentation SDK configurations 
    /// </summary>
    public static class XRayConfiguration
    {
        private const string ServiceNameKey = "ServiceName";
        private const string DaemonAddressKey = "DaemonAddress";
        private const string TraceHttpRequestsKey = "TraceHttpRequests";
        private const string TraceAWSRequestsKey = "TraceAWSRequests";
        private const string TraceSqlRequestsKey = "TraceSqlRequests";
        private const string TraceEFRequestsKey = "TraceEFRequests";

        private static readonly Logger _logger = Logger.GetLogger(typeof(XRayConfiguration));

        /// <summary>
        /// Instrument tracing configurations, such as pluggins, sampling rules, service name, etc., from AspNet Core application.
        /// </summary>
        public static XRayAutoInstrumentationOptions Register()
        {
            IConfiguration configuration = null;
            try
            {
                // Get the json file
                configuration = new ConfigurationBuilder()
                                        .SetBasePath(Directory.GetCurrentDirectory())
                                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                                        .Build();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Can't fetch configuration from appsettings.json file.");
            }

            // Initialize a new instance of the AWSXRayRecorder with given instance of IConfiguration.
            // If configuration is null, default value will be set.
            AWSXRayRecorder.InitializeInstance(configuration);

            var xrayAutoInstrumentationOptions = GetXRayAutoInstrumentationOptions(configuration);

            // Set daemon address 
            AWSXRayRecorder.Instance.SetDaemonAddress(xrayAutoInstrumentationOptions.DaemonAddress);

            return xrayAutoInstrumentationOptions;
        }

        /// <summary>
        /// Initialize Auto-Instrumentation configuration items from instance <see cref="IConfiguration"/>.
        /// </summary>
        public static XRayAutoInstrumentationOptions GetXRayAutoInstrumentationOptions(IConfiguration configuration)
        {
            var xrayAutoInstrumentationOptions = new XRayAutoInstrumentationOptions();

            IConfiguration xraySection = configuration?.GetSection("XRay");

            if (xraySection == null)
            {
                return xrayAutoInstrumentationOptions;
            }

            // Get Auto-Instrumentation related configuration items from appsetting.json file
            xrayAutoInstrumentationOptions.ServiceName = GetSettingServiceName(ServiceNameKey, xraySection);
            xrayAutoInstrumentationOptions.DaemonAddress = GetSettingDaemonAddress(DaemonAddressKey, xraySection);
            xrayAutoInstrumentationOptions.TraceHttpRequests = GetSettingBool(TraceHttpRequestsKey, xraySection);
            xrayAutoInstrumentationOptions.TraceAWSRequests = GetSettingBool(TraceAWSRequestsKey, xraySection);
            xrayAutoInstrumentationOptions.TraceSqlRequests = GetSettingBool(TraceSqlRequestsKey, xraySection);
            xrayAutoInstrumentationOptions.TraceEFRequests = GetSettingBool(TraceEFRequestsKey, xraySection);

            return xrayAutoInstrumentationOptions;
        }

        private static string GetSettingServiceName(string key, IConfiguration section, string defaultValue = "DefaultService")
        {
            if (!string.IsNullOrEmpty(section[key]))
            {
                return section[key];
            }
            
            return defaultValue;
        }

        private static string GetSettingDaemonAddress(string key, IConfiguration section, string defaultValue = "127.0.0.1:2000")
        {
            if (!string.IsNullOrEmpty(section[key]))
            {
                return section[key];
            }
                
            return defaultValue;
        }

        private static bool GetSettingBool(string key, IConfiguration section, bool defaultValue = true)
        {
            string value = section[key];
            if (bool.TryParse(value, out bool result))
            {
                return result;
            }

            return defaultValue;
        }
    }
}
#endif