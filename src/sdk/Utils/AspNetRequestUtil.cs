//-----------------------------------------------------------------------------
// <copyright file="AspNetRequestUtil.cs" company="Amazon.com">
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
using Amazon.Runtime.Internal.Util;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Exceptions;
using Amazon.XRay.Recorder.Core.Internal.Context;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Amazon.XRay.Recorder.Core.Sampling;
using Amazon.XRay.Recorder.Core.Strategies;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Web;

namespace Amazon.XRay.Recorder.AutoInstrumentation.Utils
{
    /// <summary>
    /// This class provides methods to set up segment naming strategy, process Asp.Net incoming
    /// request, response and exception.
    /// </summary>
    public class AspNetRequestUtil
    {
        private static AWSXRayRecorder _recorder;
        private static SegmentNamingStrategy segmentNamingStrategy;
        private static readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();
        private static readonly Logger _logger = Logger.GetLogger(typeof(AspNetRequestUtil));

        /// <summary>
        /// Initialize AWSXRayRecorder instance, register configurations and tracing handlers
        /// </summary>
        internal static void InitializeAspNet()
        {
            if (!AWSXRayRecorder.IsCustomRecorder) // If custom recorder is not set
            {
                AWSXRayRecorder.Instance.SetTraceContext(new HybridContextContainer()); // configure Trace Context
            }

            _recorder = AWSXRayRecorder.Instance;

            // Register configurations 
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();

            _recorder.SetDaemonAddress(xrayAutoInstrumentationOptions.DaemonAddress);

            if (GetSegmentNamingStrategy() == null) // ensures only one time initialization among many HTTPApplication instances
            {
                var serviceName = xrayAutoInstrumentationOptions.ServiceName;
                InitializeAspNet(new FixedSegmentNamingStrategy(serviceName));
            }

            // Initialize tracing handlers for Asp.Net platform
            AspNetTracingHandlers.Initialize(xrayAutoInstrumentationOptions);
        }

        private static SegmentNamingStrategy GetSegmentNamingStrategy()
        {
            rwLock.EnterReadLock();
            try
            {
                // It is safe for this thread to read from the shared resource.
                return segmentNamingStrategy;
            }
            finally
            {
                rwLock.ExitReadLock(); // Ensure that the lock is released.
            }
        }

        private static void InitializeAspNet(FixedSegmentNamingStrategy segmentNamingStrategy)
        {
            if (segmentNamingStrategy == null)
            {
                throw new ArgumentNullException("segmentNamingStrategy");
            }

            if (GetSegmentNamingStrategy() == null) // ensures only one time initialization among many HTTPApplication instances
            {
                SetSegmentNamingStrategy(segmentNamingStrategy);
            }
        }

        private static void SetSegmentNamingStrategy(SegmentNamingStrategy value)
        {
            rwLock.EnterWriteLock();
            try
            {
                // It is safe for this thread to write to the shared resource.
                segmentNamingStrategy = value;
            }
            finally
            {
                rwLock.ExitWriteLock(); // Ensure that the lock is released.
            }
        }

        internal static void ProcessHTTPRequest(object sender, EventArgs e)
        {
            var context = ((HttpApplication)sender).Context;

            string ruleName = null;

            var request = context.Request;
            TraceHeader traceHeader = GetTraceHeader(context);

            var segmentName = GetSegmentNamingStrategy().GetSegmentName(request);
            // Make sample decision
            if (traceHeader.Sampled == SampleDecision.Unknown || traceHeader.Sampled == SampleDecision.Requested)
            {
                SamplingResponse response = MakeSamplingDecision(request, traceHeader, segmentName);
                ruleName = response.RuleName;
            }

            var timestamp = context.Timestamp.ToUniversalTime(); // Gets initial timestamp of current HTTP Request

            SamplingResponse samplingResponse = new SamplingResponse(ruleName, traceHeader.Sampled); // get final ruleName and SampleDecision
            _recorder.BeginSegment(segmentName, traceHeader.RootTraceId, traceHeader.ParentId, samplingResponse, timestamp);

            // Mark the segment as auto-instrumented
            AddAutoInstrumentationMark();

            if (!AWSXRayRecorder.Instance.IsTracingDisabled())
            {
                Dictionary<string, object> requestAttributes = new Dictionary<string, object>();
                ProcessRequestAttributes(request, requestAttributes);
                _recorder.AddHttpInformation("request", requestAttributes);
            }
        }

