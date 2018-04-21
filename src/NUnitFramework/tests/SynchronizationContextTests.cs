// ***********************************************************************
// Copyright (c) 2018 Charlie Poole, Rob Prouse
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework.Internal;
using NUnit.TestData;

#if ASYNC
using System.Threading.Tasks;
#endif

namespace NUnit.Framework
{
    public static class SynchronizationContextTests
    {
#if ASYNC
        public static IEnumerable<AsyncExecutionApiAdapter> ApiAdapters => AsyncExecutionApiAdapter.All;

#if NET40 || NET45
        // TODO: test a custom awaitable type whose awaiter executes continuations on a brand new thread
        // to ensure that the message pump is shut down on the correct thread.

        public static readonly IEnumerable<Type> KnownSynchronizationContextTypes = new[]
        {
            typeof(System.Windows.Forms.WindowsFormsSynchronizationContext),
            typeof(System.Windows.Threading.DispatcherSynchronizationContext)
        };

        private static SynchronizationContext CreateSynchronizationContext(Type knownSynchronizationContextType)
        {
            if (new PlatformHelper().IsPlatformSupported("Mono"))
            {
                if (knownSynchronizationContextType == typeof(System.Windows.Threading.DispatcherSynchronizationContext))
                {
                    Assert.Ignore("DispatcherSynchronizationContext throws NotImplementedException on Mono.");
                }
                else if (knownSynchronizationContextType == typeof(System.Windows.Forms.WindowsFormsSynchronizationContext))
                {
                    if (!Environment.UserInteractive)
                    {
                        Assert.Inconclusive("WindowsFormsSynchronizationContext throws ArgumentNullException on Mono when not running interactively.");
                    }
                }
            }

            return (SynchronizationContext)Activator.CreateInstance(knownSynchronizationContextType);
        }

        [Test, Timeout(10000)]
        public static void ContinuationDoesNotDeadlockOnKnownSynchronizationContext(
            [ValueSource(nameof(KnownSynchronizationContextTypes))] Type knownSynchronizationContextType,
            [ValueSource(nameof(ApiAdapters))] AsyncExecutionApiAdapter apiAdapter)
        {
            var createdOnThisThread = CreateSynchronizationContext(knownSynchronizationContextType);

            using (TemporarySynchronizationContext(createdOnThisThread))
            {
                apiAdapter.Execute(async () =>
                {
#if NET40
                    await TaskEx.Yield();
#else
                    await Task.Yield();
#endif
                });
            }
        }

        [Test]
        public static void AsyncDelegatesAreExecutedUnderTheCurrentSynchronizationContext(
            [ValueSource(nameof(KnownSynchronizationContextTypes))] Type knownSynchronizationContextType,
            [ValueSource(nameof(ApiAdapters))] AsyncExecutionApiAdapter apiAdapter)
        {
            var createdOnThisThread = CreateSynchronizationContext(knownSynchronizationContextType);

            using (TemporarySynchronizationContext(createdOnThisThread))
            {
                apiAdapter.Execute(() =>
                {
                    Assert.That(SynchronizationContext.Current, Is.SameAs(createdOnThisThread));
#if NET40
                    return TaskEx.FromResult<object>(null);
#else
                    return Task.FromResult<object>(null);
#endif
                });
            }
        }

        [Test, Timeout(10000)]
        public static void AwaitingContinuationDoesNotAlterSynchronizationContext(
            [ValueSource(nameof(KnownSynchronizationContextTypes))] Type knownSynchronizationContextType,
            [ValueSource(nameof(ApiAdapters))] AsyncExecutionApiAdapter apiAdapter)
        {
            var createdOnThisThread = CreateSynchronizationContext(knownSynchronizationContextType);

            using (TemporarySynchronizationContext(createdOnThisThread))
            {
                apiAdapter.Execute(async () =>
                {
#if NET40
                    await TaskEx.Yield();
#else
                    await Task.Yield();
#endif
                });

                Assert.That(SynchronizationContext.Current, Is.SameAs(createdOnThisThread));
            }
        }
#endif
#endif

        private static IDisposable TemporarySynchronizationContext(SynchronizationContext synchronizationContext)
        {
            var originalContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            return On.Dispose(() => SynchronizationContext.SetSynchronizationContext(originalContext));
        }
    }
}
