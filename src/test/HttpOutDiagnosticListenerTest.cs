//-----------------------------------------------------------------------------
// <copyright file="HttpOutDiagnosticListenerTest.cs" company="Amazon.com">
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

using Amazon.XRay.Recorder.AutoInstrumentation.UnitTests.Tools;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
#if !NET45
using System.Linq;
#endif
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Amazon.XRay.Recorder.AutoInstrumentation.Unittests
{
    [TestClass]
    public class HttpOutDiagnosticListenerTest : TestBase
    {
        private const string URL = "https://httpbin.org/";

        private const string URL404 = "https://httpbin.org/404";

        private readonly HttpClient _httpClient;

        private static AWSXRayRecorder _recorder;

        private static IDisposable _subscription;

        public HttpOutDiagnosticListenerTest()
        {
            _httpClient = new HttpClient();

            var subscription = new List<DiagnosticListenerBase>()
            {
#if NET45
                new HttpOutDiagnosticListenerNetframework()
#else
                new HttpOutDiagnosticListenerNetstandard()
#endif
            };

            _subscription = DiagnosticListener.AllListeners.Subscribe(new DiagnosticListenerObserver(subscription));
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _recorder = new AWSXRayRecorder();
#if NET45
            AWSXRayRecorder.InitializeInstance(_recorder);
#else
            AWSXRayRecorder.InitializeInstance(recorder : _recorder);
#endif
        }

        [TestCleanup]
        public new void TestCleanup()
        {
            base.TestCleanup();
            _recorder.Dispose();
            _recorder = null;
            _subscription.Dispose();
        }

        [TestMethod]
        public async Task TestHttpClientSendAsync()
        {
            AWSXRayRecorder.Instance.BeginSegment("HttpClientSegment", TraceId);
            var request = new HttpRequestMessage(HttpMethod.Get, URL);
            using (await _httpClient.SendAsync(request)) { }

            // HttpOutDiagnosticListenerNetframwork injects trace header in HttpWebRequest layer, upon which 
            // HttpClient layer is built, and trace header will be passed to downstream service through HttpWebRequest,
            // but will not be present in HttpClient header.
#if !NET45
            var traceHeader = request.Headers.GetValues(TraceHeader.HeaderKey).SingleOrDefault();
            Assert.IsNotNull(traceHeader);
#endif

            var segment = AWSXRayRecorder.Instance.TraceContext.GetEntity();
            AWSXRayRecorder.Instance.EndSegment();

            var requestInfo = segment.Subsegments[0].Http["request"] as Dictionary<string, object>;
            Assert.AreEqual(URL, requestInfo["url"]);
            Assert.AreEqual("GET", requestInfo["method"]);

            var responseInfo = segment.Subsegments[0].Http["response"] as Dictionary<string, object>;
            Assert.AreEqual(200, responseInfo["status"]);
            Assert.IsNotNull(responseInfo["content_length"]);
        }

        [TestMethod]
        public async Task TestHttpClientSendAsync404()
        {
            AWSXRayRecorder.Instance.BeginSegment("HttpClientSegment", TraceId);
            var request = new HttpRequestMessage(HttpMethod.Get, URL404);
            using (await _httpClient.SendAsync(request)) { }

            // HttpOutDiagnosticListenerNetframwork injects trace header in HttpWebRequest layer, upon which 
            // HttpClient layer is built, and trace header will be passed to downstream service through HttpWebRequest,
            // but will not be present in HttpClient header.
#if !NET45
            var traceHeader = request.Headers.GetValues(TraceHeader.HeaderKey).SingleOrDefault();
            Assert.IsNotNull(traceHeader);
#endif

            var segment = AWSXRayRecorder.Instance.TraceContext.GetEntity();
            AWSXRayRecorder.Instance.EndSegment();

            var requestInfo = segment.Subsegments[0].Http["request"] as Dictionary<string, object>;
            Assert.AreEqual(URL404, requestInfo["url"]);
            Assert.AreEqual("GET", requestInfo["method"]);

            Assert.IsTrue(segment.Subsegments[0].HasError);
            Assert.IsFalse(segment.Subsegments[0].HasFault);
        }

        [TestMethod]
        public void TestHttpWebRequestSend()
        {
            AWSXRayRecorder.Instance.BeginSegment("HttpWebRequestSegment", TraceId);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
            using (var response = request.GetResponse()) { }

            var segment = AWSXRayRecorder.Instance.TraceContext.GetEntity();
            AWSXRayRecorder.Instance.EndSegment();

            // HttpOutDiagnosticListenerNetstandard injects trace header in HttpRequestMessage layer, upon which 
            // HttpWebRequest layer is built, and trace header will be passed to downstream service through HttpRequestMessage,
            // but will not be present in HttpWebRequest header.
#if NET45
            Assert.IsNotNull(request.Headers[TraceHeader.HeaderKey]);
#endif

            var requestInfo = segment.Subsegments[0].Http["request"] as Dictionary<string, object>;
            Assert.AreEqual(URL, requestInfo["url"]);
            Assert.AreEqual("GET", requestInfo["method"]);

            var responseInfo = segment.Subsegments[0].Http["response"] as Dictionary<string, object>;
            Assert.AreEqual(200, responseInfo["status"]);
            Assert.IsNotNull(responseInfo["content_length"]);
        }

        [TestMethod]
        public void TestHttpWebRequestSend404()
        {
            AWSXRayRecorder.Instance.BeginSegment("HttpWebRequestSegment", TraceId);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL404);
            try
            {
                using (var response = request.GetResponse())
                {
                }
                Assert.Fail();
            }
            catch (WebException) // Expected
            {
                var segment = AWSXRayRecorder.Instance.TraceContext.GetEntity();
                AWSXRayRecorder.Instance.EndSegment();

                // HttpOutDiagnosticListenerNetstandard injects trace header in HttpRequestMessage layer, upon which 
                // HttpWebRequest layer is built, and trace header will be passed to downstream service through HttpRequestMessage,
                // but will not be present in HttpWebRequest header.
#if NET45
                Assert.IsNotNull(request.Headers[TraceHeader.HeaderKey]);
#endif

                var requestInfo = segment.Subsegments[0].Http["request"] as Dictionary<string, object>;
                Assert.AreEqual(URL404, requestInfo["url"]);
                Assert.AreEqual("GET", requestInfo["method"]);

                var subsegment = segment.Subsegments[0];
                Assert.IsTrue(subsegment.HasError);
                Assert.IsFalse(subsegment.HasFault);
            }
        }

        [TestMethod]
        public async Task TestHttpWebRequestSendAsync()
        {
            AWSXRayRecorder.Instance.BeginSegment("HttpWebRequestSegment", TraceId);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
            using (await request.GetResponseAsync())
            {
            }

            var segment = AWSXRayRecorder.Instance.TraceContext.GetEntity();
            AWSXRayRecorder.Instance.EndSegment();

            // HttpOutDiagnosticListenerNetstandard injects trace header in HttpRequestMessage layer, upon which 
            // HttpWebRequest layer is built, and trace header will be passed to downstream service through HttpRequestMessage,
            // but will not be present in HttpWebRequest header.
#if NET45
            Assert.IsNotNull(request.Headers[TraceHeader.HeaderKey]);
#endif

            var requestInfo = segment.Subsegments[0].Http["request"] as Dictionary<string, object>;
            Assert.AreEqual(URL, requestInfo["url"]);
            Assert.AreEqual("GET", requestInfo["method"]);

            var responseInfo = segment.Subsegments[0].Http["response"] as Dictionary<string, object>;
            Assert.AreEqual(200, responseInfo["status"]);
            Assert.IsNotNull(responseInfo["content_length"]);
        }

        [TestMethod]
        public async Task TestHttpWebRequestSendAsync404()
        {
            AWSXRayRecorder.Instance.BeginSegment("HttpWebRequestSegment", TraceId);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL404);
            try
            {
                using (await request.GetResponseAsync())
                {
                }
                Assert.Fail();
            }
            catch (WebException) // Expected
            {
                var segment = AWSXRayRecorder.Instance.TraceContext.GetEntity();
                AWSXRayRecorder.Instance.EndSegment();

                // HttpOutDiagnosticListenerNetstandard injects trace header in HttpRequestMessage layer, upon which 
                // HttpWebRequest layer is built, and trace header will be passed to downstream service through HttpRequestMessage,
                // but will not be present in HttpWebRequest header.
#if NET45
                Assert.IsNotNull(request.Headers[TraceHeader.HeaderKey]);
#endif

                var requestInfo = segment.Subsegments[0].Http["request"] as Dictionary<string, object>;
                Assert.AreEqual(URL404, requestInfo["url"]);
                Assert.AreEqual("GET", requestInfo["method"]);

                var subsegment = segment.Subsegments[0];
                Assert.IsTrue(subsegment.HasError);
                Assert.IsFalse(subsegment.HasFault);
            }
        }

        [TestMethod]
        public async Task TestXrayDisabledSendAsync()
        {
            _recorder = new MockAWSXRayRecorder() { IsTracingDisabledValue = true };

#if NET45
            AWSXRayRecorder.InitializeInstance(_recorder);
#else
            AWSXRayRecorder.InitializeInstance(recorder: _recorder);
#endif

            Assert.IsTrue(AWSXRayRecorder.Instance.IsTracingDisabled());

            var request = new HttpRequestMessage(HttpMethod.Get, URL);
            using (var response = await _httpClient.SendAsync(request))
            {
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [TestMethod]
        public async Task TestXrayContextMissingStrategySendAsync() // Test that respects ContextMissingStrategy
        {
            _recorder = new MockAWSXRayRecorder();

#if NET45
            AWSXRayRecorder.InitializeInstance(_recorder);
#else
            AWSXRayRecorder.InitializeInstance(recorder: _recorder);
#endif

            AWSXRayRecorder.Instance.ContextMissingStrategy = Core.Strategies.ContextMissingStrategy.LOG_ERROR;

            Assert.IsFalse(AWSXRayRecorder.Instance.IsTracingDisabled());

            _recorder.EndSegment();

            // The test should not break. No segment is available in the context, however, since the context missing strategy is log error,
            // no exception should be thrown by below code.

            var request = new HttpRequestMessage(HttpMethod.Get, URL);
            using (var response = await _httpClient.SendAsync(request))
            {
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }
        }
    }
}