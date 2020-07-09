//-----------------------------------------------------------------------------
// <copyright file="AspNetCoreRequestUtil.cs" company="Amazon.com">
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
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Amazon.XRay.Recorder.Core.Sampling;
using Amazon.XRay.Recorder.Core.Strategies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides.Internal;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Amazon.XRay.Recorder.AutoInstrumentation.Utils
{
    /// <summary>
    /// This class provides methods to set up segment naming strategy, process Asp.Net Core incoming
    /// request, response and exception.
    /// </summary>
    public class AspNetCoreRequestUtil
    {
        private static AWSXRayRecorder _recorder;
        private static readonly Logger _logger = Logger.GetLogger(typeof(AspNetCoreRequestUtil));
        private static readonly string SchemeDelimiter = "://";
        private static readonly string X_FORWARDED_FOR = "X-Forwarded-For";

        private static SegmentNamingStrategy SegmentNamingStrategy { get; set; }

        /// <summary>
        /// Set up segment naming strategy
        /// </summary>
        internal static void SetSegmentNamingStrategy(SegmentNamingStrategy segmentNamingStrategy)
        {
            SegmentNamingStrategy = segmentNamingStrategy ?? throw new ArgumentNullException("segmentNamingStrategy");
        }

        internal static void SetAWSXRayRecorder(AWSXRayRecorder recorder)
        {
            _recorder = recorder ?? throw new ArgumentNullException("recorder");
        }

        /// <summary>
        /// Process http request. 
        /// </summary>
        internal static void ProcessRequest(HttpContext httpContext)
        {
            HttpRequest request = httpContext.Request;
            string headerString = null;

            if (request.Headers.TryGetValue(TraceHeader.HeaderKey, out StringValues headerValue))
            {
                if (headerValue.Count >= 1)
                    headerString = headerValue[0];
            }

            if (!TraceHeader.TryParse(headerString, out TraceHeader traceHeader))
            {
                _logger.DebugFormat("Trace header doesn't exist or not valid : ({0}). Injecting a new one.", headerString);
                traceHeader = new TraceHeader
                {
                    RootTraceId = TraceId.NewId(),
                    ParentId = null,
                    Sampled = SampleDecision.Unknown
                };
            }

            var segmentName = SegmentNamingStrategy.GetSegmentName(request);
            bool isSampleDecisionRequested = traceHeader.Sampled == SampleDecision.Requested;

            string ruleName = null;
            // Make sample decision
            if (traceHeader.Sampled == SampleDecision.Unknown || traceHeader.Sampled == SampleDecision.Requested)
            {
                string host = request.Host.Host;
                string url = request.Path;
                string method = request.Method;
                SamplingInput samplingInput = new SamplingInput(host, url, method, segmentName, _recorder.Origin);
                SamplingResponse sampleResponse = _recorder.SamplingStrategy.ShouldTrace(samplingInput);
                traceHeader.Sampled = sampleResponse.SampleDecision;
                ruleName = sampleResponse.RuleName;
            }

            if (AWSXRayRecorder.IsLambda())
            {
                _recorder.BeginSubsegment(segmentName);
            }
            else
            {
                SamplingResponse samplingResponse = new SamplingResponse(ruleName, traceHeader.Sampled); // get final ruleName and SampleDecision
                _recorder.BeginSegment(SegmentNamingStrategy.GetSegmentName(request), traceHeader.RootTraceId, traceHeader.ParentId, samplingResponse);
            }

            if (!AWSXRayRecorder.Instance.IsTracingDisabled())
            {
                var requestAttributes = PopulateRequestAttributes(request);
                _recorder.AddHttpInformation("request", requestAttributes);
            }

            // Mark the segment as auto-instrumented
            AgentUtil.AddAutoInstrumentationMark();

            if (isSampleDecisionRequested)
            {
                httpContext.Response.Headers.Add(TraceHeader.HeaderKey, traceHeader.ToString()); // Its recommended not to modify response header after _next.Invoke() call
            }
        }

        private static Dictionary<string, object> PopulateRequestAttributes(HttpRequest request)
        {
            var requestAttributes = new Dictionary<string, object>();

            requestAttributes["url"] = GetUrl(request);
            requestAttributes["method"] = request.Method;
            string xForwardedFor = GetXForwardedFor(request);

            if (xForwardedFor == null)
            {
                requestAttributes["client_ip"] = GetClientIpAddress(request);
            }
            else
            {
                requestAttributes["client_ip"] = xForwardedFor;
                // If it's outer Proxy, add "X-Forwarded-For: true" in the trace context.
                if (IsOuterProxy(request))
                {
                    requestAttributes["x_forwarded_for"] = true;
                }
            }

            if (request.Headers.ContainsKey(HeaderNames.UserAgent))
            {
                requestAttributes["user_agent"] = request.Headers[HeaderNames.UserAgent].ToString();
            }

            return requestAttributes;
        }

        private static bool IsOuterProxy(HttpRequest request)
        {
            if (request.HttpContext.Request.Headers.TryGetValue(X_FORWARDED_FOR, out StringValues headerValue))
            {
                return headerValue.ToString().IndexOf(',') >= 0;
            }

            return false;
        }

        private static string GetClientIpAddress(HttpRequest request)
        {
            return request.HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        /// <summary>
        /// Get X-Forwarded-For header.
        /// </summary>
        private static string GetXForwardedFor(HttpRequest request)
        {
            String clientIp = null;

            if (request.HttpContext.Request.Headers.TryGetValue(X_FORWARDED_FOR, out StringValues headerValue))
            {
                string[] ipEndPoints = headerValue.ToString().Split(',');
                
                // parse the IP address from "IP:port number" end point
                clientIp = ExtractIpAddress(ipEndPoints[0].Trim());
            }

            return string.IsNullOrEmpty(clientIp) ? null : clientIp.Split(',')[0].Trim();
        }

        /// <summary>
        /// IP end point format: "IP:Port number". 
        /// IPV6 formats: [xx:xx:xx:xx:xx:xx:xx:xx]:port number, [xx:xx:xx:xx:xx:xx:xx:xx], xx:xx:xx:xx:xx:xx:xx:xx.
        /// IPV4 formats: x.x.x.x:port number, x.x.x.x.
        /// Extract IP address from "IP:Port number" end point format.
        /// </summary>
        private static string ExtractIpAddress(string endPoint)
        {
            IPAddress ipAddress = null;
            if (IPEndPointParser.TryParse(endPoint, out IPEndPoint ipEndPoint))
            {
                ipAddress = ipEndPoint.Address;
            }

            return ipAddress?.ToString();
        }

        private static string GetUrl(HttpRequest request)
        {
            if (request == null)
            {
                _logger.DebugFormat("HTTPRequest instance is null. Cannot get URL from the request, Setting url to null");
                return null;
            }
            var scheme = request.Scheme ?? string.Empty;
            var host = request.Host.Value ?? string.Empty;
            var pathBase = request.PathBase.Value ?? string.Empty;
            var path = request.Path.Value ?? string.Empty;
            var queryString = request.QueryString.Value ?? string.Empty;

            // PERF: Calculate string length to allocate correct buffer size for StringBuilder.
            var length = scheme.Length + SchemeDelimiter.Length + host.Length
                + pathBase.Length + path.Length + queryString.Length;

            return new StringBuilder(length)
                .Append(scheme)
                .Append(SchemeDelimiter)
                .Append(host)
                .Append(pathBase)
                .Append(path)
                .Append(queryString)
                .ToString();
        }

        /// <summary>
        /// Process http response.
        /// </summary>
        internal static void ProcessResponse(HttpContext httpContext)
        {
            HttpResponse response = httpContext.Response;

            if (!AWSXRayRecorder.Instance.IsTracingDisabled())
            {
                var responseAttributes = PopulateResponseAttributes(response);
                _recorder.AddHttpInformation("response", responseAttributes);
            }

            if (AWSXRayRecorder.IsLambda())
            {
                _recorder.EndSubsegment();
            }
            else
            {
                _recorder.EndSegment();
            }
        }

        private static Dictionary<string, object> PopulateResponseAttributes(HttpResponse response)
        {
            var responseAttributes = new Dictionary<string, object>();

            int statusCode = (int)response.StatusCode;

            AgentUtil.MarkEntityFromStatus(statusCode);

            responseAttributes["status"] = statusCode;

            if (response.Headers.ContentLength != null)
            {
                responseAttributes["content_length"] = response.Headers.ContentLength;
            }

            return responseAttributes;
        }

        internal static void ProcessException(Exception exception)
        {
            _recorder.AddException(exception);
        }
    }
}
#endif
