namespace mRemoteNG.Security
{
    /// <summary>
    /// Provides on-demand decryption of a stored credential ciphertext.
    /// Used to support lazy (just-in-time) credential decryption so that
    /// stored secrets are not decrypted into memory at application startup
    /// (mitigation for CVE-2023-30367).
    /// </summary>
    public interface IConnectionCredentialDecryptor
    {
        /// <summary>
        /// Decrypts the supplied ciphertext. Returns an empty string when the
        /// input is empty.
        /// </summary>
        string Decrypt(string cipherText);
    }
}
