//-----------------------------------------------------------------------------
// <copyright file="EntityFrameworkWithSqlDiagnosticListenerTest.cs" company="Amazon.com">
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
    public class EntityFrameworkWithSqlDiagnosticListenerTest : TestBase
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
        public void TestEFCoreRequestWithEntityFrameworkCoreAndSqlDiagnosticListener()
        {
            // EntityFramework request will first trigger EntityFrameworkCoreDiagnosticListener and then SqlDiagnosticListener,
            // With EntityFrameworkCoreDiagnosticListener, SqlDiagnosticListener will process EF Core request.
            var subscription = new List<DiagnosticListenerBase>()
            {
                new EntityFrameworkCoreDiagnosticListener(),
                new SqlDiagnosticListener()
            };

            _subscription = DiagnosticListener.AllListeners.Subscribe(new DiagnosticListenerObserver(subscription));

            _recorder.BeginSegment("EFCoreRequestWithEntityFrameworkCoreAndSqlDiagnosticListener");

            var context = GetTestEFContext();

            var users = context.Users.Where(u => u.UserId == 1).ToList();

            var segment = _recorder.TraceContext.GetEntity();
            Assert.AreEqual(4, segment.Subsegments.Count);
            var subsegment = segment.Subsegments[3];
            Assert.IsNotNull(subsegment);
            Assert.IsNotNull(subsegment.Sql);
            Assert.AreEqual(0, subsegment.Subsegments.Count); // No nested duplicate subsegment
            Assert.AreEqual("sqlite", subsegment.Sql["database_type"]);
            Assert.AreEqual(_connectionString, subsegment.Sql["connection_string"]);
            Assert.AreEqual(connection.ServerVersion, subsegment.Sql["database_version"]);
            _recorder.EndSegment();
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