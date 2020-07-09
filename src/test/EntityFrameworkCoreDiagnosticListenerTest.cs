//-----------------------------------------------------------------------------
// <copyright file="EntityFrameworkCoreDiagnosticListenerTest.cs" company="Amazon.com">
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
using Amazon.XRay.Recorder.AutoInstrumentation.Unittests.Tools;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Amazon.XRay.Recorder.AutoInstrumentation.Unittests
{
    [TestClass]
    public class EntityFrameworkCoreDiagnosticListenerTest : TestBase
    {
        private const string _connectionString = "datasource=:memory:";
        private SqliteConnection connection = null;
        
        private static IDisposable _subscription;

        private static AWSXRayRecorder _recorder;


        [TestInitialize]
        public void TestInitialize()
        {
            _recorder = new AWSXRayRecorder();
            AWSXRayRecorder.InitializeInstance(recorder: _recorder);

            var subscription = new List<DiagnosticListenerBase>()
            {
                new EntityFrameworkCoreDiagnosticListener()
            };

            _subscription = DiagnosticListener.AllListeners.Subscribe(new DiagnosticListenerObserver(subscription));

            // In-memory database only exists while the connection is open
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
        }

        [TestCleanup]
        public new void TestCleanup()
        {
            connection.Close();
            base.TestCleanup();
            _recorder.Dispose();
            _recorder = null;
            _subscription.Dispose();
        }

        [TestMethod]
        public void Test_EFCore_successful_query()
        {
            AWSXRayRecorder.Instance.XRayOptions.CollectSqlQueries = true;

            AWSXRayRecorder.Instance.BeginSegment("TestSegment");
            var context = GetTestEFContext();

            var users = context.Users.Where(u => u.UserId == 1).ToList();

            // Assert
            var segment = AWSXRayRecorder.Instance.TraceContext.GetEntity();
            Assert.AreEqual(4, segment.Subsegments.Count); 
            var query_subsegment = segment.Subsegments[3];
            AssertQueryCollected(query_subsegment);

            AWSXRayRecorder.Instance.EndSegment();
        }

        [TestMethod]
        public void Test_EFCore_unsuccessful_query()
        {
            AWSXRayRecorder.Instance.XRayOptions.CollectSqlQueries = false;

            AWSXRayRecorder.Instance.BeginSegment("TestSegment");
            var context = GetTestEFContext();
            var users = context.Users.Where(u => u.UserId == 1).ToList();

            var segment = AWSXRayRecorder.Instance.TraceContext.GetEntity();
            Assert.AreEqual(4, segment.Subsegments.Count);
            var query_subsegment = segment.Subsegments[3];
            AssertQueryNotCollected(query_subsegment);
            AWSXRayRecorder.Instance.EndSegment();
        }

        [TestMethod]
        public void Test_EFCore_query_with_exception()
        {
            AWSXRayRecorder.Instance.BeginSegment("TestSegment");
            var context = GetTestEFContext();

            try
            {
                context.Database.ExecuteSqlCommand("Select * From FakeTable"); // A false sql command which results in 'no such table: FakeTable' exception
            }
            catch
            {
                // ignore
            }

            var segment = AWSXRayRecorder.Instance.TraceContext.GetEntity();
            Assert.AreEqual(4, segment.Subsegments.Count);
            var subsegment = segment.Subsegments[3];
            Assert.AreEqual(true, subsegment.HasFault);
            AWSXRayRecorder.Instance.EndSegment();
            AWSXRayRecorder.Instance.Dispose();
        }

        private void AssertQueryCollected(Subsegment subsegment)
        {
            AssertExpectedSqlInformation(subsegment);
            Assert.IsTrue(subsegment.Sql.ContainsKey("sanitized_query"));
        }

        private void AssertQueryNotCollected(Subsegment subsegment)
        {
            AssertExpectedSqlInformation(subsegment);
            Assert.IsFalse(subsegment.Sql.ContainsKey("sanitized_query"));
        }

        private void AssertExpectedSqlInformation(Subsegment subsegment)
        {
            Assert.IsNotNull(subsegment);
            Assert.IsNotNull(subsegment.Sql);
            Assert.AreEqual(_connectionString, subsegment.Sql["connection_string"]);
            Assert.AreEqual(connection.ServerVersion, subsegment.Sql["database_version"]);
        }

        private MockEntityFrameworkCoreDbContext GetTestEFContext()
        {
            var options = new DbContextOptionsBuilder<MockEntityFrameworkCoreDbContext>()
                    .UseSqlite(connection)
                    .Options;
            var context = new MockEntityFrameworkCoreDbContext(options);
            context.Database.EnsureCreated();

            return context;
        }
    }
}
#endif
