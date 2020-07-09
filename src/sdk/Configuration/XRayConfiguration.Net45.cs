//-----------------------------------------------------------------------------
// <copyright file="XRayConfiguration.Net45.cs" company="Amazon.com">
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
using System.Configuration;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Class for register Auto-Instrumentation SDK configurations for Asp.Net application
    /// </summary>
    public class XRayConfiguration
    {
        public static XRayAutoInstrumentationOptions Register()
        {
            var serviceName = GetSettingServiceName("ServiceName");
            var daemonAddress = GetSettingDaemonAddress("DaemonAddress");
            var traceHttpRequests = GetSettingBool("TraceHttpRequests");
            var traceAWSRequests = GetSettingBool("TraceAWSRequests");
            var traceSqlRequests = GetSettingBool("TraceSqlRequests");
            var traceEFRequests = GetSettingBool("TraceEFRequests");

            var xrayAutoInstrumentationOptions = new XRayAutoInstrumentationOptions(serviceName, daemonAddress, traceHttpRequests, traceAWSRequests, traceSqlRequests, traceEFRequests);

            return xrayAutoInstrumentationOptions;
        }

        private static string GetSetting(string key)
        {
            var appSettings = ConfigurationManager.AppSettings;
            if (appSettings == null)
            {
                return null;
            }

            string value = appSettings[key];
            return value;
        }

        private static bool GetSettingBool(string key, bool defaultValue = true)
        {
            string value = GetSetting(key);
            if (bool.TryParse(value, out bool result))
            {
                return result;
            }

            return defaultValue;
        }

        private static string GetSettingDaemonAddress(string key, string defaultValue = "127.0.0.1:2000")
        {
            string value = GetSetting(key);
            if (value == null)
            {
                return defaultValue;
            }

            return value;
        }

        private static string GetSettingServiceName(string key, string defaultValue = "DefaultService")
        {
            string value = GetSetting(key);
            if (value == null)
            {
                return defaultValue;
            }

            return value;
        }
    }
}
#endif
