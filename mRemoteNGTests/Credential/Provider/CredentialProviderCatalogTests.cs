using mRemoteNG.Connection;
using mRemoteNG.Credential.Provider;
using NUnit.Framework;

namespace mRemoteNGTests.Credential.Provider
{
    public class CredentialProviderCatalogTests
    {
        [TestCase(ExternalCredentialProvider.ClickstudiosPasswordState, typeof(PasswordstateCredentialProvider))]
        [TestCase(ExternalCredentialProvider.DelineaSecretServer, typeof(DelineaSecretServerCredentialProvider))]
        [TestCase(ExternalCredentialProvider.OnePassword, typeof(OnePasswordCredentialProvider))]
        [TestCase(ExternalCredentialProvider.VaultOpenbao, typeof(VaultOpenbaoCredentialProvider))]
        public void CatalogReturnsExpectedProviderForEachType(ExternalCredentialProvider providerType, System.Type expectedType)
        {
            CredentialProviderCatalog catalog = new();

            bool found = catalog.TryGetProvider(providerType, out ICredentialProvider provider);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True);
                Assert.That(provider, Is.TypeOf(expectedType));
                Assert.That(provider.ProviderType, Is.EqualTo(providerType));
            });
        }

        [Test]
        public void CatalogHasNoProviderForNone()
        {
            CredentialProviderCatalog catalog = new();

            bool found = catalog.TryGetProvider(ExternalCredentialProvider.None, out ICredentialProvider provider);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.False);
                Assert.That(provider, Is.Null);
            });
        }

        [Test]
        public void PurgeClearsResultSecrets()
        {
            CredentialProviderResult result = new()
            {
                Username = "user",
                Password = "secret",
                Domain = "dom",
                PrivateKey = "key"
            };

            result.Purge();

            Assert.Multiple(() =>
            {
                Assert.That(result.Password, Is.Empty);
                Assert.That(result.PrivateKey, Is.Empty);
                Assert.That(result.Domain, Is.Empty);
                Assert.That(result.Username, Is.Empty);
            });
        }
    }
}
