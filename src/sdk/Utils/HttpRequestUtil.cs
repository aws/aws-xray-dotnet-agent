//-----------------------------------------------------------------------------
// <copyright file="HttpRequestUtil.cs" company="Amazon.com">
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

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Amazon.Runtime.Internal.Util;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Exceptions;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Amazon.XRay.Recorder.Core.Sampling;

namespace Amazon.XRay.Recorder.AutoInstrumentation.Utils
{
    /// <summary>
    /// Utils for process http outgoing request and response.
    /// </summary>
    public static class HttpRequestUtil
    {
        private static readonly Logger _logger = Logger.GetLogger(typeof(HttpRequestUtil));

        /// <summary>
        /// Collects information from and adds a tracing header to the request.
        /// </summary>
        internal static void ProcessRequest(WebRequest request)
        {
            ProcessRequest(request.RequestUri, request.Method, header => request.Headers.Add(TraceHeader.HeaderKey, header));
        }

        /// <summary>
        /// Collects information from and adds a tracing header to the request.
        /// </summary>
        internal static void ProcessRequest(HttpRequestMessage request)
        {
            ProcessRequest(request.RequestUri, request.Method.Method, AddOrReplaceHeader);

            void AddOrReplaceHeader(string header)
            {
                request.Headers.Remove(TraceHeader.HeaderKey);
                request.Headers.Add(TraceHeader.HeaderKey, header);
            }
        }

        /// <summary>
        /// Collects information from the response and adds to <see cref="AWSXRayRecorder"/> instance.
        /// </summary>
        internal static void ProcessResponse(HttpWebResponse response)
        {
            if (response != null)
            {
                ProcessResponse(response.StatusCode, response.ContentLength);
            }
        }

        /// <summary>
        /// Collects information from the response and adds to <see cref="AWSXRayRecorder"/> instance.
        /// </summary>
        internal static void ProcessResponse(HttpResponseMessage response)
        {
            if (response != null)
            {
                ProcessResponse(response.StatusCode, response.Content.Headers.ContentLength);
            }
        }

        /// <summary>
        /// Process Request
        /// </summary>
        private static void ProcessRequest(Uri uri, string method, Action<string> addHeaderAction)
        {
            if (AWSXRayRecorder.Instance.IsTracingDisabled())
            {
                _logger.DebugFormat("Tracing is disabled. Not starting a subsegment on HTTP request.");
                return;
            }

            var recorder = AWSXRayRecorder.Instance;
            recorder.BeginSubsegment(uri.Host);
            recorder.SetNamespace("remote");

            var requestInformation = new Dictionary<string, object>
            {
                ["url"] = uri.AbsoluteUri,
                ["method"] = method
            };
            recorder.AddHttpInformation("request", requestInformation);

            try
            {
                if (TraceHeader.TryParse(recorder.GetEntity(), out var header))
                {
                    addHeaderAction(header.ToString());
                }
            }
            catch (EntityNotAvailableException e)
            {
                recorder.TraceContext.HandleEntityMissing(recorder, e, "Failed to get entity since it is not available in trace context while processing http request.");
            }
        }

        /// <summary>
        /// Process response
        /// </summary>
        private static void ProcessResponse(HttpStatusCode httpStatusCode, long? contentLength)
        {
            if (AWSXRayRecorder.Instance.IsTracingDisabled())
            {
                _logger.DebugFormat("Tracing is disabled. Not ending a subsegment on HTTP response.");
                return;
            }

            var statusCode = (int)httpStatusCode;

            var responseInformation = new Dictionary<string, object> { ["status"] = statusCode };
            if (statusCode >= 400 && statusCode <= 499)
            {
                AWSXRayRecorder.Instance.MarkError();

                if (statusCode == 429)
                {
                    AWSXRayRecorder.Instance.MarkThrottle();
                }
            }
            else if (statusCode >= 500 && statusCode <= 599)
            {
                AWSXRayRecorder.Instance.MarkFault();
            }

            responseInformation["content_length"] = contentLength;
            AWSXRayRecorder.Instance.AddHttpInformation("response", responseInformation);
        }

        /// <summary>
        /// Process exception
        /// </summary>
        internal static void ProcessException(Exception exception)
        {
            AWSXRayRecorder.Instance.AddException(exception);
        }

