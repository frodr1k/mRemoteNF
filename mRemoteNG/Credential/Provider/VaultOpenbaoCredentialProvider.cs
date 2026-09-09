using System;
using mRemoteNG.Connection;

namespace mRemoteNG.Credential.Provider
{
    /// <summary>
    /// <see cref="ICredentialProvider"/> implementation backed by HashiCorp Vault
    /// / OpenBao. The retrieval logic differs by protocol and secret engine.
    /// </summary>
    public sealed class VaultOpenbaoCredentialProvider : ICredentialProvider
    {
        public ExternalCredentialProvider ProviderType => ExternalCredentialProvider.VaultOpenbao;

        public void Populate(CredentialProviderRequest request, CredentialProviderResult credentials, Action<string> reportError)
        {
            try
            {
                switch (request.Usage)
                {
                    case CredentialProviderUsage.SshConnection:
                        PopulateSsh(request, credentials);
                        break;
                    default: // RdpConnection / RdpGateway
                        PopulateRdp(request, credentials);
                        break;
                }
            }
            catch (ExternalConnectors.VO.VaultOpenbaoException ex)
            {
                reportError?.Invoke("Secret Server Interface Error: " + ex.Message);
            }
        }

        private static void PopulateRdp(CredentialProviderRequest request, CredentialProviderResult credentials)
        {
            string username = request.Username ?? "";
            ExternalConnectors.VO.VaultOpenbao.ReadPasswordRDP(
                (int)request.VaultSecretEngine,
                request.VaultMount,
                request.VaultRole,
                ref username,
                out string password);

            credentials.Username = username;
            credentials.Password = password;
        }

        private static void PopulateSsh(CredentialProviderRequest request, CredentialProviderResult credentials)
        {
            string password;
            if (request.VaultSecretEngine == VaultOpenbaoSecretEngine.SSHOTP)
            {
                ExternalConnectors.VO.VaultOpenbao.ReadOtpSSH(
                    request.VaultMount,
                    request.VaultRole,
                    request.Username ?? "",
                    request.Hostname,
                    out password);
            }
            else
            {
                ExternalConnectors.VO.VaultOpenbao.ReadPasswordSSH(
                    (int)request.VaultSecretEngine,
                    request.VaultMount,
                    request.VaultRole,
                    request.Username ?? "root",
                    out password);
            }

            credentials.Password = password;
        }
    }
}
