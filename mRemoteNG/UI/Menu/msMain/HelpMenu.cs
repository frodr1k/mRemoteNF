using System;
using System.Diagnostics;
using System.Windows.Forms;
using mRemoteNG.App;
using mRemoteNG.App.Info;
using mRemoteNG.UI.Forms;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;

namespace mRemoteNG.UI.Menu
{
    [SupportedOSPlatform("windows")]
    public class HelpMenu : ToolStripMenuItem
    {
        private ToolStripMenuItem _mMenInfoHelp = null!;
        private ToolStripSeparator _mMenInfoSep1 = null!;
        private ToolStripMenuItem _mMenInfoAbout = null!;

        public HelpMenu()
        {
            Initialize();
        }

        private void Initialize()
        {
            _mMenInfoHelp = new ToolStripMenuItem();
            _mMenInfoSep1 = new ToolStripSeparator();
            _mMenInfoAbout = new ToolStripMenuItem();

            // 
            // mMenInfo
            // 
            DropDownItems.AddRange(new ToolStripItem[]
            {
                _mMenInfoHelp,
                _mMenInfoSep1,
                _mMenInfoAbout
            });
            Name = "mMenInfo";
            Size = new System.Drawing.Size(44, 20);
            Text = Language._Help;
            TextDirection = ToolStripTextDirection.Horizontal;
            // 
            // mMenInfoHelp
            // 
            _mMenInfoHelp.Image = Properties.Resources.F1Help_16x;
            _mMenInfoHelp.Name = "mMenInfoHelp";
            _mMenInfoHelp.ShortcutKeys = Keys.F1;
            _mMenInfoHelp.Size = new System.Drawing.Size(190, 22);
            _mMenInfoHelp.Text = Language.MenuItem_HelpContents;
            _mMenInfoHelp.Click += mMenInfoHelp_Click;
            // 
            // mMenInfoSep1
            // 
            _mMenInfoSep1.Name = "mMenInfoSep1";
            _mMenInfoSep1.Size = new System.Drawing.Size(187, 6);
            // 
            // mMenInfoAbout
            // 
            _mMenInfoAbout.Image = Properties.Resources.UIAboutBox_16x;
            _mMenInfoAbout.Name = "mMenInfoAbout";
            _mMenInfoAbout.Size = new System.Drawing.Size(190, 22);
            _mMenInfoAbout.Text = Language.MenuItem_About;
            _mMenInfoAbout.Click += mMenInfoAbout_Click;
        }

        public void ApplyLanguage()
        {
            Text = Language._Help;
            _mMenInfoHelp.Text = Language.MenuItem_HelpContents;
            _mMenInfoAbout.Text = Language.MenuItem_About;
        }

        #region Info

        private void mMenInfoHelp_Click(object? sender, EventArgs e) => OpenUrl(GeneralAppInfo.UrlDocumentation);

        private static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) ||
                (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                 !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
                return;

            var startInfo = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }

        private void mMenInfoAbout_Click(object? sender, EventArgs e)
        {
            if (frmAbout.Instance == null || frmAbout.Instance.IsDisposed)
                frmAbout.Instance = new frmAbout();
            frmAbout.Instance.Show(FrmMain.Default.pnlDock);
        }

        #endregion
    }
}
