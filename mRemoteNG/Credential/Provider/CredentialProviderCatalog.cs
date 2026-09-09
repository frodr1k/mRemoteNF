using System.Collections.Generic;
using mRemoteNG.Connection;

namespace mRemoteNG.Credential.Provider
{
    /// <summary>
    /// Resolves <see cref="ICredentialProvider"/> implementations by their
    /// <see cref="ExternalCredentialProvider"/> type.
    /// </summary>
    public interface ICredentialProviderCatalog
    {
        bool TryGetProvider(ExternalCredentialProvider providerType, out ICredentialProvider provider);
    }

    /// <summary>
    /// Default registry of the built-in external credential providers.
    /// </summary>
    public sealed class CredentialProviderCatalog : ICredentialProviderCatalog
    {
        private readonly Dictionary<ExternalCredentialProvider, ICredentialProvider> _providers = new();

        public CredentialProviderCatalog()
        {
            Register(new DelineaSecretServerCredentialProvider());
            Register(new PasswordstateCredentialProvider());
            Register(new OnePasswordCredentialProvider());
            Register(new VaultOpenbaoCredentialProvider());
        }

        public void Register(ICredentialProvider provider)
        {
            if (provider == null) return;
            _providers[provider.ProviderType] = provider;
        }

        public bool TryGetProvider(ExternalCredentialProvider providerType, out ICredentialProvider provider)
        {
            return _providers.TryGetValue(providerType, out provider);
        }

        /// <summary>Shared default catalog instance.</summary>
        public static CredentialProviderCatalog Default { get; } = new();
    }
}
