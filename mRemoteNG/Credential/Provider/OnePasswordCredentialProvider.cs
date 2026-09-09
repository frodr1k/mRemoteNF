using System;
using mRemoteNG.App;
using mRemoteNG.Connection;
using mRemoteNG.Messages;
using mRemoteNG.Resources.Language;

namespace mRemoteNG.Credential.Provider
{
    /// <summary>
    /// <see cref="ICredentialProvider"/> implementation backed by the 1Password
    /// command line interface.
    /// </summary>
    public sealed class OnePasswordCredentialProvider : ICredentialProvider
    {
        public ExternalCredentialProvider ProviderType => ExternalCredentialProvider.OnePassword;

        public void Populate(CredentialProviderRequest request, CredentialProviderResult credentials, Action<string> reportError)
        {
            try
            {
                ExternalConnectors.OP.OnePasswordCli.ReadPassword(
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
            catch (ExternalConnectors.OP.OnePasswordCliException ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, Language.ECPOnePasswordCommandLine + ": " + ex.Arguments);
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.ECPOnePasswordReadFailed + Environment.NewLine + ex.Message);
            }
        }
    }
}
