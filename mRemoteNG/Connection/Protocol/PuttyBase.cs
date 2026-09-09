using mRemoteNG.App;
using mRemoteNG.Credential.Provider;
using mRemoteNG.Messages;
using mRemoteNG.Resources.Language;
using mRemoteNG.Security;
using mRemoteNG.Security.SymmetricEncryption;
using mRemoteNG.Tools;
using mRemoteNG.Tools.Cmdline;
using mRemoteNG.Tree.Root;
using mRemoteNG.UI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteNG.Connection.Protocol
{
    [SupportedOSPlatform("windows")]
    public class PuttyBase : ProtocolBase
    {
        private const int IDM_RECONF = 0x50; // PuTTY Settings Menu ID
        private bool _isPuttyNg;
        private readonly DisplayProperties _display = new();
        private Panel? _puttyContainerPanel; // Panel to hold PuTTY with margins

        #region Public Properties

        protected Putty_Protocol PuttyProtocol { private get; set; }

        protected Putty_SSHVersion PuttySSHVersion { private get; set; }

        public IntPtr PuttyHandle { get; set; }

        private Process? PuttyProcess { get; set; }

        public static string? PuttyPath { get; set; }

        public bool Focused => NativeMethods.GetForegroundWindow() == PuttyHandle;

        #endregion

        #region Private Events & Handlers

        private void ProcessExited(object sender, EventArgs e)
        {
            Event_Closed(this);
        }

        /// <summary>
        /// Removes ALL window decorations from the embedded PuTTY window.
        /// Called initially when embedding and then on every resize to ensure styles persist.
        /// Uses DWM to remove the frame completely.
        /// </summary>
        private void RemovePuttyWindowDecorations()
        {
            if (PuttyHandle == IntPtr.Zero)
                return;

            try
            {
                // Remove ALL standard window styles
                int style = NativeMethods.GetWindowLong(PuttyHandle, NativeMethods.GWL_STYLE);
                style &= ~(NativeMethods.WS_CAPTION |         // Title bar
                           NativeMethods.WS_THICKFRAME |      // Resizable border
                           NativeMethods.WS_BORDER |          // Static border
                           NativeMethods.WS_VSCROLL |         // Vertical scrollbar
                           NativeMethods.WS_HSCROLL |         // Horizontal scrollbar
                           NativeMethods.WS_MINIMIZEBOX |     // Minimize button
                           NativeMethods.WS_MAXIMIZEBOX |     // Maximize button
                           NativeMethods.WS_SYSMENU);         // System menu
                NativeMethods.SetWindowLong(PuttyHandle, NativeMethods.GWL_STYLE, style);

                // Remove ALL extended window styles
                int exStyle = NativeMethods.GetWindowLong(PuttyHandle, NativeMethods.GWL_EXSTYLE);
                exStyle &= ~(NativeMethods.WS_EX_STATICEDGE |      // Static edge border
                             NativeMethods.WS_EX_CLIENTEDGE |      // Client edge border (3D effect)
                             NativeMethods.WS_EX_WINDOWEDGE);      // Window edge border  
                NativeMethods.SetWindowLong(PuttyHandle, NativeMethods.GWL_EXSTYLE, exStyle);

                // Use DWM (Desktop Window Manager) to extend client area into the frame
                // This removes the frame drawing completely for a seamless look
                try
                {
                    // Extend margins: -1 means extend into entire non-client area
                    NativeMethods.MARGINS margins = new() { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
                    NativeMethods.DwmExtendFrameIntoClientArea(PuttyHandle, ref margins);
                }
                catch
                {
                    // DWM might not be available on all Windows versions; continue anyway
                }

                // Force recalculation of the window frame
                NativeMethods.SetWindowPos(PuttyHandle, IntPtr.Zero,
                    0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                    $"Error removing PuTTY window decorations: {ex.Message}", true);
            }
        }

        /// <summary>
        /// Handles resize events to reapply border removal styles.
        /// Decorations can re-appear after resize, so we need to continuously remove them.
        /// Uses dynamically calculated border values with fallback to defaults if calculation fails.
        /// </summary>
        private void InterfaceControl_Resize(object sender, EventArgs e)
        {
            if (PuttyHandle == IntPtr.Zero)
                return;

            RemovePuttyWindowDecorations();

            // Use the same dynamic sizing logic as Resize() method
            // Use the container panel if it exists, otherwise use InterfaceControl
            Rectangle clientRect = _puttyContainerPanel?.ClientRectangle ?? InterfaceControl.ClientRectangle;

            // Get the actual border sizes from the window
            int leftBorder = 8;      // Default fallback
            int topBorder = 30;      // Default fallback
            int rightBorder = 8;     // Default fallback
            int bottomBorder = 8;    // Default fallback

            if (NativeMethods.GetWindowRect(PuttyHandle, out NativeMethods.RECT windowRect) &&
                NativeMethods.GetClientRect(PuttyHandle, out NativeMethods.RECT internalClientRect))
            {
                long windowWidth = windowRect.right - windowRect.left;
                long windowHeight = windowRect.bottom - windowRect.top;
                long clientWidth = internalClientRect.right - internalClientRect.left;
                long clientHeight = internalClientRect.bottom - internalClientRect.top;

                // Only use calculated values if they're reasonable (positive and not huge)
                int calcLeftBorder = (int)(internalClientRect.left);
                int calcTopBorder = (int)(internalClientRect.top);
                int calcRightBorder = (int)(windowWidth - clientWidth - calcLeftBorder);
                int calcBottomBorder = (int)(windowHeight - clientHeight - calcTopBorder);

                // Validate the calculated values
                if (calcLeftBorder > 0 && calcLeftBorder < 50 &&
                    calcTopBorder > 0 && calcTopBorder < 100 &&
                    calcRightBorder > 0 && calcRightBorder < 50 &&
                    calcBottomBorder > 0 && calcBottomBorder < 50)
                {
                    leftBorder = calcLeftBorder;
                    topBorder = calcTopBorder;
                    rightBorder = calcRightBorder;
                    bottomBorder = calcBottomBorder;
                }
            }

            // Apply calculated offsets to hide borders outside panel
            NativeMethods.MoveWindow(PuttyHandle,
                clientRect.X - leftBorder,
                clientRect.Y - topBorder,
                clientRect.Width + leftBorder + rightBorder,
                clientRect.Height + topBorder + bottomBorder,
                true);
        }

        #endregion

        #region Public Methods

        public bool isRunning()
        {
            return PuttyProcess?.HasExited == false;
        }

        public void CreatePipe(object oData)
        {
            string data = (string)oData;
            string random = data[..8];
            string password = data[8..];
            using NamedPipeServerStream server = CreatePipeServer($"mRemoteNGSecretPipe{random}");
            server.WaitForConnection();
            using StreamWriter writer = new(server);
            writer.Write(password);
            writer.Flush();
        }

        public override bool Connect()
        {
            string optionalTemporaryPrivateKeyPath = ""; // path to ppk file instead of password. only temporary (extracted from credential vault).

            try
            {
                _isPuttyNg = PuttyTypeDetector.GetPuttyType() == PuttyTypeDetector.PuttyType.PuttyNg;

                // Validate PuttyPath to prevent command injection
                PathValidator.ValidateExecutablePathOrThrow(PuttyPath, nameof(PuttyPath));

                PuttyProcess = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = PuttyPath
                    }
                };

                CommandLineArguments arguments = new() { EscapeForShell = false };

                arguments.Add("-load", InterfaceControl.Info.PuttySession);

                if (!(InterfaceControl.Info is PuttySessionInfo))
                {
                    arguments.Add("-" + PuttyProtocol);

                    if (PuttyProtocol == Putty_Protocol.ssh)
                    {

                        string username = InterfaceControl.Info?.Username ?? "";
                        //string password = InterfaceControl.Info?.Password?.ConvertToUnsecureString() ?? "";
                        string password = InterfaceControl.Info?.Password ?? "";
                        string UserViaAPI = InterfaceControl.Info?.UserViaAPI ?? "";
                        string privatekey = "";

                        // Resolve credentials from an external provider just-in-time if configured.
                        if (CredentialProviderCatalog.Default.TryGetProvider(InterfaceControl.Info?.ExternalCredentialProvider ?? ExternalCredentialProvider.None, out ICredentialProvider sshCredentialProvider))
                        {
                            CredentialProviderResult resolved = new()
                            {
                                Username = username,
                                Password = password,
                                PrivateKey = privatekey
                            };

                            sshCredentialProvider.Populate(
                                new CredentialProviderRequest
                                {
                                    Usage = CredentialProviderUsage.SshConnection,
                                    CredentialId = UserViaAPI,
                                    Username = InterfaceControl.Info?.Username,
                                    Hostname = InterfaceControl.Info?.Hostname ?? "",
                                    VaultSecretEngine = InterfaceControl.Info?.VaultOpenbaoSecretEngine ?? VaultOpenbaoSecretEngine.Kv,
                                    VaultMount = InterfaceControl.Info?.VaultOpenbaoMount ?? "",
                                    VaultRole = InterfaceControl.Info?.VaultOpenbaoRole ?? ""
                                },
                                resolved,
                                message => Event_ErrorOccured(this, message, 0));

                            username = resolved.Username;
                            password = resolved.Password;
                            privatekey = resolved.PrivateKey;

                            if (!string.IsNullOrEmpty(privatekey))
                            {
                                optionalTemporaryPrivateKeyPath = Path.GetTempFileName();
                                RestrictFileToCurrentUser(optionalTemporaryPrivateKeyPath);
                                File.WriteAllText(optionalTemporaryPrivateKeyPath, privatekey);
                                FileInfo fileInfo = new(optionalTemporaryPrivateKeyPath)
                                {
                                    Attributes = FileAttributes.Temporary
                                };
                            }

                            resolved.Purge();
                        }

                        if (string.IsNullOrEmpty(username))
                        {
                            switch (Properties.OptionsCredentialsPage.Default.EmptyCredentials)
                            {
                                case "windows":
                                    username = Environment.UserName;
                                    break;
                                case "custom" when !string.IsNullOrEmpty(Properties.OptionsCredentialsPage.Default.DefaultUsername):
                                    username = Properties.OptionsCredentialsPage.Default.DefaultUsername;
                                    break;
                                case "custom":

                                    if (Properties.OptionsCredentialsPage.Default.ExternalCredentialProviderDefault == ExternalCredentialProvider.DelineaSecretServer)
                                    {
                                        try
                                        {
                                            ExternalConnectors.DSS.SecretServerInterface.FetchSecretFromServer(
                                                $"{Properties.OptionsCredentialsPage.Default.UserViaAPIDefault}", out username, out password, out _, out privatekey);
                                        }
                                        catch (Exception ex)
                                        {
                                            Event_ErrorOccured(this, "Secret Server Interface Error: " + ex.Message, 0);
                                        }
                                    }

                                    break;
                            }
                        }


                        if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(optionalTemporaryPrivateKeyPath))
                        {
                            if (Properties.OptionsCredentialsPage.Default.EmptyCredentials == "custom")
                            {
                                LegacyRijndaelCryptographyProvider cryptographyProvider = new();
                                password = cryptographyProvider.Decrypt(Properties.OptionsCredentialsPage.Default.DefaultPassword, Runtime.EncryptionKey);
                            }
                        }

                        arguments.Add("-" + (int)PuttySSHVersion);

                        if (!Force.HasFlag(ConnectionInfo.Force.NoCredentials))
                        {
                            if (!string.IsNullOrEmpty(username))
                            {
                                arguments.Add("-l", username);
                            }

                            if (!string.IsNullOrEmpty(password))
                            {
                                string random = string.Join("", Guid.NewGuid().ToString("n").Take(8));
                                // write data to pipe
                                Thread thread = new(new ParameterizedThreadStart(CreatePipe));
                                thread.Start($"{random}{password}");
                                // start putty with piped password
                                arguments.Add("-pwfile", $"\\\\.\\PIPE\\mRemoteNGSecretPipe{random}");
                                //arguments.Add("-pw", password);
                            }
                        }

                        if (InterfaceControl.Info?.ExternalCredentialProvider == ExternalCredentialProvider.VaultOpenbao && InterfaceControl.Info?.VaultOpenbaoSecretEngine == VaultOpenbaoSecretEngine.SSHOTP) {
                            if (!_isPuttyNg) {
                                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "Cannot connect to VaultOpenbao ssh otp without using puttyng to inject authenticator plugin");
                                return false;
                            }
                            arguments.Add("-auth-plugin");
                            string random = string.Join("", Guid.NewGuid().ToString("n").Take(8));
                            string pipename = $"mRemoteNGSecretPipe{random}";
                            arguments.Add($"{App.Info.GeneralAppInfo.HomePath}\\vault-ssh-helper-plugin.exe {username} --pipeName={pipename}");
                            System.Threading.Tasks.Task.Run(async () => {
                                using NamedPipeServerStream server = CreatePipeServer(pipename);
                                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;
                                await server.WaitForConnectionAsync(cts);
                                using var reader = new StreamReader(server, Utf8NoBom, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                                using var writer = new StreamWriter(server, Utf8NoBom, bufferSize: 1024, leaveOpen: true) { AutoFlush = true };
                                string? pingMessage = await reader.ReadLineAsync(cts);
                                if (pingMessage != "ping") throw new FormatException("Invalid ping from VaultOpenbao SSH OTP plugin");
                                await writer.WriteLineAsync("pong");
                                string dataRequest = await reader.ReadLineAsync(cts) ?? throw new FormatException("Invalid data request from VaultOpenbao SSH OTP plugin");
                                var data = DeserializeData(dataRequest);
                                if (data.Username != username || data.Hostname != InterfaceControl.Info.Hostname || data.Port != InterfaceControl.Info.Port)
                                    throw new FormatException("Mismatched data request from VaultOpenbao SSH OTP plugin");
                                await writer.WriteLineAsync(password);
                            }).ConfigureAwait(false);
                        }

                        // use private key if specified
                        if (!string.IsNullOrEmpty(optionalTemporaryPrivateKeyPath))
                        {
                            arguments.Add("-i", optionalTemporaryPrivateKeyPath);
                        }

                    }

                    arguments.Add("-P", InterfaceControl.Info?.Port.ToString());
                    arguments.Add(InterfaceControl.Info.Hostname);
                }

                if (_isPuttyNg)
                {
                    arguments.Add("-hwndparent", InterfaceControl.Handle.ToString());
                }

                PuttyProcess.StartInfo.Arguments = arguments.ToString();
                // add additional SSH options, f.e. tunnel or noshell parameters that may be specified for the the connnection
                if (!string.IsNullOrEmpty(InterfaceControl.Info.SSHOptions))
                {
                    // SSHOptions is appended verbatim to the PuTTY command line. A
                    // malicious/imported connection could smuggle flags that execute
                    // remote commands (-m) or override authentication (-pw/-pwfile/-i/
                    // -auth-plugin). Reject the whole SSHOptions string (fail-safe:
                    // still connect, just ignore the options) when such a flag is
                    // present, while leaving legitimate tunnel flags (-L/-R/-D/-N/...) intact.
                    if (ContainsDangerousPuttyOption(InterfaceControl.Info.SSHOptions))
                    {
                        Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                            "Ignoring SSH options for this connection because they contain a disallowed " +
                            "flag (one of -m, -pw, -pwfile, -i, -auth-plugin) that could execute commands " +
                            "or override authentication.", true);
                    }
                    else
                    {
                        PuttyProcess.StartInfo.Arguments += " " + InterfaceControl.Info.SSHOptions;
                    }
                }

                PuttyProcess.EnableRaisingEvents = true;
                PuttyProcess.Exited += ProcessExited;

                // Start the process minimized for non-PuTTYNG so the window
                // does not flash at its default position on screen before
                // being reparented into the mRemoteNG panel.
                if (!_isPuttyNg)
                {
                    PuttyProcess.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                }

                PuttyProcess.Start();
                ChildProcessTracker.AddProcess(PuttyProcess);
                PuttyProcess.WaitForInputIdle(Properties.OptionsAdvancedPage.Default.MaxPuttyWaitTime * 1000);

                int startTicks = Environment.TickCount;
                while (PuttyHandle.ToInt32() == 0 &
                       Environment.TickCount < startTicks + Properties.OptionsAdvancedPage.Default.MaxPuttyWaitTime * 1000)
                {
                    if (PuttyProcess.HasExited)
                        break;

                    if (_isPuttyNg)
                    {
                        PuttyHandle = NativeMethods.FindWindowEx(InterfaceControl.Handle, new IntPtr(0), null, null);
                    }
                    else
                    {
                        PuttyProcess.Refresh();
                        IntPtr candidateHandle = PuttyProcess.MainWindowHandle;

                        if (candidateHandle != IntPtr.Zero)
                        {
                            // Check the window class name to distinguish the actual PuTTY
                            // terminal window ("PuTTY") from popup dialogs like the host key
                            // verification alert (class "#32770"). Dialogs must remain as
                            // top-level windows so the user can interact with them.
                            StringBuilder className = new(256);
                            NativeMethods.GetClassName(candidateHandle, className, className.Capacity);
                            string cls = className.ToString();

                            if (cls.Equals("PuTTY", StringComparison.OrdinalIgnoreCase))
                            {
                                PuttyHandle = candidateHandle;
                                // Hide the window immediately so it doesn't flash
                                // at its default position before being reparented.
                                NativeMethods.ShowWindow(PuttyHandle, (int)NativeMethods.SW_HIDE);
                            }
                        }
                    }

                    if (PuttyHandle.ToInt32() == 0)
                    {
                        Thread.Sleep(100);
                    }
                }

                if (!_isPuttyNg)
                {
                    // Create a container panel with 10px margins on all sides
                    _puttyContainerPanel = new Panel
                    {
                        Parent = InterfaceControl,
                        Dock = DockStyle.Fill,
                        Margin = new Padding(10, 10, 10, 10),
                        BackColor = System.Drawing.Color.Black
                    };

                    // Parent PuTTY to the container panel instead of InterfaceControl
                    NativeMethods.SetParent(PuttyHandle, _puttyContainerPanel.Handle);

                    // Get container panel dimensions for sizing
                    Rectangle containerRect = _puttyContainerPanel.ClientRectangle;

                    // Calculate 70% size 
                    int newWidth = (int)(containerRect.Width * 0.7);
                    int newHeight = (int)(containerRect.Height * 0.7);

                    // Initial aggressive border and decoration removal
                    RemovePuttyWindowDecorations();

                    // Step 1: First SetWindowPos call - with explicit position and size
                    NativeMethods.SetWindowPos(PuttyHandle, IntPtr.Zero,
                        0, 0, 
                        newWidth, newHeight,
                        NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);

                    // Step 2: Small delay to allow Windows to process style changes
                    Thread.Sleep(100);

                    // Step 3: Second SetWindowPos call - force recalculation again
                    NativeMethods.SetWindowPos(PuttyHandle, IntPtr.Zero,
                        0, 0, 
                        newWidth, newHeight,
                        NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);

                    // Step 4: Show the window explicitly to force redraw
                    NativeMethods.ShowWindow(PuttyHandle, (int)NativeMethods.SW_SHOW);

                    // Step 5: Final positioning to ensure seamless integration
                    NativeMethods.SetWindowPos(PuttyHandle, IntPtr.Zero,
                        0, 0, 
                        newWidth, newHeight,
                        NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW);

                    // CRITICAL: Hook into resize event to reapply decoration removal
                    // This ensures decorations don't reappear when the window is resized
                    InterfaceControl.Resize += InterfaceControl_Resize;

                    Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, 
                        Language.PuttyStuff + ": Border removal hooked to persist through resizes", true);
                }

                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, Language.PuttyStuff, true);
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, string.Format(Language.PuttyHandle, PuttyHandle), true);
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, string.Format(Language.PuttyTitle, PuttyProcess.MainWindowTitle), true);
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, string.Format(Language.PanelHandle, InterfaceControl.Parent.Handle), true);

                if (!string.IsNullOrEmpty(InterfaceControl.Info?.OpeningCommand))
                {
                    NativeMethods.SetForegroundWindow(PuttyHandle);
                    string finalCommand = InterfaceControl.Info.OpeningCommand.TrimEnd() + "\n";
                    SendKeys.SendWait(finalCommand);
                }

                if (!_isPuttyNg)
                {
                    NativeMethods.ShowWindow(PuttyHandle, (int)NativeMethods.SW_RESTORE);
                }

                Resize(this, new EventArgs());
                base.Connect();
                return true;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.ConnectionFailed + Environment.NewLine + ex.Message);
                return false;
            }
            finally
            {
                // make sure to remove the private key file
                if (!string.IsNullOrEmpty(optionalTemporaryPrivateKeyPath))
                {
                    System.Threading.Thread.Sleep(500);
                    SecureDeleteFile(optionalTemporaryPrivateKeyPath);
                }
            }
        }

        /// <summary>
        /// Checks whether a user-supplied SSHOptions string contains a PuTTY flag
        /// that could execute remote commands or override authentication. Only
        /// these clearly dangerous flags are blocked; legitimate options such as
        /// tunnels (-L/-R/-D), -N, -X, -A, -C remain allowed.
        /// </summary>
        private static bool ContainsDangerousPuttyOption(string sshOptions)
        {
            // PuTTY flags are case-sensitive and space-separated on the command line.
            string[] blockedFlags = { "-m", "-pw", "-pwfile", "-i", "-auth-plugin" };
            string[] tokens = sshOptions.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                foreach (string blocked in blockedFlags)
                {
                    if (string.Equals(token, blocked, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Restricts a file so that only the current user (and SYSTEM) can read
        /// it, removing inherited permissions. Used for the temporary SSH private
        /// key file so other accounts on the machine cannot read it.
        /// </summary>
        private static void RestrictFileToCurrentUser(string path)
        {
            try
            {
                FileInfo fileInfo = new(path);
                FileSecurity security = new();
                SecurityIdentifier user = WindowsIdentity.GetCurrent().User!;
                SecurityIdentifier system = new(WellKnownSidType.LocalSystemSid, null);
                security.SetOwner(user);
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, AccessControlType.Allow));
                fileInfo.SetAccessControl(security);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                    "Could not restrict permissions on the temporary SSH key file: " + ex.Message, true);
            }
        }

        /// <summary>
        /// Overwrites a file's contents with random data before deleting it, so
        /// the plaintext private key cannot be trivially recovered from disk.
        /// </summary>
        private static void SecureDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    long length = new FileInfo(path).Length;
                    if (length > 0)
                    {
                        byte[] noise = new byte[length];
                        System.Security.Cryptography.RandomNumberGenerator.Fill(noise);
                        File.WriteAllBytes(path, noise);
                    }
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                    "Could not overwrite the temporary SSH key file before deletion: " + ex.Message, true);
            }
            finally
            {
                try { File.Delete(path); } catch { /* best effort */ }
            }
        }

        public override void Focus()
        {
            try
            {
                NativeMethods.SetForegroundWindow(PuttyHandle);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.PuttyFocusFailed + Environment.NewLine + ex.Message, true);
            }
        }

        protected override void Resize(object sender, EventArgs e)
        {
            try
            {
                if (InterfaceControl.Size == Size.Empty)
                    return;

                if (_isPuttyNg)
                {
                    // PuTTYNG 0.70.0.1 and later doesn't have any window borders
                    // Use ClientRectangle to account for padding (for connection frame color)
                    Rectangle clientRect = InterfaceControl.ClientRectangle;
                    NativeMethods.MoveWindow(PuttyHandle, clientRect.X, clientRect.Y, clientRect.Width, clientRect.Height, true);
                }
                else
                {
                    // For regular PuTTY, use the container panel if it exists
                    Rectangle clientRect = _puttyContainerPanel?.ClientRectangle ?? InterfaceControl.ClientRectangle;

                    // Get the actual border sizes from the window
                    int leftBorder = 8;      // Default fallback
                    int topBorder = 30;      // Default fallback
                    int rightBorder = 8;     // Default fallback
                    int bottomBorder = 8;    // Default fallback
                    bool calculatedSuccessfully = false;

                    if (NativeMethods.GetWindowRect(PuttyHandle, out NativeMethods.RECT windowRect) &&
                        NativeMethods.GetClientRect(PuttyHandle, out NativeMethods.RECT internalClientRect))
                    {
                        long windowWidth = windowRect.right - windowRect.left;
                        long windowHeight = windowRect.bottom - windowRect.top;
                        long clientWidth = internalClientRect.right - internalClientRect.left;
                        long clientHeight = internalClientRect.bottom - internalClientRect.top;

                        // Only use calculated values if they're reasonable (positive and not huge)
                        int calcLeftBorder = (int)(internalClientRect.left);
                        int calcTopBorder = (int)(internalClientRect.top);
                        int calcRightBorder = (int)(windowWidth - clientWidth - calcLeftBorder);
                        int calcBottomBorder = (int)(windowHeight - clientHeight - calcTopBorder);

                        // Validate the calculated values
                        if (calcLeftBorder > 0 && calcLeftBorder < 50 &&
                            calcTopBorder > 0 && calcTopBorder < 100 &&
                            calcRightBorder > 0 && calcRightBorder < 50 &&
                            calcBottomBorder > 0 && calcBottomBorder < 50)
                        {
                            leftBorder = calcLeftBorder;
                            topBorder = calcTopBorder;
                            rightBorder = calcRightBorder;
                            bottomBorder = calcBottomBorder;
                            calculatedSuccessfully = true;

                            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                                $"PuTTY dynamic borders - Left:{leftBorder}, Top (header):{topBorder}, Right (+ scroll): {rightBorder}, Bottom:{bottomBorder}", true);
                        }
                    }

                    if (!calculatedSuccessfully)
                    {
                        Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                            $"PuTTY using fallback borders - Left:{leftBorder}, Top:{topBorder}, Right:{rightBorder}, Bottom:{bottomBorder}", true);
                    }

                    // Apply calculated offsets to hide borders outside panel
                    // Position at negative offset to move borders out of view
                    // Size is expanded by border amounts to compensate
                    NativeMethods.MoveWindow(PuttyHandle,
                        clientRect.X - leftBorder,
                        clientRect.Y - topBorder,
                        clientRect.Width + leftBorder + rightBorder,
                        clientRect.Height + topBorder + bottomBorder,
                        true);
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.PuttyResizeFailed + Environment.NewLine + ex.Message, true);
            }
        }

        public override void Close()
        {
            try
            {
                // Remove resize event handler to prevent memory leaks
                InterfaceControl.Resize -= InterfaceControl_Resize;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, 
                    Language.PuttyStuff + ": Error removing resize handler: " + ex.Message, true);
            }

            try
            {
                // Dispose the container panel if it was created
                _puttyContainerPanel?.Dispose();
                _puttyContainerPanel = null;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                    Language.PuttyStuff + ": Error disposing container panel: " + ex.Message, true);
            }

            try
            {
                if (PuttyProcess?.HasExited == false)
                {
                    PuttyProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.PuttyKillFailed + Environment.NewLine + ex.Message, true);
            }

            try
            {
                PuttyProcess?.Dispose();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.PuttyDisposeFailed + Environment.NewLine + ex.Message, true);
            }

            base.Close();
        }

        public void ShowSettingsDialog()
        {
            try
            {
                NativeMethods.PostMessage(PuttyHandle, NativeMethods.WM_SYSCOMMAND, (IntPtr)IDM_RECONF, (IntPtr)0);
                NativeMethods.SetForegroundWindow(PuttyHandle);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.PuttyShowSettingsDialogFailed + Environment.NewLine + ex.Message, true);
            }
        }

        #endregion

        #region Enums

        protected enum Putty_Protocol
        {
            ssh = 0,
            telnet = 1,
            rlogin = 2,
            raw = 3,
            serial = 4
        }

        protected enum Putty_SSHVersion
        {
            ssh1 = 1,
            ssh2 = 2
        }

        #endregion

        #region VaultOpenbaoUtils
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static NamedPipeServerStream CreatePipeServer(string pipeName) {
            var pipeSecurity = new PipeSecurity();
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.Owner ?? identity.User ?? throw new InvalidOperationException("Unable to determine current user SID.");
            pipeSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            pipeSecurity.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                pipeName: pipeName,
                direction: PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                transmissionMode: PipeTransmissionMode.Byte,
                options: PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                pipeSecurity);
        }
        private static (string Username, string Hostname, uint Port) DeserializeData(string data) {
            var strings = data.Split(':');
            if (strings.Length != 3) {
                throw new FormatException("Invalid data format");
            }
            return (
                Encoding.UTF8.GetString(Convert.FromBase64String(strings[0])),
                Encoding.UTF8.GetString(Convert.FromBase64String(strings[1])),
                uint.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(strings[2])))
            );
        }
        #endregion
    }
}