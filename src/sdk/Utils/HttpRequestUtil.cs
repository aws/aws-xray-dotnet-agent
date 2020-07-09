//-----------------------------------------------------------------------------
// <copyright file="HttpRequestUtil.cs" company="Amazon.com">
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

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Amazon.Runtime.Internal.Util;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Exceptions;
using Amazon.XRay.Recorder.Core.Internal.Entities;

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

            AgentUtil.MarkEntityFromStatus(statusCode);

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
            AWSXRayRecorder.Instance.SetEntity(subsegment);
            ProcessResponse(httpStatusCode, contentLength);
        }

        /// <summary>
        /// Handles status code when an exception occurs
        /// </summary>
        internal static void HandleStatus(HttpStatusCode httpStatusCode, Subsegment subsegment)
        {
            ProcessResponse(httpStatusCode, null, subsegment);
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
            AWSXRayRecorder.Instance.SetEntity(subsegment);
            AWSXRayRecorder.Instance.EndSubsegment();
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
            
            return false;
        }
    }
}
