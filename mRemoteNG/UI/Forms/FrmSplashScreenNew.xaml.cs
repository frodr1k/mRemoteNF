using System;
using System.Runtime.Versioning;
using mRemoteNG.App.Info;

namespace mRemoteNG.UI.Forms
{
    [SupportedOSPlatform("windows")]
    /// <summary>
    /// Interaction logic for FrmSplashScreenNew.xaml
    /// </summary>
    public partial class FrmSplashScreenNew
    {
        static FrmSplashScreenNew instance = null;
        public FrmSplashScreenNew()
        {
            InitializeComponent();
            lblLogoPartD.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
            lblLogoPartD.Content = $@"mRemoteNF {GeneralAppInfo.ApplicationVersion}";
        }
        public static FrmSplashScreenNew GetInstance()
        {
            //instance == null
            instance ??= new FrmSplashScreenNew();
            return instance;
        }
    }
}
