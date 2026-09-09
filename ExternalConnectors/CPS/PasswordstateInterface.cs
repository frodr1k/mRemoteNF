using Microsoft.Win32;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExternalConnectors.CPS;

public class PasswordstateInterface
{
    private static class CPSConnectionData
    {
        public static string ssUsername = "";
        public static string ssPassword = "";
        public static string ssUrl = "";
        public static string ssOTP = "";
        public static DateTime ssOTPTimeStampExpiration;

        public static bool ssSSO = false;
        public static bool initdone = false;

        // Session-only: when true the user has explicitly acknowledged that any
        // TLS certificate presented by the Passwordstate server should be
        // accepted (expired, self-signed, name mismatch, ...). Never persisted
        // to disk; it must be re-confirmed every time the application starts.
        public static bool ssTrustInvalidCert = false;

        //token 
        //public static string ssTokenBearer = "";
        //public static DateTime ssTokenExpiresOn = DateTime.UtcNow;
        //public static string ssTokenRefresh = "";

        public static void Init()
        {
            // 2024-05-04 passwordstate currently does not support auth tokens, so we need to re-enter otp codes frequently
            if (!string.IsNullOrEmpty(ssOTP) && DateTime.Now > ssOTPTimeStampExpiration)
            {
                ssOTP = "";
                initdone = false;
            }

            if (initdone == true)
                return;

            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\mRemoteNF_CPSInterface");
            try
            {
                // display gui and ask for data
                CPSConnectionForm f = new CPSConnectionForm();
                //string? un = key.GetValue("Username") as string;
                //f.tbUsername.Text = un ?? "";
                f.tbAPIKey.Text = CPSConnectionData.ssPassword;    // in OTP refresh cases, this value might already be filled

                string? url = key.GetValue("URL") as string;
                if (url == null || !url.Contains("://"))
                    url = "https://cred.domain.local/SecretServer";
                f.tbServerURL.Text = url;

                var b = key.GetValue("SSO");
                if (b == null || (string)b != "True")
                    ssSSO = false;
                else
                    ssSSO = true;
                f.cbUseSSO.Checked = ssSSO;
                f.cbTrustInvalidCert.Checked = ssTrustInvalidCert;
                
                // show dialog
                while (true)
                {
                    _ = f.ShowDialog();

                    if (f.DialogResult != DialogResult.OK)
                        return;

                    // store values to memory
                    //ssUsername = f.tbUsername.Text;
                    ssPassword = f.tbAPIKey.Text;
                    ssUrl = f.tbServerURL.Text;
                    ssSSO = f.cbUseSSO.Checked;
                    ssOTP = f.tbOTP.Text;
                    ssTrustInvalidCert = f.cbTrustInvalidCert.Checked;
                    ssOTPTimeStampExpiration = DateTime.Now.AddSeconds(30);

                    // Enforce transport security. When the endpoint is plain HTTP the
                    // API key and retrieved secrets would travel unencrypted.
                    if (ssUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        if (IsHttpsRequiredByPolicy())
                        {
                            // An administrator has enforced HTTPS-only via group policy;
                            // block plain HTTP and re-prompt for a valid https:// URL.
                            MessageBox.Show(
                                "Plain HTTP connections to Passwordstate are blocked by your " +
                                "organization's policy. Enter an https:// URL to continue.",
                                "HTTP blocked by policy",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            continue;
                        }

                        // Not enforced by policy: warn but allow.
                        MessageBox.Show(
                            "The Passwordstate URL uses plain HTTP. Your API key and any " +
                            "retrieved secrets will be sent unencrypted over the network.\r\n\r\n" +
                            "Use an https:// URL whenever possible.",
                            "Insecure connection (HTTP)",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    // check connection first
                    try
                    {
                        if (TestCredentials() == true)
                        {
                            initdone = true;
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Test Credentials failed - please check your credentials");
                    }
                }


                // write values to registry
                //key.SetValue("Username", ssUsername);
                key.SetValue("URL", ssUrl);
                key.SetValue("SSO", ssSSO);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                key.Close();
            }
        }

        /// <summary>
        /// Returns true when an administrator has enforced HTTPS-only connections
        /// to Passwordstate through group policy. The value is read from the
        /// managed policy hive that Group Policy writes to and that standard
        /// users cannot modify:
        ///   SOFTWARE\Policies\mRemoteNG\Passwordstate\RequireHttps (DWORD)
        /// The machine policy (HKLM) takes precedence; when it is absent the
        /// per-user policy (HKCU) is consulted. A value other than 0 blocks
        /// plain HTTP and forces the use of an https:// URL.
        /// </summary>
        private static bool IsHttpsRequiredByPolicy()
        {
            const string policyPath = @"SOFTWARE\Policies\mRemoteNF\Passwordstate";
            const string valueName = "RequireHttps";

            foreach (RegistryKey root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                using RegistryKey? policyKey = root.OpenSubKey(policyPath);
                if (policyKey?.GetValue(valueName) is int dword)
                    return dword != 0;
            }

            return false;
        }
    }

    private static bool TestCredentials()
    {
        return ConnectionTest();
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> for the Passwordstate API. When the
    /// user has opted in for this session, an expired or otherwise untrusted
    /// certificate chain is tolerated; otherwise the platform's default (strict)
    /// TLS validation applies. A hostname mismatch is ALWAYS rejected - that is
    /// the exact condition a man-in-the-middle needs, so the opt-in must never
    /// weaken it. The certificate bypass is scoped to this handler only - it
    /// does not affect TLS validation anywhere else in the application.
    /// </summary>
    private static HttpClient CreateClient(bool useDefaultCredentials)
    {
        var handler = new HttpClientHandler();
        if (useDefaultCredentials)
            handler.UseDefaultCredentials = true;
        if (CPSConnectionData.ssTrustInvalidCert)
            handler.ServerCertificateCustomValidationCallback = TrustAllExceptNameMismatch;
        return new HttpClient(handler);
    }

    /// <summary>
    /// Certificate validation callback used when the user has opted in to trust
    /// an invalid TLS certificate. It tolerates chain/time errors (expired,
    /// self-signed) but never a hostname mismatch, which would indicate a
    /// man-in-the-middle serving a certificate for a different host.
    /// </summary>
    private static bool TrustAllExceptNameMismatch(
        HttpRequestMessage request, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
            return true;

        // A hostname mismatch is never tolerated, even with the opt-in enabled.
        if ((errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
            return false;

        return true;
    }
    private static bool ConnectionTest()
    {
        if (CPSConnectionData.ssSSO)
        {
            string url = $"{CPSConnectionData.ssUrl}/winapi/passwordlists/";

            using HttpClient client = CreateClient(useDefaultCredentials: true);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", "mRemote");
            client.DefaultRequestHeaders.Add("OTP", CPSConnectionData.ssOTP);

            var json = client.GetStringAsync(url).Result;
            JsonNode? data = JsonSerializer.Deserialize<JsonNode>(json);
            if (data == null)
                return false;
            return true;
        }
        else
        {
            string url = $"{CPSConnectionData.ssUrl}/api/passwordlists/";
            using HttpClient client = CreateClient(useDefaultCredentials: false);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", "mRemote");
            client.DefaultRequestHeaders.Add("APIKey", CPSConnectionData.ssPassword);
            client.DefaultRequestHeaders.Add("OTP", CPSConnectionData.ssOTP);

            var json = client.GetStringAsync(url).Result;
            JsonNode? data = JsonSerializer.Deserialize<JsonNode>(json);
            if (data == null)
                return false;
            return true;
        }
    }

    private static JsonNode? FetchDataWinAuth(int secretID)
    {
        string url = $"{CPSConnectionData.ssUrl}/winapi/passwords/{secretID}";

        using HttpClient client = CreateClient(useDefaultCredentials: true);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "mRemote");
        client.DefaultRequestHeaders.Add("OTP", CPSConnectionData.ssOTP);

        var json = client.GetStringAsync(url).Result;
        JsonNode? data = JsonSerializer.Deserialize<JsonNode>(json);
        if (data == null)
            return null;
        JsonNode? element = data[0];
        return element;
    }
    private static JsonNode? FetchDataAPIKeyAuth(int secretID)
    {
        string url = $"{CPSConnectionData.ssUrl}/api/passwords/{secretID}";

        using HttpClient client = CreateClient(useDefaultCredentials: false);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "mRemote");
        client.DefaultRequestHeaders.Add("APIKey", CPSConnectionData.ssPassword);
        client.DefaultRequestHeaders.Add("OTP", CPSConnectionData.ssOTP);

        var json = client.GetStringAsync(url).Result;
        JsonNode? data = JsonSerializer.Deserialize<JsonNode>(json);
        if (data == null)
            return null;
        JsonNode? element = data[0];
        return element;
    }

    private static void FetchSecret(int secretID, out string secretUsername, out string secretPassword, out string secretDomain, out string privatekey)
    {
        // clear return variables
        secretDomain = "";
        secretUsername = "";
        secretPassword = "";
        privatekey = "";
        string privatekeypassphrase = "";
        JsonNode? element = null;

        if (CPSConnectionData.ssSSO)
            element = FetchDataWinAuth(secretID);
        else
            element = FetchDataAPIKeyAuth(secretID);

        if (element == null)
            return;

        var dom = element["Domain"];
        if (dom != null) secretDomain = dom.ToString();

        var user = element["UserName"];
        if (user != null) secretUsername = user.ToString();

        var pw = element["Password"];
        if (pw != null) secretPassword = pw.ToString();

        var privkey = element["GenericField1"];
        if (privkey != null) privatekey = privkey.ToString();

        var phrase = element["GenericField3"];
        if (phrase != null) privatekeypassphrase = phrase.ToString();

        // need to decode the private key?
        if (!string.IsNullOrEmpty(privatekeypassphrase))
        {
            try
            {
                var key = DecodePrivateKey(privatekey, privatekeypassphrase);
                privatekey = key;
            }
            catch(Exception)
            {

            }
        }

        // conversion to putty format necessary?
        if (!string.IsNullOrEmpty(privatekey) && !privatekey.StartsWith("PuTTY-User-Key-File-2"))
        {
            try
            {
                RSACryptoServiceProvider key = ImportPrivateKey(privatekey);
                privatekey = PuttyKeyFileGenerator.ToPuttyPrivateKey(key);
            }
            catch (Exception)
            {

            }
        }
    }

    #region PUTTY KEY HANDLING
    // decode rsa private key with encryption password
    private static string DecodePrivateKey(string encryptedPrivateKey, string password)
    {
        TextReader textReader = new StringReader(encryptedPrivateKey);
        PemReader pemReader = new PemReader(textReader, new PasswordFinder(password));

        AsymmetricCipherKeyPair keyPair = (AsymmetricCipherKeyPair)pemReader.ReadObject();

        TextWriter textWriter = new StringWriter();
        var pemWriter = new PemWriter(textWriter);
        pemWriter.WriteObject(keyPair.Private);
        pemWriter.Writer.Flush();

        return ""+textWriter.ToString();
    }
    private class PasswordFinder(string password) : IPasswordFinder
    {
        private string password = password;

        public char[] GetPassword()
        {
            return password.ToCharArray();
        }
    }

    // read private key pem string to rsacryptoserviceprovider
    public static RSACryptoServiceProvider ImportPrivateKey(string pem)
    {
        PemReader pr = new PemReader(new StringReader(pem));
        AsymmetricCipherKeyPair KeyPair = (AsymmetricCipherKeyPair)pr.ReadObject();
        RSAParameters rsaParams = DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)KeyPair.Private);
        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.ImportParameters(rsaParams);
        return rsa;
    }
    #endregion


    // input: must be the secret id to fetch
    public static void FetchSecretFromServer(string secretID, out string username, out string password, out string domain, out string privatekey)
    {
        // get secret id
        int sid = Int32.Parse(secretID);

        // init connection credentials, display popup if necessary
        CPSConnectionData.Init();

        // get the secret
        FetchSecret(sid, out username, out password, out domain, out privatekey);
    }
}
