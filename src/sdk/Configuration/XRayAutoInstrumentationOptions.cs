//-----------------------------------------------------------------------------
// <copyright file="XRayAutoInstrumentationOptions.cs" company="Amazon.com">
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

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// This is a class for storing Auto-Instrumentation related configurations from IConfiguration instance.
    /// For other X-Ray .Net SDK configurations, take reference to https://github.com/aws/aws-xray-sdk-dotnet/tree/master#configuration
    /// </summary>
    public class XRayAutoInstrumentationOptions
    {
        /// <summary>
        /// Service name of instrumented Asp.Net or Asp.Net Core application
        /// </summary>
        public string ServiceName { get; set; } = "DefaultService";

        /// <summary>
        /// Daemon address
        /// </summary>
        public string DaemonAddress { get; set; } = "127.0.0.1:2000";

        /// <summary>
        /// Enable tracing Http request
        /// </summary>
        public bool TraceHttpRequests { get; set; } = true;

        /// <summary>
        /// Enable tracing AWS request
        /// </summary>
        public bool TraceAWSRequests { get; set; } = true;

        /// <summary>
        /// Enable tracing Sql request
        /// </summary>
        public bool TraceSqlRequests { get; set; } = true;

        /// <summary>
        /// Enable tracing Entity Framework request
        /// </summary>
        public bool TraceEFRequests { get; set; } = true;

        /// <summary>
        /// Default constructor
        /// </summary>
        public XRayAutoInstrumentationOptions()
        {
        }

        public XRayAutoInstrumentationOptions(string serviceName, string daemonAddress, bool traceHttpRequests, bool traceAWSRequests, bool traceSqlRequests, bool traceEFRequests)
        {
            ServiceName = serviceName;
            DaemonAddress = daemonAddress;
            TraceHttpRequests = traceHttpRequests;
            TraceAWSRequests = traceAWSRequests;
            TraceSqlRequests = traceSqlRequests;
            TraceEFRequests = traceEFRequests;
        }
    }
}