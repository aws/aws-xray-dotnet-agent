//-----------------------------------------------------------------------------
// <copyright file="EntityFrameworkHandler.cs" company="Amazon.com">
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

#if NET45
using Amazon.XRay.Recorder.AutoInstrumentation.Utils;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Entity Framework handler for tracing Sql query through EF 6
    /// </summary>
    public class EntityFrameworkHandler : IDbCommandInterceptor
    {

        /// <summary>
        /// Trace before executing non query command.
        /// </summary>
        /// <param name="command">An instance of <see cref="DbCommand"/>.</param>
        /// <param name="interceptionContext">An instance of <see cref="DbCommandInterceptionContext"/>.</param>
        public void NonQueryExecuting(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            SqlRequestUtil.BeginSubsegment(command);
            SqlRequestUtil.ProcessCommand(command);
        }

        /// <summary>
        /// Trace after executing non query command.
        /// </summary>
        /// <param name="command">An instance of <see cref="DbCommand"/>.</param>
        /// <param name="interceptionContext">An instance of <see cref="DbCommandInterceptionContext"/>.</param>
        public void NonQueryExecuted(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            if (interceptionContext.Exception != null)
            {
                SqlRequestUtil.ProcessException(interceptionContext.Exception);
            }

            SqlRequestUtil.EndSubsegment();
        }

        /// <summary>
        /// Trace before executing reader command.
        /// </summary>
        /// <param name="command">An instance of <see cref="DbCommand"/>.</param>
        /// <param name="interceptionContext">An instance of <see cref="DbCommandInterceptionContext"/>.</param>
        public void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            SqlRequestUtil.BeginSubsegment(command);
            SqlRequestUtil.ProcessCommand(command);
        }

        /// <summary>
        /// Trace after executing reader command.
        /// </summary>
        /// <param name="command">An instance of <see cref="DbCommand"/>.</param>
        /// <param name="interceptionContext">An instance of <see cref="DbCommandInterceptionContext"/>.</param>
        public void ReaderExecuted(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            if (interceptionContext.Exception != null)
            {
                SqlRequestUtil.ProcessException(interceptionContext.Exception);
            }
            
            SqlRequestUtil.EndSubsegment();
        }

        /// <summary>
        /// Trace before executing scalar command.
        /// </summary>
        /// <param name="command">An instance of <see cref="DbCommand"/>.</param>
        /// <param name="interceptionContext">An instance of <see cref="DbCommandInterceptionContext"/>.</param>
        public void ScalarExecuting(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            SqlRequestUtil.BeginSubsegment(command);
            SqlRequestUtil.ProcessCommand(command);
        }

        /// <summary>
        /// Trace after executing scalar command.
        /// </summary>
        /// <param name="command">An instance of <see cref="DbCommand"/>.</param>
        /// <param name="interceptionContext">An instance of <see cref="DbCommandInterceptionContext"/>.</param>
        public void ScalarExecuted(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            if (interceptionContext.Exception != null)
            {
                SqlRequestUtil.ProcessException(interceptionContext.Exception);
            }

            SqlRequestUtil.EndSubsegment();
        }
    }
}
#endif
