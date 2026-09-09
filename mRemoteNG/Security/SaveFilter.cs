using mRemoteNG.Config.Settings.Registry;
using System.Runtime.Versioning;

namespace mRemoteNG.Security
{
    [SupportedOSPlatform("windows")]
    public class SaveFilter
    {
        public SaveFilter(bool disableEverything = false)
        {
            if (disableEverything) return;
            SaveUsername = CommonRegistrySettings.AllowSaveUsernames;
            // When an external credential provider is mandated by policy, local passwords
            // must never be written to the connections file (Definition of Done).
            SavePassword = CommonRegistrySettings.AllowSavePasswords && !CommonRegistrySettings.RequireCredentialProvider;
            SaveDomain = true;
            SaveCredentialId = true;
            SaveInheritance = true;
        }

        public bool SaveUsername { get; set; }

        public bool SavePassword { get; set; }

        public bool SaveDomain { get; set; }

        public bool SaveCredentialId { get; set; }

        public bool SaveInheritance { get; set; }
    }
}