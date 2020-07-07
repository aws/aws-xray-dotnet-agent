//-----------------------------------------------------------------------------
// <copyright file="UtilTest.cs" company="Amazon.com">
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
using Amazon.XRay.Recorder.AutoInstrumentation.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Data.Common;

namespace Amazon.XRay.Recorder.AutoInstrumentation.Unittests
{
    [TestClass]
    public class UtilTest : TestBase
    {
        private string SqlServerConnectionString = "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;port=1234;";
        private string MySqlConnectionString = "server=localhost;database=temp;user=root;pwd=1234abcD*;port=1234;";
        private string SqliteConnectionString = "Data Source=/ex1.db;port=1234;";
        private string PostgreSqlConnectionString = "Host=localhost;Database=postgres;Userid=postgres;password=1234abcD*;Port=1234;";
        private string FirebirdSqlConnectionString = "Username=SYSDBA;Password=masterkey;Database=/firebird.fdb;DataSource=localhost;port=1234;";

        [TestCleanup]
        public new void TestCleanup()
        {
            base.TestCleanup();
        }

        [TestMethod]
        public void TestGetUserIdMySql()
        {
            DbConnectionStringBuilder builder = new DbConnectionStringBuilder()
            {
                ConnectionString = MySqlConnectionString
            };
            object result = AgentUtil.GetUserId(builder);
            Assert.AreEqual("root", result.ToString());
        }

        [TestMethod]
        public void TestGetUserIdSqlServer()
        {
            DbConnectionStringBuilder builder = new DbConnectionStringBuilder()
            {
                ConnectionString = SqlServerConnectionString
            };
            object result = AgentUtil.GetUserId(builder);
            Assert.AreEqual("myUsername", result.ToString());
        }

        [TestMethod]
        public void TestGetUserIdSqlite()
        {
            DbConnectionStringBuilder builder = new DbConnectionStringBuilder()
            {
                ConnectionString = SqliteConnectionString
            };
            object result = AgentUtil.GetUserId(builder);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestGetUserIdPostgreSql()
        {
            DbConnectionStringBuilder builder = new DbConnectionStringBuilder()
            {
                ConnectionString = PostgreSqlConnectionString
            };
            object result = AgentUtil.GetUserId(builder);
            Assert.AreEqual("postgres", result.ToString());
        }

        [TestMethod]
        public void TestGetUserIdFirebirdSql()
        {
            DbConnectionStringBuilder builder = new DbConnectionStringBuilder()
            {
                ConnectionString = FirebirdSqlConnectionString
            };
            object result = AgentUtil.GetUserId(builder);
            Assert.AreEqual("SYSDBA", result.ToString());
        }
    }
}
#endif