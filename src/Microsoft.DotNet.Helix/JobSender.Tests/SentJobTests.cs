// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Client.Models;
using Moq;
using Xunit;

namespace Microsoft.DotNet.Helix.JobSender.Test
{
    public class SentJobTests
    {
        [Fact]
        public async Task WaitAsync_WhenCancelledWithHelixToken_CancelsHelixJob()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var jobApi = new Mock<IJob>(MockBehavior.Strict);
            const string correlationId = "test-job-id";
            const string helixCancellationToken = "helix-cancel-token";

            var newJob = new JobCreationResult(correlationId, "summary-url", "results-uri", "results-uri-rsas")
            {
                CancellationToken = helixCancellationToken
            };
            var sentJob = new SentJob(jobApi.Object, newJob);

            cts.Cancel();

            // WaitForJobAsync throws OperationCanceledException when cancellation is requested
            jobApi
                .Setup(j => j.WaitForJobAsync(correlationId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException("Cancelled", cts.Token));

            // CancelAsync should be called with the helix cancellation token
            jobApi
                .Setup(j => j.CancelAsync(correlationId, helixCancellationToken, default))
                .Returns(Task.CompletedTask);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => sentJob.WaitAsync(cancellationToken: cts.Token));

            jobApi.Verify(j => j.CancelAsync(correlationId, helixCancellationToken, default), Times.Once);
        }

        [Fact]
        public async Task WaitAsync_WhenCancelledWithNoHelixToken_DoesNotCallCancelAsync()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var jobApi = new Mock<IJob>(MockBehavior.Strict);
            const string correlationId = "test-job-id";

            // No CancellationToken set on the job result
            var newJob = new JobCreationResult(correlationId, "summary-url", "results-uri", "results-uri-rsas");
            var sentJob = new SentJob(jobApi.Object, newJob);

            cts.Cancel();

            // WaitForJobAsync throws OperationCanceledException when cancellation is requested
            jobApi
                .Setup(j => j.WaitForJobAsync(correlationId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException("Cancelled", cts.Token));

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => sentJob.WaitAsync(cancellationToken: cts.Token));

            // CancelAsync should NOT be called since there is no Helix cancellation token
            jobApi.Verify(j => j.CancelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task WaitAsync_WhenCancelledAndCancelAsyncFails_StillThrowsOriginalException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var jobApi = new Mock<IJob>(MockBehavior.Strict);
            const string correlationId = "test-job-id";
            const string helixCancellationToken = "helix-cancel-token";

            var newJob = new JobCreationResult(correlationId, "summary-url", "results-uri", "results-uri-rsas")
            {
                CancellationToken = helixCancellationToken
            };
            var sentJob = new SentJob(jobApi.Object, newJob);

            cts.Cancel();

            jobApi
                .Setup(j => j.WaitForJobAsync(correlationId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException("Cancelled", cts.Token));

            // CancelAsync throws an exception (e.g., network error)
            jobApi
                .Setup(j => j.CancelAsync(correlationId, helixCancellationToken, default))
                .ThrowsAsync(new InvalidOperationException("Cancel failed"));

            // Act & Assert - should still throw the original OperationCanceledException
            await Assert.ThrowsAsync<OperationCanceledException>(() => sentJob.WaitAsync(cancellationToken: cts.Token));
        }

        [Fact]
        public async Task WaitAsync_WhenNotCancelled_ReturnsJobPassFail()
        {
            // Arrange
            var jobApi = new Mock<IJob>(MockBehavior.Strict);
            const string correlationId = "test-job-id";

            var newJob = new JobCreationResult(correlationId, "summary-url", "results-uri", "results-uri-rsas")
            {
                CancellationToken = "helix-cancel-token"
            };
            var sentJob = new SentJob(jobApi.Object, newJob);

            var expectedResult = new JobPassFail(1, 0, ImmutableList<string>.Empty);
            jobApi
                .Setup(j => j.WaitForJobAsync(correlationId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await sentJob.WaitAsync();

            // Assert
            Assert.Same(expectedResult, result);
            jobApi.Verify(j => j.CancelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
