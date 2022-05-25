using Microsoft.DotNet.Helix.Client;
using Moq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.Helix.JobSender.Test
{
    public class ArchivePayloadTests
    {
        [Fact]
        public async Task ArchivePayloadUploads()
        {
            var archiveFile = Path.GetTempFileName();
            var blobContainer = new Mock<IBlobContainer>(MockBehavior.Strict);
            blobContainer
                .Setup(bc => bc.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new Uri("http://microsoft.com/blob")));
            var archivePayload = new ArchivePayload(archiveFile);

            await File.WriteAllBytesAsync(archiveFile, new byte[] { 1, 2, 3 });
            var uri = await archivePayload.UploadAsync(blobContainer.Object, (s) => { }, default);

            Assert.Equal("http://microsoft.com/blob", uri);
        }

        [Fact]
        public void LongRunningTest()
        {
            Func<long, bool> isPrime = x =>
            {
                var result = true;
                for (var i = 2L; i < long.MaxValue; i++)
                {
                    if (x % i == 0)
                    {
                        result = false;
                    }
                }

                return result;
            };

            Assert.True(isPrime(7));
        }
    }
}
