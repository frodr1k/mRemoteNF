using System;

namespace mRemoteNG.Credential.Provider
{
    /// <summary>
    /// Identifies the context in which credentials are being requested. Some
    /// external providers (e.g. HashiCorp Vault / OpenBao) behave differently
    /// depending on the target protocol, so the usage must be supplied.
    /// </summary>
    public enum CredentialProviderUsage
    {
        RdpConnection,
        RdpGateway,
        SshConnection
    }

    /// <summary>
    /// Carries the information an <see cref="ICredentialProvider"/> needs in order
    /// to resolve a credential just-in-time.
    /// </summary>
    public sealed class CredentialProviderRequest
    {
        public CredentialProviderUsage Usage { get; init; }

        /// <summary>
        /// The provider specific credential identifier (historically stored in the
        /// connection's <c>UserViaAPI</c> / <c>RDGatewayUserViaAPI</c> field).
        /// </summary>
        public string CredentialId { get; init; } = "";

        /// <summary>The username currently configured on the connection.</summary>
        public string Username { get; init; }

        /// <summary>The hostname of the target connection (used by some engines).</summary>
        public string Hostname { get; init; } = "";

        public Connection.VaultOpenbaoSecretEngine VaultSecretEngine { get; init; }
        public string VaultMount { get; init; } = "";
        public string VaultRole { get; init; } = "";
    }

    /// <summary>
    /// The resolved credential values. Providers only overwrite the fields they
    /// are able to supply; the caller seeds this object with the connection's
    /// existing values beforehand.
    /// </summary>
    public sealed class CredentialProviderResult
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Domain { get; set; } = "";
        public string PrivateKey { get; set; } = "";

        /// <summary>
        /// Releases the references to any fetched secret values once they have been
        /// consumed, so that a just-in-time credential is not retained by this
        /// transient object longer than necessary (Priority 3 - memory purging).
        /// </summary>
        public void Purge()
        {
            Password = "";
            PrivateKey = "";
            Domain = "";
            Username = "";
        }
    }

    /// <summary>
    /// Abstraction over an external credential source (password vault). Concrete
    /// implementations fetch secrets on demand so that credentials are only
    /// materialised when a session is opened (supports CVE-2023-30367 mitigation
    /// and just-in-time credential retrieval).
    /// </summary>
    public interface ICredentialProvider
    {
        /// <summary>The provider this implementation handles.</summary>
        Connection.ExternalCredentialProvider ProviderType { get; }

        /// <summary>
        /// Resolves the credential described by <paramref name="request"/> and
        /// writes the result into <paramref name="credentials"/>. Errors are
        /// reported through <paramref name="reportError"/> rather than thrown so
        /// that connection setup can continue gracefully, mirroring the previous
        /// inline behaviour.
        /// </summary>
        void Populate(CredentialProviderRequest request, CredentialProviderResult credentials, Action<string> reportError);
    }
}
