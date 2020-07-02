//-----------------------------------------------------------------------------
// <copyright file="XRayConfigurationTest.cs" company="Amazon.com">
//      Copyright 2017 Amazon.com, Inc. or its affiliates. All Rights Reserved.
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
using Amazon.XRay.Recorder.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Amazon.XRay.Recorder.Core.Internal.Utils;
using System.Configuration;

namespace Amazon.XRay.Recorder.AutoInstrumentation.Unittests
{
    [TestClass]
    public class XRayConfigurationTest : TestBase
    {

        [TestCleanup]
        public new void TestCleanup()
        {
            ConfigurationManager.AppSettings["DisableXRayTracing"] = null;
            ConfigurationManager.AppSettings["CollectSqlQueries"] = null;
            ConfigurationManager.AppSettings["UseRuntimeErrors"] = null;
            ConfigurationManager.AppSettings["ServiceName"] = null;
            ConfigurationManager.AppSettings["DaemonAddress"] = null;
            ConfigurationManager.AppSettings["TraceHttpRequests"] = null;
            ConfigurationManager.AppSettings["TraceAWSRequests"] = null;
            ConfigurationManager.AppSettings["TraceSqlRequests"] = null;
            ConfigurationManager.AppSettings["TraceEFRequests"] = null;
            AppSettings.Reset();
            base.TestCleanup();
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTracingDisabled()
        {
            ConfigurationManager.AppSettings["DisableXRayTracing"] = "true";
            AppSettings.Reset();
            Assert.IsTrue(AppSettings.IsXRayTracingDisabled);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTracingEnabled()
        {
            ConfigurationManager.AppSettings["DisableXRayTracing"] = "false";
            AppSettings.Reset();
            Assert.IsFalse(AppSettings.IsXRayTracingDisabled);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTracingDefault()
        {
            AppSettings.Reset();
            Assert.IsFalse(AppSettings.IsXRayTracingDisabled);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTracingKeyInvalid()
        {
            ConfigurationManager.AppSettings["DisableXRayTracing"] = "invalid";
            AppSettings.Reset();
            Assert.IsFalse(AppSettings.IsXRayTracingDisabled);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkRuntimeErrorTrue()
        {
            ConfigurationManager.AppSettings["UseRuntimeErrors"] = "true";
            AppSettings.Reset();
            var recorder = GetRecorder();
            Assert.AreEqual(Core.Strategies.ContextMissingStrategy.RUNTIME_ERROR, recorder.ContextMissingStrategy);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkRuntimeErrorFalse()
        {
            ConfigurationManager.AppSettings["UseRuntimeErrors"] = "false";
            AppSettings.Reset();
            var recorder = GetRecorder();
            Assert.AreEqual(Core.Strategies.ContextMissingStrategy.LOG_ERROR, recorder.ContextMissingStrategy);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkRuntimeErrorDefault()
        {
            AppSettings.Reset();
            var recorder = GetRecorder();
            Assert.AreEqual(Core.Strategies.ContextMissingStrategy.RUNTIME_ERROR, recorder.ContextMissingStrategy);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkRuntimeErrorKeyInvalid()
        {
            ConfigurationManager.AppSettings["UseRuntimeErrors"] = "invalid";
            AppSettings.Reset();
            var recorder = GetRecorder();
            Assert.AreEqual(Core.Strategies.ContextMissingStrategy.RUNTIME_ERROR, recorder.ContextMissingStrategy);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkCollectSqlQueriesTrue()
        {
            ConfigurationManager.AppSettings["CollectSqlQueries"] = "true";
            AppSettings.Reset();
            Assert.IsTrue(AppSettings.CollectSqlQueries);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkCollectSqlQueriesFlase()
        {
            ConfigurationManager.AppSettings["CollectSqlQueries"] = "false";
            AppSettings.Reset();
            Assert.IsFalse(AppSettings.CollectSqlQueries);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkCollectSqlQueriesDefault()
        {
            AppSettings.Reset();
            Assert.IsFalse(AppSettings.CollectSqlQueries);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkCollectSqlQueriesKeyInvalid()
        {
            ConfigurationManager.AppSettings["CollectSqlQueries"] = "invalid";
            AppSettings.Reset();
            Assert.IsFalse(AppSettings.CollectSqlQueries);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkServiceName()
        {
            ConfigurationManager.AppSettings["ServiceName"] = "UnittestSample";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.AreEqual("UnittestSample", xrayAutoInstrumentationOptions.ServiceName);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkServiceNameDefault()
        {
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.AreEqual("DefaultService", xrayAutoInstrumentationOptions.ServiceName);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkDaemonAddress()
        {
            ConfigurationManager.AppSettings["DaemonAddress"] = "1.2.3.4:2000";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.AreEqual("1.2.3.4:2000", xrayAutoInstrumentationOptions.DaemonAddress);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkDaemonAddressDefault()
        {
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.AreEqual("127.0.0.1:2000", xrayAutoInstrumentationOptions.DaemonAddress);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceHttpRequestsTrue()
        {
            ConfigurationManager.AppSettings["TraceHttpRequests"] = "true";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsTrue(xrayAutoInstrumentationOptions.TraceHttpRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceHttpRequestsFalse()
        {
            ConfigurationManager.AppSettings["TraceHttpRequests"] = "false";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsFalse(xrayAutoInstrumentationOptions.TraceHttpRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceHttpRequestsDefault()
        {
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsTrue(xrayAutoInstrumentationOptions.TraceHttpRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceHttpRequestsInvalid()
        {
            ConfigurationManager.AppSettings["TraceHttpRequests"] = "invalid";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsTrue(xrayAutoInstrumentationOptions.TraceHttpRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceAWSRequestsTrue()
        {
            ConfigurationManager.AppSettings["TraceAWSRequests"] = "true";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsTrue(xrayAutoInstrumentationOptions.TraceAWSRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceAWSRequestsFalse()
        {
            ConfigurationManager.AppSettings["TraceAWSRequests"] = "false";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsFalse(xrayAutoInstrumentationOptions.TraceAWSRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceAWSRequestsDefault()
        {
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsTrue(xrayAutoInstrumentationOptions.TraceAWSRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceAWSRequestsInvalid()
        {
            ConfigurationManager.AppSettings["TraceAWSRequests"] = "invalid";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsTrue(xrayAutoInstrumentationOptions.TraceAWSRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceSqlRequestsTrue()
        {
            ConfigurationManager.AppSettings["TraceSqlRequests"] = "true";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsTrue(xrayAutoInstrumentationOptions.TraceSqlRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceSqlRequestsFalse()
        {
            ConfigurationManager.AppSettings["TraceSqlRequests"] = "false";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsFalse(xrayAutoInstrumentationOptions.TraceSqlRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceSqlRequestsDefault()
        {
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsTrue(xrayAutoInstrumentationOptions.TraceSqlRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceSqlRequestsInvalid()
        {
            ConfigurationManager.AppSettings["TraceSqlRequests"] = "invalid";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsTrue(xrayAutoInstrumentationOptions.TraceSqlRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceEFRequestsTrue()
        {
            ConfigurationManager.AppSettings["TraceEFRequests"] = "true";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsTrue(xrayAutoInstrumentationOptions.TraceEFRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceEFRequestsFalse()
        {
            ConfigurationManager.AppSettings["TraceEFRequests"] = "false";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsFalse(xrayAutoInstrumentationOptions.TraceEFRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceEFRequestsDefault()
        {
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsTrue(xrayAutoInstrumentationOptions.TraceEFRequests);
        }

        [TestMethod]
        public void TestXRayConfigureNetframeworkTraceEFRequestsInvalid()
        {
            ConfigurationManager.AppSettings["TraceSqlRequests"] = "invalid";
            var xrayAutoInstrumentationOptions = XRayConfiguration.Register();
            AppSettings.Reset();
            Assert.IsTrue(xrayAutoInstrumentationOptions.TraceEFRequests);
        }

        private AWSXRayRecorder GetRecorder()
        {
            return new AWSXRayRecorderBuilder().WithPluginsFromAppSettings().WithContextMissingStrategyFromAppSettings().Build();
        }
    }
}
#endif