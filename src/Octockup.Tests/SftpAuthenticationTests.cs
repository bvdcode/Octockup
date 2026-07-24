// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using Octockup.Server.Modules;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Octockup.Tests
{
    public class SftpAuthenticationTests
    {
        [Test]
        public void CreateSftpClient_UsesPasswordAuthentication_ForOrdinaryCredential()
        {
            const string password = "ordinary-password";

            using SftpClient client = SFTPBackupStorage.CreateSftpClient(
                "localhost",
                22,
                "octockup",
                password,
                out PrivateKeyFile? ownedPrivateKey
            );

            Assert.Multiple(() =>
            {
                Assert.That(
                    client.ConnectionInfo.AuthenticationMethods.Single(),
                    Is.TypeOf<PasswordAuthenticationMethod>()
                );
                Assert.That(ownedPrivateKey, Is.Null);
                Assert.That(
                    client.ConnectionInfo.Timeout,
                    Is.EqualTo(TimeSpan.FromSeconds(30))
                );
            });
        }

        [Test]
        public void CreateSftpClient_UsesPrivateKeyAuthentication_ForPemCredential()
        {
            using RSA rsa = RSA.Create(2048);
            string privateKey = $"\r\n{rsa.ExportPkcs8PrivateKeyPem()}\r\n";
            SftpClient? client = null;
            PrivateKeyFile? ownedPrivateKey = null;

            try
            {
                client = SFTPBackupStorage.CreateSftpClient(
                    "localhost",
                    22,
                    "octockup",
                    privateKey,
                    out ownedPrivateKey
                );

                PrivateKeyAuthenticationMethod authenticationMethod = client
                    .ConnectionInfo
                    .AuthenticationMethods
                    .OfType<PrivateKeyAuthenticationMethod>()
                    .Single();

                Assert.Multiple(() =>
                {
                    Assert.That(authenticationMethod.KeyFiles, Has.Count.EqualTo(1));
                    Assert.That(authenticationMethod.KeyFiles.Single(), Is.SameAs(ownedPrivateKey));
                    Assert.That(
                        client.ConnectionInfo.AuthenticationMethods
                            .OfType<PasswordAuthenticationMethod>(),
                        Is.Empty
                    );
                });
            }
            finally
            {
                client?.Dispose();
                ownedPrivateKey?.Dispose();
            }
        }

        [Test]
        public void CreateSftpClient_RejectsMalformedPrivateKey_InsteadOfUsingItAsPassword()
        {
            const string malformedPrivateKey = """
                -----BEGIN OPENSSH PRIVATE KEY-----
                definitely-not-a-private-key
                -----END OPENSSH PRIVATE KEY-----
                """;

            Assert.That(
                () => SFTPBackupStorage.CreateSftpClient(
                    "localhost",
                    22,
                    "octockup",
                    malformedPrivateKey,
                    out _
                ),
                Throws.TypeOf<SshException>()
            );
        }
    }
}