        /// <summary>
        /// Process response
        /// </summary>
        internal static void ProcessResponse(HttpStatusCode httpStatusCode, long? contentLength, Subsegment subsegment)
        {
            if (AWSXRayRecorder.Instance.IsTracingDisabled())
            {
                _logger.DebugFormat("X-Ray tracing is disabled, do not process response");
                return;
            }

            var statusCode = (int)httpStatusCode;

            var responseInformation = new Dictionary<string, object> { ["status"] = statusCode };
            if (statusCode >= 400 && statusCode <= 499)
            {
                subsegment.HasError = true;
                subsegment.HasFault = false;

                if (statusCode == 429)
                {
                    subsegment.IsThrottled = true;
                }
            }
            else if (statusCode >= 500 && statusCode <= 599)
            {
                subsegment.HasFault = true;
                subsegment.HasError = false;
            }

            responseInformation["content_length"] = contentLength;
            subsegment.Http["response"] = responseInformation;
        }

        /// <summary>
        /// End subsegment.
        /// </summary>
        internal static void EndSubsegment()
        {
            AWSXRayRecorder.Instance.EndSubsegment();
        }

        /// <summary>
        /// End subsegment
        /// </summary>
        internal static void EndSubsegment(Subsegment subsegment)
        {
            if (AWSXRayRecorder.Instance.IsTracingDisabled())
            {
                _logger.DebugFormat("X-Ray tracing is disabled, do not end subsegment");
                return;
            }

            if (subsegment.Sampled != SampleDecision.Sampled)
            {
                return;
            }

            subsegment.IsInProgress = false;

            // Restore parent segment to trace context
            if (subsegment.Parent != null)
            {
                AWSXRayRecorder.Instance.TraceContext.SetEntity(subsegment.Parent);
            }

            // Drop ref count
            subsegment.Release();
            subsegment.SetEndTimeToNow();

            // Check emittable
            if (subsegment.IsEmittable())
            {
                // Emit
                AWSXRayRecorder.Instance.Emitter.Send(subsegment.RootSegment);
            }
            else if (AWSXRayRecorder.Instance.StreamingStrategy.ShouldStream(subsegment))
            {
                AWSXRayRecorder.Instance.StreamingStrategy.Stream(subsegment.RootSegment, AWSXRayRecorder.Instance.Emitter);
            }
        }

        /// <summary>
        /// Check if Http out going request should be traced.
        /// Http out going request that is GetSamplingRules or GetSamplingTargets call will not be traced.
        /// Http out going request that is sent by AWS SDK will no be traced.
        /// </summary>
        internal static bool IsTraceable(HttpRequestMessage request)
        {
            if (request == null || request.RequestUri == null)
            {
                return false;
            }
            var url = request.RequestUri.ToString();
            return !IsSamplingCall(url) && !IsAWSSDKRequest();
        }

        internal static bool IsTraceable(HttpWebRequest request)
        {
            if (request == null || request.RequestUri == null)
            {
                return false;
            }
            var url = request.RequestUri.ToString();
            return !IsSamplingCall(url) && !IsAWSSDKRequest();
        }

        /// <summary>
        /// Check if it's a call for get sampling rules or sampling targets.
        /// </summary>
        private static bool IsSamplingCall(string url)
        {
            return url.Contains("GetSamplingRules") || url.Contains("SamplingTargets");
        }

        /// <summary>
        /// Check if request is sent via AWS SDK handler.
        /// </summary>
        private static bool IsAWSSDKRequest()
        {
            try
            {
                var subsegment = AWSXRayRecorder.Instance.GetEntity() as Subsegment;
                if (subsegment == null || subsegment.Namespace == null)
                {
                    return false;
                }
                return subsegment.Namespace.Equals("aws") && subsegment.IsInProgress;
            }
            catch (EntityNotAvailableException e)
            {
                AWSXRayRecorder.Instance.TraceContext.HandleEntityMissing(AWSXRayRecorder.Instance, e, "Failed to get entity since it is not available in trace context.");
            }
            catch (InvalidCastException e)
            {
                _logger.Error(new EntityNotAvailableException("Failed to cast the entity to Subsegment.", e), "Failed to get the Subsegment from trace context.");
            }

            return false;
        }
    }
}