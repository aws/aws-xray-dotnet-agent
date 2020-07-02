//-----------------------------------------------------------------------------
// <copyright file="TestXRayConfigureNetstandard.cs" company="Amazon.com">
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

#if !NET45
using Amazon.XRay.Recorder.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Amazon.XRay.Recorder.AutoInstrumentation.Unittests
{
    [TestClass]
    class XRayConfigurationNetstandardTest : TestBase
    {
        [TestMethod]
        public void TestXRayConfigureNetstandard()
        {
            //  Register the configurations from appsettings.json file in the unittest folder
            //  Register the following items, if provided, for AWS XRay .Net Core SDK
            //      "DisableXRayTracing" : bool
            //      "SamplingRuleManifest" : string
            //      "AWSXRayPlugins" : string
            //      "AwsServiceHandlerManifest" : string
            //      "UseRuntimeErrors" : bool
            //      "CollectSqlQueries" : bool
            //  AND register the following items, if provided, for Auto-instrumentation SDK
            //      "ServiceName" : string
            //      "DaemonAddress" : string
            //      "TraceHttpRequests" : bool
            //      "TraceAWSRequests" : bool
            //      "TraceSqlRequests" : bool
            //      "TraceEFReqeusts" : bool
            var xrayAutoInstrumentationoptions = XRayConfiguration.Register();

            var _recorder = AWSXRayRecorder.Instance;

            Assert.IsFalse(_recorder.XRayOptions.IsXRayTracingDisabled);
            Assert.IsTrue(_recorder.XRayOptions.UseRuntimeErrors);
            Assert.IsFalse(_recorder.XRayOptions.CollectSqlQueries);
            Assert.AreEqual("UnittestSample", xrayAutoInstrumentationoptions.ServiceName);
            Assert.AreEqual("127.0.0.1:2000", xrayAutoInstrumentationoptions.DaemonAddress);
            Assert.IsTrue(xrayAutoInstrumentationoptions.TraceHttpRequests);
            Assert.IsFalse(xrayAutoInstrumentationoptions.TraceAWSRequests);
            Assert.IsTrue(xrayAutoInstrumentationoptions.TraceSqlRequests);
            Assert.IsFalse(xrayAutoInstrumentationoptions.TraceEFRequests);
        }
    }
}
#endif