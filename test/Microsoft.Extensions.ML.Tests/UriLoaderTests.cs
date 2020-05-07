﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.ML;
using Microsoft.ML.TestFramework;
using Microsoft.ML.TestFrameworkCommon;
using Microsoft.ML.TestFrameworkCommon.Attributes;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Extensions.ML
{
    public class UriLoaderTests : BaseTestClass
    {
        public UriLoaderTests(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [IterationData(iterations: 20)]
        [Trait("Category", "RunSpecificTest")]
        public void Completesthrow_until_started(int iterations)
        {
            Output.WriteLine($"{iterations} - th");

            int timeout = 20 * 60 * 1000;

            var runTask = Task.Run(throw_until_started);
            var timeoutTask = Task.Delay(timeout + iterations);
            var finishedTask = Task.WhenAny(timeoutTask, runTask).Result;
            if (finishedTask == timeoutTask)
            {
                Console.WriteLine("throw_until_started test Hanging: fail to complete in 20 minutes");
                Environment.FailFast("Fail here to take memory dump");
            }
        }

        [Fact]
        public void throw_until_started()
        {
            var services = new ServiceCollection()
                .AddOptions()
                .AddLogging();
            var sp = services.BuildServiceProvider();

            var loaderUnderTest = ActivatorUtilities.CreateInstance<UriModelLoader>(sp);
            Assert.Throws<InvalidOperationException>(() => loaderUnderTest.GetModel());
            Assert.Throws<InvalidOperationException>(() => loaderUnderTest.GetReloadToken());
        }

        [Fact]
        public void can_reload_model()
        {
            var services = new ServiceCollection()
                .AddOptions()
                .AddLogging();
            var sp = services.BuildServiceProvider();

            var loaderUnderTest = ActivatorUtilities.CreateInstance<UriLoaderMock>(sp);
            loaderUnderTest.Start(new Uri("http://microsoft.com"), TimeSpan.FromMilliseconds(1));

            using AutoResetEvent changed = new AutoResetEvent(false);
            using IDisposable changeTokenRegistration = ChangeToken.OnChange(
                        () => loaderUnderTest.GetReloadToken(),
                        () => changed.Set());
            
            Assert.True(changed.WaitOne(AsyncTestHelper.UnexpectedTimeout), "UriLoader ChangeToken didn't fire before the allotted time.");
        }

        [Fact]
        public void no_reload_no_change()
        {
            var services = new ServiceCollection()
                .AddOptions()
                .AddLogging();
            var sp = services.BuildServiceProvider();

            var loaderUnderTest = ActivatorUtilities.CreateInstance<UriLoaderMock>(sp);

            loaderUnderTest.ETagMatches = (a,b) => true;

            loaderUnderTest.Start(new Uri("http://microsoft.com"), TimeSpan.FromMilliseconds(1));

            using AutoResetEvent changed = new AutoResetEvent(false);
            using IDisposable changeTokenRegistration = ChangeToken.OnChange(
                        () => loaderUnderTest.GetReloadToken(),
                        () => changed.Set());

            Assert.False(changed.WaitOne(100), "UriLoader ChangeToken fired but shouldn't have.");
        }
    }

    class UriLoaderMock : UriModelLoader
    {
        public Func<Uri, string, bool> ETagMatches { get; set; } = delegate { return false; };

        public UriLoaderMock(IOptions<MLOptions> contextOptions,
                         ILogger<UriModelLoader> logger) : base(contextOptions, logger)
        {
        }

        public override ITransformer GetModel()
        {
            return null;
        }

        internal override Task<bool> LoadModelAsync()
        {
            return Task.FromResult(true);
        }

        internal override Task<bool> MatchEtagAsync(Uri uri, string eTag)
        {
            return Task.FromResult(ETagMatches(uri, eTag));
        }
    }
}