        private static void AddAutoInstrumentationMark()
        {
            try
            {
                var segment = _recorder.GetEntity() as Segment;
                IDictionary<string, object> awsAttribute = segment.Aws;

                if (awsAttribute == null)
                {
                    _logger.DebugFormat("Unable to retrieve AWS dictionary to set the auto instrumentation flag.");
                }
                else
                {
                    Dictionary<string, string> xrayAttribute = (Dictionary<string, string>)awsAttribute["xray"];

                    if (xrayAttribute == null)
                    {
                        _logger.DebugFormat("Unable to retrieve X-Ray dictionary from AWS dictionary of segment.");
                    }
                    else
                    {
                        // Set attribute "auto_instrumentation":"true" in the "xray" section of the segment
                        xrayAttribute["auto_instrumentation"] = "true";
                    }
                }
            }
            catch (EntityNotAvailableException e)
            {
                _recorder.TraceContext.HandleEntityMissing(_recorder, e, "Failed to get entity since it is not available in trace context while processing ASPNET request.");
            }
            catch (InvalidCastException e)
            {
                _logger.Error(new EntityNotAvailableException("Failed to cast the entity to Segment.", e), "Failed to get the segment from trace context for adding auto-instrumentation mark.");
            }
        }

        private static void ProcessRequestAttributes(HttpRequest request, Dictionary<string, object> requestAttributes)
        {
            requestAttributes["url"] = request.Url.AbsoluteUri;
            requestAttributes["user_agent"] = request.UserAgent;
            requestAttributes["method"] = request.HttpMethod;
            string xForwardedFor = GetXForwardedFor(request);

            if (xForwardedFor == null)
            {
                requestAttributes["client_ip"] = GetClientIpAddress(request);
            }
            else
            {
                requestAttributes["client_ip"] = xForwardedFor;
                requestAttributes["x_forwarded_for"] = true;
            }
        }

        private static object GetClientIpAddress(HttpRequest request)
        {
            return request.UserHostAddress;
        }

        private static string GetXForwardedFor(HttpRequest request)
        {
            string clientIp = request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            return string.IsNullOrEmpty(clientIp) ? null : clientIp.Split(',')[0].Trim();
        }

        private static SamplingResponse MakeSamplingDecision(HttpRequest request, TraceHeader traceHeader, string segmentName)
        {
            string host = request.Headers.Get("Host");
            string url = request.Url.AbsolutePath;
            string method = request.HttpMethod;
            SamplingInput samplingInput = new SamplingInput(host, url, method, segmentName, _recorder.Origin);
            SamplingResponse sampleResponse = _recorder.SamplingStrategy.ShouldTrace(samplingInput);
            traceHeader.Sampled = sampleResponse.SampleDecision;
            return sampleResponse;
        }

        private static TraceHeader GetTraceHeader(HttpContext context)
        {
            var request = context.Request;
            string headerString = request.Headers.Get(TraceHeader.HeaderKey);

            // Trace header doesn't exist, which means this is the root node. Create a new traceId and inject the trace header.
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

            return traceHeader;
        }

        internal static void ProcessHTTPResponse(object sender, EventArgs e)
        {
            var context = ((HttpApplication)sender).Context;
            var response = context.Response;

            if (!AWSXRayRecorder.Instance.IsTracingDisabled() && response != null)
            {
                Dictionary<string, object> responseAttributes = new Dictionary<string, object>();
                ProcessResponseAttributes(response, responseAttributes);
                _recorder.AddHttpInformation("response", responseAttributes);
            }

            Exception exc = context.Error; // Record exception, if any

            if (exc != null)
            {
                _recorder.AddException(exc);
            }

            TraceHeader traceHeader = GetTraceHeader(context);
            bool isSampleDecisionRequested = traceHeader.Sampled == SampleDecision.Requested;

            if (traceHeader.Sampled == SampleDecision.Unknown || traceHeader.Sampled == SampleDecision.Requested)
            {
                SetSamplingDecision(traceHeader); // extracts sampling decision from the available segment
            }

            _recorder.EndSegment();
            // if the sample decision is requested, add the trace header to response
            if (isSampleDecisionRequested)
            {
                response.Headers.Add(TraceHeader.HeaderKey, traceHeader.ToString());
            }
        }

        private static void ProcessResponseAttributes(HttpResponse response, Dictionary<string, object> responseAttributes)
        {
            int statusCode = (int)response.StatusCode;
            responseAttributes["status"] = statusCode;

            if (statusCode >= 400 && statusCode <= 499)
            {
                _recorder.MarkError();

                if (statusCode == 429)
                {
                    _recorder.MarkThrottle();
                }
            }
            else if (statusCode >= 500 && statusCode <= 599)
            {
                _recorder.MarkFault();
            }
        }

        private static void SetSamplingDecision(TraceHeader traceHeader)
        {
            try
            {
                Segment segment = (Segment)AWSXRayRecorder.Instance.GetEntity();
                traceHeader.Sampled = segment.Sampled;
            }

            catch (InvalidCastException e)
            {
                _logger.Error(new EntityNotAvailableException("Failed to cast the entity to Segment.", e), "Failed to  get the segment from trace context for setting sampling decision in the response.");
            }

            catch (EntityNotAvailableException e)
            {
                AWSXRayRecorder.Instance.TraceContext.HandleEntityMissing(AWSXRayRecorder.Instance, e, "Failed to get entity since it is not available in trace context while processing ASPNET request.");
            }
        }

        internal static void ProcessHTTPError(object sender, EventArgs e)
        {
            ProcessHTTPRequest(sender, e);
        }
    }
}
#endif