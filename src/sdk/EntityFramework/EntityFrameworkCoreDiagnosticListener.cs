//-----------------------------------------------------------------------------
// <copyright file="EntityFrameworkCoreDiagnosticListener.cs" company="Amazon.com">
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

#if !NET45
using Amazon.Runtime.Internal.Util;
using Amazon.XRay.Recorder.AutoInstrumentation.Utils;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Data.Common;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Diagnostic listener for processing EntityFramework Core request
    /// </summary>
    public class EntityFrameworkCoreDiagnosticListener : DiagnosticListenerBase
    {
        private static readonly Logger _logger = Logger.GetLogger(typeof(EntityFrameworkCoreDiagnosticListener));
        
        internal override string Name => "Microsoft.EntityFrameworkCore";

        protected override void OnEvent(KeyValuePair<string, object> value)
        {            

            try
            {
                switch (value.Key)
                {
                    case "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuting":
                        {
                            OnEventStart(value.Value);
                        }
                        break;
                    case "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted":
                        {
                            OnEventStop(value.Value);
                        }
                        break;
                    case "Microsoft.EntityFrameworkCore.Database.Command.CommandError":
                        {
                            OnEventException(value.Value);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Invalid diagnostic source key ({0})", value.Key);
            }
        }

        private void OnEventStart(object value)
        {
            var command = ((CommandEventData)value).Command;
            if (command is DbCommand dbCommand)
            {
                SqlRequestUtil.BeginSubsegment(dbCommand);
                SqlRequestUtil.ProcessCommand(dbCommand);
            }
        }

        private void OnEventStop(object value)
        {
            var command = ((CommandExecutedEventData)value).Command;
            if (command is DbCommand dbCommand)
            {
                SqlRequestUtil.EndSubsegment();
            }
        }

        private void OnEventException(object value)
        {
            var exc = ((CommandErrorEventData)value).Exception;
            if (exc is Exception exception)
            {
                SqlRequestUtil.ProcessException(exception);
                SqlRequestUtil.EndSubsegment();
            }
        }
    }
}
#endif
