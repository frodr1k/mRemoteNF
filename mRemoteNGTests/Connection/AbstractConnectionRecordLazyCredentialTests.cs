using mRemoteNG.Connection;
using mRemoteNG.Security;
using NUnit.Framework;

namespace mRemoteNGTests.Connection
{
    public class AbstractConnectionRecordLazyCredentialTests
    {
        private class CountingDecryptor : IConnectionCredentialDecryptor
        {
            public int CallCount { get; private set; }

            public string Decrypt(string cipherText)
            {
                CallCount++;
                // Simple reversible transform standing in for real decryption.
                return "plain:" + cipherText;
            }
        }

        [Test]
        public void LoadingEncryptedCredentialDoesNotDecryptImmediately()
        {
            CountingDecryptor decryptor = new();
            ConnectionInfo connectionInfo = new();

            connectionInfo.LoadEncryptedCredential(
                AbstractConnectionRecord.EncryptedCredential.Password, "CIPHER", decryptor);

            // CVE-2023-30367: nothing should be decrypted at load time.
            Assert.That(decryptor.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void PasswordIsDecryptedOnFirstAccess()
        {
            CountingDecryptor decryptor = new();
            ConnectionInfo connectionInfo = new();
            connectionInfo.LoadEncryptedCredential(
                AbstractConnectionRecord.EncryptedCredential.Password, "CIPHER", decryptor);

            string value = connectionInfo.Password;

            Assert.Multiple(() =>
            {
                Assert.That(value, Is.EqualTo("plain:CIPHER"));
                Assert.That(decryptor.CallCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void RepeatedAccessDecryptsOnlyOnce()
        {
            CountingDecryptor decryptor = new();
            ConnectionInfo connectionInfo = new();
            connectionInfo.LoadEncryptedCredential(
                AbstractConnectionRecord.EncryptedCredential.Password, "CIPHER", decryptor);

            _ = connectionInfo.Password;
            _ = connectionInfo.Password;
            _ = connectionInfo.Password;

            Assert.That(decryptor.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void SettingPasswordClearsPendingDecryption()
        {
            CountingDecryptor decryptor = new();
            ConnectionInfo connectionInfo = new();
            connectionInfo.LoadEncryptedCredential(
                AbstractConnectionRecord.EncryptedCredential.Password, "CIPHER", decryptor);

            connectionInfo.Password = "newPlaintext";
            string value = connectionInfo.Password;

            Assert.Multiple(() =>
            {
                Assert.That(value, Is.EqualTo("newPlaintext"));
                Assert.That(decryptor.CallCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void EmptyCipherTextIsNotMarkedEncrypted()
        {
            CountingDecryptor decryptor = new();
            ConnectionInfo connectionInfo = new();
            connectionInfo.LoadEncryptedCredential(
                AbstractConnectionRecord.EncryptedCredential.Password, "", decryptor);

            string value = connectionInfo.Password;

            Assert.Multiple(() =>
            {
                Assert.That(value, Is.EqualTo(""));
                Assert.That(decryptor.CallCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void AllEncryptedCredentialFieldsDeferDecryption()
        {
            CountingDecryptor decryptor = new();
            ConnectionInfo connectionInfo = new();

            connectionInfo.LoadEncryptedCredential(AbstractConnectionRecord.EncryptedCredential.Password, "p", decryptor);
            connectionInfo.LoadEncryptedCredential(AbstractConnectionRecord.EncryptedCredential.RDGatewayPassword, "g", decryptor);
            connectionInfo.LoadEncryptedCredential(AbstractConnectionRecord.EncryptedCredential.RDGatewayAccessToken, "t", decryptor);
            connectionInfo.LoadEncryptedCredential(AbstractConnectionRecord.EncryptedCredential.VNCProxyPassword, "v", decryptor);

            Assert.That(decryptor.CallCount, Is.EqualTo(0), "No credential should be decrypted before it is accessed.");

            Assert.Multiple(() =>
            {
                Assert.That(connectionInfo.Password, Is.EqualTo("plain:p"));
                Assert.That(connectionInfo.RDGatewayPassword, Is.EqualTo("plain:g"));
                Assert.That(connectionInfo.RDGatewayAccessToken, Is.EqualTo("plain:t"));
                Assert.That(connectionInfo.VNCProxyPassword, Is.EqualTo("plain:v"));
            });
        }
    }
}
