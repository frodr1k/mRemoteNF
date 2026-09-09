using System;
using mRemoteNG.Connection;

namespace mRemoteNG.Credential.Provider
{
    /// <summary>
    /// <see cref="ICredentialProvider"/> implementation backed by Click Studios
    /// Passwordstate. Secrets are fetched just-in-time when a session is opened.
    /// </summary>
    public sealed class PasswordstateCredentialProvider : ICredentialProvider
    {
        public ExternalCredentialProvider ProviderType => ExternalCredentialProvider.ClickstudiosPasswordState;

        public void Populate(CredentialProviderRequest request, CredentialProviderResult credentials, Action<string> reportError)
        {
            try
            {
                ExternalConnectors.CPS.PasswordstateInterface.FetchSecretFromServer(
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
                reportError?.Invoke("Passwordstate Interface Error: " + ex.Message);
            }
        }
    }
}
