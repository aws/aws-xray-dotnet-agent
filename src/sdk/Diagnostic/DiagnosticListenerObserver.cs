//-----------------------------------------------------------------------------
// <copyright file="DiagnosticListenerBase.cs" company="Amazon.com">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Amazon.XRay.Recorder.AutoInstrumentation
{
    /// <summary>
    /// Class for diagnostic observer
    /// </summary>
    public class DiagnosticListenerObserver : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly IList<DiagnosticListenerBase> _diagnosticListeners;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        public DiagnosticListenerObserver(IList<DiagnosticListenerBase> diagnosticListeners)
        {
            _diagnosticListeners = diagnosticListeners;
        }

        public void Dispose()
        {
            foreach (var sub in _subscriptions)
            {
                sub.Dispose();
            }
            _subscriptions.Clear();
        }

        /// <summary>
        /// Subscribe diagnostic lsitener as long as its name matches <see cref="DiagnosticListener"/>.
        /// </summary>
        public void OnNext(DiagnosticListener diagnosticListener)
        {
            foreach (var _diagnosticListener in _diagnosticListeners)
            {
                if (diagnosticListener.Name == _diagnosticListener?.Name)
                {
                    var subscription = diagnosticListener.Subscribe(_diagnosticListener);
                    _subscriptions.Add(subscription);
                    break;
                }
            }
        }

        public void OnCompleted()
        {
            Dispose();
        }

        public void OnError(Exception error)
        {
        }
    }
}
