using mRemoteNG.Security.SymmetricEncryption;
using System;
using System.Runtime.Versioning;

namespace mRemoteNG.Security.Factories
{
    /// <summary>
    /// DEPRECATED - builds the insecure <see cref="LegacyRijndaelCryptographyProvider"/>.
    /// Retained only for backward-compatible decryption of legacy data; do not use
    /// for encrypting new secrets.
    /// </summary>
    [Obsolete("Insecure (MD5 key derivation, no integrity). Retained for legacy decryption only; use CryptoProviderFactoryFromSettings for new data.")]
    [SupportedOSPlatform("windows")]
    public class LegacyInsecureCryptoProviderFactory : ICryptoProviderFactory
    {
        public ICryptographyProvider Build()
        {
#pragma warning disable CS0618 // Legacy provider intentionally used here for backward compatibility.
            return new LegacyRijndaelCryptographyProvider();
#pragma warning restore CS0618
        }
    }
}