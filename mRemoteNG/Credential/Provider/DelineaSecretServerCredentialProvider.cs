using System;
using mRemoteNG.Connection;

namespace mRemoteNG.Credential.Provider
{
    /// <summary>
    /// <see cref="ICredentialProvider"/> implementation backed by Delinea Secret
    /// Server.
    /// </summary>
    public sealed class DelineaSecretServerCredentialProvider : ICredentialProvider
    {
        public ExternalCredentialProvider ProviderType => ExternalCredentialProvider.DelineaSecretServer;

        public void Populate(CredentialProviderRequest request, CredentialProviderResult credentials, Action<string> reportError)
        {
            try
            {
                ExternalConnectors.DSS.SecretServerInterface.FetchSecretFromServer(
                    $"{request.CredentialId}",
                    out string username,
                    out string password,
                    out string domain,
                    out string privateKey);

                credentials.Username = username;
                credentials.Password = password;
                credentials.Domain = domain;
                credentials.PrivateKey = privateKey;
            }
            catch (Exception ex)
            {
                reportError?.Invoke("Secret Server Interface Error: " + ex.Message);
            }
        }
    }
}
