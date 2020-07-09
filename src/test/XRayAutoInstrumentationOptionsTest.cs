//-----------------------------------------------------------------------------
// <copyright file="XRayAutoInstrumentationOptionsTest.cs" company="Amazon.com">
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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Amazon.XRay.Recorder.AutoInstrumentation.Unittests
{
    [TestClass]
    public class XRayAutoInstrumentationOptionsTest : TestBase
    {
        private XRayAutoInstrumentationOptions _options;

        [TestCleanup]
        public new void TestCleanup()
        {
            base.TestCleanup();
            _options = null;
        }

        [TestMethod]
        public void TestXRayAutoInstrumentationOptionsDefaultValue()
        {
            // Default values
            _options = new XRayAutoInstrumentationOptions();

            Assert.AreEqual("DefaultService", _options.ServiceName); // Default Value: "DefaultService"
            Assert.AreEqual("127.0.0.1:2000", _options.DaemonAddress); // Default Value: "DaemonAddress"
            Assert.IsTrue(_options.TraceHttpRequests); // Default Value : true
            Assert.IsTrue(_options.TraceAWSRequests); // Default Value : true
            Assert.IsTrue(_options.TraceSqlRequests); // Default Value : true
            Assert.IsTrue(_options.TraceEFRequests); // Default Value : true
        }

        [TestMethod]
        public void TestXRayAutoInstrumentationOptionsCustomizedValue()
        {
            _options = new XRayAutoInstrumentationOptions("TestApplication", "127.0.0.0:2000", false, true, false, true);

            Assert.AreEqual("TestApplication", _options.ServiceName);
            Assert.AreEqual("127.0.0.0:2000", _options.DaemonAddress);
            Assert.IsFalse(_options.TraceHttpRequests);
            Assert.IsTrue(_options.TraceAWSRequests);
            Assert.IsFalse(_options.TraceSqlRequests);
            Assert.IsTrue(_options.TraceEFRequests);
        }
    }
}
