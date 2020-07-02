//-----------------------------------------------------------------------------
// <copyright file="IDiagnosticListener.cs" company="Amazon.com">
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

using System;
using System.Collections.Generic;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Base class of diagnostic listener for processing events
    /// </summary>
    public abstract class DiagnosticListenerBase : IObserver<KeyValuePair<string, object>>
    {
        internal abstract string Name { get; }

        public void OnNext(KeyValuePair<string, object> value)
        {
            OnEvent(value);
        }

        protected abstract void OnEvent(KeyValuePair<string, object> value);

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }
}