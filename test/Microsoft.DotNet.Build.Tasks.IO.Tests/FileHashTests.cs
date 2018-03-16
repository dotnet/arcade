// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.IO.Tests
{
    public class FileHashTests
    {
        [Theory]
        [InlineData("SHA256", "BCFAF334240356E1B97824A866F643B1ADA3C16AA0B5B2BFA8390D8BB54A244C")]
        [InlineData("sha256", "BCFAF334240356E1B97824A866F643B1ADA3C16AA0B5B2BFA8390D8BB54A244C")]
        [InlineData("SHA384", "5520B01FDE8A8A7EA38DCADFBF3CFAB2818FA0D5A8A16CB11A2FC7F5C9F1497F7B3C528FDB8CE10AA293A4E5FF32297F")]
        [InlineData("SHA512", "7774962C97EAC52B45291E1410F06AC6EFF6AF9ED38A57E2CEB720650282E46CFE512FAAD68AD9C45B74ED1B7E460198E0B00D5C9EF0404FF76B12F8AD2D329F")]
        public void ComputesFileChecksum(string algoritm, string hash)
        {
            var task = new GetFileHash
            {
                Files = new[] { new TaskItem(Path.Combine(AppContext.BaseDirectory, "TestResources", "lorem.bin")) },
                BuildEngine = new MockEngine(),
                Algorithm = algoritm,
            };
            Assert.True(task.Execute(), "Task should pass");
            Assert.Equal(hash, task.Hash);
        }

        [Theory]
        [InlineData("SHA256", "BCFAF334240356E1B97824A866F643B1ADA3C16AA0B5B2BFA8390D8BB54A244C")]
        [InlineData("SHA384", "5520B01FDE8A8A7EA38DCADFBF3CFAB2818FA0D5A8A16CB11A2FC7F5C9F1497F7B3C528FDB8CE10AA293A4E5FF32297F")]
        [InlineData("SHA512", "7774962C97EAC52B45291E1410F06AC6EFF6AF9ED38A57E2CEB720650282E46CFE512FAAD68AD9C45B74ED1B7E460198E0B00D5C9EF0404FF76B12F8AD2D329F")]
        public void ComputesFileChecksumInGroup(string algoritm, string hash)
        {
            var task = new GetFileHash
            {
                Files = new[]
                {
                    new TaskItem(Path.Combine(AppContext.BaseDirectory, "TestResources", "lorem.bin")),
                    new TaskItem(Path.Combine(AppContext.BaseDirectory, "TestResources", "lorem.bin")),
                },
                BuildEngine = new MockEngine(),
                Algorithm = algoritm,
            };

            Assert.True(task.Execute(), "Task should pass");
            Assert.Equal(2, task.Items.Length);
            Assert.All(task.Items, i => Assert.Equal(hash, i.GetMetadata("FileHash")));
        }

        [Theory]
        [InlineData("SHA256", "C442A45BB8D0938AFB2B5B0AA61C3ADA1B346F668A42879B1E653042433FAFCB")]
        [InlineData("SHA384", "F79223FF5E4A392AA01EC8BDF825C3B7F7941F9C5F7CF2A11BC61A8A5D0AF8182BAFC3FBFDACD83AE7A8A8EDF10B0255")]
        [InlineData("SHA512", "F923D2DA8F21B67FF4040FE9C5D00B0E891064E7B1DE47B54C9DA86DAAF215EFC64E282056027BEC2E75A83DE9FA6FFE6CA60F0141E19254B25CAE79C2694777")]
        public void VerifyFileChecksum_Fails(string algoritm, string hash)
        {
            var task = new VerifyFileHash
            {
                File = Path.Combine(AppContext.BaseDirectory, "TestResources", "lorem.bin"),
                BuildEngine = new MockEngine { ContinueOnError = true },
                Algorithm = algoritm,
                Hash = hash,
            };

            Assert.False(task.Execute(), "Task should fail");
        }

        [Theory]
        [InlineData("SHA256", "BCFAF334240356E1B97824A866F643B1ADA3C16AA0B5B2BFA8390D8BB54A244C")]
        [InlineData("sha256", "bcfaf334240356e1b97824a866f643b1ada3c16aa0b5b2bfa8390d8bb54a244c")]
        [InlineData("SHA384", "5520B01FDE8A8A7EA38DCADFBF3CFAB2818FA0D5A8A16CB11A2FC7F5C9F1497F7B3C528FDB8CE10AA293A4E5FF32297F")]
        [InlineData("SHA512", "7774962C97EAC52B45291E1410F06AC6EFF6AF9ED38A57E2CEB720650282E46CFE512FAAD68AD9C45B74ED1B7E460198E0B00D5C9EF0404FF76B12F8AD2D329F")]
        public void VerifyFileChecksum_Pass(string algoritm, string hash)
        {
            var task = new VerifyFileHash
            {
                File = Path.Combine(AppContext.BaseDirectory, "TestResources", "lorem.bin"),
                BuildEngine = new MockEngine(),
                Algorithm = algoritm,
                Hash = hash,
            };

            Assert.True(task.Execute(), "Task should pass");
        }
    }
}
