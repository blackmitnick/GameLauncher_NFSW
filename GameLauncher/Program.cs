using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Globalization;
using System.Reflection;
using WindowsFirewallHelper;
using GameLauncher.App.Classes;
using GameLauncher.App.Classes.Logger;
using GameLauncher.App.Classes.InsiderKit;
using GameLauncher.App.Classes.LauncherCore.ModNet;
using GameLauncher.App.Classes.SystemPlatform.Windows;
using GameLauncher.App.Classes.LauncherCore.FileReadWrite;
using GameLauncher.App.Classes.LauncherCore.APICheckers;
using GameLauncher.App.Classes.LauncherCore.Visuals;
using GameLauncher.App.Classes.LauncherCore.Global;
using GameLauncher.App.Classes.SystemPlatform.Components;
using GameLauncher.App.Classes.LauncherCore.Client;
using GameLauncher.App.Classes.LauncherCore.Proxy;
using GameLauncher.App.Classes.SystemPlatform.Linux;
using GameLauncher.App.Classes.LauncherCore.Lists;
using GameLauncher.App.Classes.LauncherCore.LauncherUpdater;
using GameLauncher.App.Classes.LauncherCore.RPC;
using System.Threading.Tasks;
using GameLauncher.App.Classes.LauncherCore.Client.Web;

namespace GameLauncher
{
    internal static class Program
    {
        /* Global Thread for Splash Screen */
        private static Thread _SplashScreen;
        private static bool IsSplashScreenLive = false;

        private static void NetCodeDefaults()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        }

        [STAThread]
        static async Task Main()
        {
            if (Debugger.IsAttached && !NFSW.IsNFSWRunning())
            {
                NetCodeDefaults();
                await DoRunChecksAsync();
            } 
            else
            {
                if (NFSW.IsNFSWRunning())
                {
                    MessageBox.Show(null, "An instance of Need for Speed: World is already running", "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    Process.GetProcessById(Process.GetCurrentProcess().Id).Kill();
                }

                NetCodeDefaults();

                /* INFO: this is here because this dll is necessary for downloading game files and I want to make it async.
                   Updated RedTheKitsune Code so it downloads the file if its missing.
                   It also restarts the launcher if the user click on yes on Prompt. - DavidCarbon */
                if (!File.Exists("LZMA.dll"))
                {
                    try
                    {
                        using (WebClientWithTimeout wc = new WebClientWithTimeout())
                        {
                            wc.DownloadFile(new Uri(URLs.fileserver + "/LZMA.dll"), "LZMA.dll");
                        }

                        DialogResult restartApp = MessageBox.Show(null, "Downloaded Missing LZMA.dll File. \nPlease Restart Launcher, Thanks!", "GameLauncher Restart Required", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                        if (restartApp == DialogResult.Yes)
                        {
                            Application.Restart();
                        }

                        Process.GetProcessById(Process.GetCurrentProcess().Id).Kill();
                    }
                    catch (Exception) { }
                }

                var mutex = new Mutex(false, "GameLauncherNFSW-MeTonaTOR");
                try
                {
                    if (mutex.WaitOne(0, false))
                    {
                        string[] files = {
                            "CommandLine.dll - 2.8.0",
                            "DiscordRPC.dll - 1.0.175.0",
                            "Flurl.dll - 3.0.1",
                            "Flurl.Http.dll - 3.0.1",
                            "INIFileParser.dll - 2.5.2",
                            "LZMA.dll - 9.10 beta",
                            "Microsoft.WindowsAPICodePack.dll - 1.1.0.0",
                            "Microsoft.WindowsAPICodePack.Shell.dll - 1.1.0.0",
                            "Microsoft.WindowsAPICodePack.ShellExtensions.dll - 1.1.0.0",
                            "Nancy.dll - 2.0.0",
                            "Nancy.Hosting.Self.dll - 2.0.0",
                            "Newtonsoft.Json.dll - 12.0.3",
                            "System.Runtime.InteropServices.RuntimeInformation.dll - 4.6.24705.01. Commit Hash: 4d1af962ca0fede10beb01d197367c2f90e92c97",
                            "System.ValueTuple.dll - 4.6.26515.06 @BuiltBy: dlab-DDVSOWINAGE059 @Branch: release/2.1 @SrcCode: https://github.com/dotnet/corefx/tree/30ab651fcb4354552bd4891619a0bdd81e0ebdbf",
                            "WindowsFirewallHelper.dll - 2.0.4.70-beta2"
                        };

                        var missingfiles = new List<string>();

                        if (!DetectLinux.LinuxDetected())
                        {   /* MONO Hates this... */
                            foreach (var file in files) {
                                var splitFileVersion = file.Split(new string[] { " - " }, StringSplitOptions.None);

                                if (!File.Exists(Directory.GetCurrentDirectory() + "\\" + splitFileVersion[0]))
                                {
                                    missingfiles.Add(splitFileVersion[0] + " - Not Found");
                                } 
                                else
                                {
                                    try
                                    {
                                        var versionInfo = FileVersionInfo.GetVersionInfo(splitFileVersion[0]);
                                        string[] versionsplit = versionInfo.ProductVersion.Split('+');
                                        string version = versionsplit[0];

                                        if (version == "")
                                        {
                                            missingfiles.Add(splitFileVersion[0] + " - Invalid File");
                                        } 
                                        else
                                        { 
                                            if (HardwareInfo.CheckArchitectureFile(splitFileVersion[0]) == false) 
                                            {
                                                missingfiles.Add(splitFileVersion[0] + " - Wrong Architecture");
                                            } 
                                            else
                                            {
                                                if (version != splitFileVersion[1])
                                                {
                                                    missingfiles.Add(splitFileVersion[0] + " - Invalid Version (" + splitFileVersion[1] + " != " + version + ")");
                                                }
                                            }
                                        }
                                    } 
                                    catch
                                    {
                                        missingfiles.Add(splitFileVersion[0] + " - Invalid File");
                                    }
                                }
                            }
                        }
                        if (missingfiles.Count != 0)
                        {
                            var message = "Cannot launch GameLauncher. The following files are invalid:\n\n";

                            foreach (var file in missingfiles)
                            {
                                message += "• " + file + "\n";
                            }

                            MessageBox.Show(null, message, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else
                        {
                            await DoRunChecksAsync();
                        }
                    } 
                    else
                    {
                        MessageBox.Show(null, "An instance of Launcher is already running.", "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                } 
                finally
                {
                    mutex.Close();
                }
            }
        }

        private static void SplashScreen()
        {
            if (IsSplashScreenLive == false)
            {
                Application.Run(new SplashScreen());
            }

            IsSplashScreenLive = true;
        }

        private static async Task DoRunChecksAsync()
        {
            if (!DetectLinux.LinuxDetected())
            {
                /* Check if User has .NETFramework 4.6.2 or later Installed */
                const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";

                using (var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subkey))
                {
                    if (ndpKey != null && ndpKey.GetValue("Release") != null && (int)ndpKey.GetValue("Release") >= 394802)
                    {
                        InformationCache.CurrentLanguage = CultureInfo.CurrentCulture.Name.Split('-')[0].ToUpper();
                        Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
                        Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("en-US");

                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(true);
                    }
                    else
                    {
                        DialogResult frameworkError = MessageBox.Show(null, "This application requires one of the following versions of the .NET Framework:\n" +
                            " .NETFramework, Version=v4.6.2 \n\nDo you want to install this .NET Framework version now?", "GameLauncher.exe - This application could not be started.", MessageBoxButtons.YesNo, MessageBoxIcon.Error);

                        if (frameworkError == DialogResult.Yes)
                        {
                            Process.Start("https://dotnet.microsoft.com/download/dotnet-framework");
                        }

                        /* Close Splash Screen (Just in Case) */
                        if (IsSplashScreenLive == true)
                        {
                            _SplashScreen.Abort();
                        }

                        Process.GetProcessById(Process.GetCurrentProcess().Id).Kill();
                    }
                }
            }

            /* Splash Screen */
            if (!Debugger.IsAttached)
            {
                _SplashScreen = new Thread(new ThreadStart(SplashScreen));
                _SplashScreen.Start();
            }

            File.Delete("communication.log");
            File.Delete("launcher.log");
            Log.StartLogging();

            if (EnableInsider.ShouldIBeAnInsider() == true)
            {
                Log.Build("INSIDER: GameLauncher " + Application.ProductVersion + "_" + EnableInsider.BuildNumber());
            }
            else
            {
                Log.Build("BUILD: GameLauncher " + Application.ProductVersion);
            }

            Log.Info("LAUNCHER: Detecting OS");
            if (DetectLinux.LinuxDetected())
            {
                InformationCache.OSName = DetectLinux.Distro();
                Log.System("SYSTEM: Detected OS: " + InformationCache.OSName);
            }
            else
            {
                WindowsProductVersion.CachedWindowsNumber = WindowsProductVersion.GetWindowsNumber();
                InformationCache.OSName = WindowsProductVersion.ConvertWindowsNumberToName(WindowsProductVersion.CachedWindowsNumber);

                Log.System("SYSTEM: Detected OS: " + InformationCache.OSName);
                Log.System("SYSTEM: OS Details: " + Environment.OSVersion);
                Log.System("SYSTEM: Video Card: " + HardwareInfo.GPU.CardName());
                Log.System("SYSTEM: Driver Version: " + HardwareInfo.GPU.DriverVersion());
            }

            FileSettingsSave.NullSafeSettings();
            FileAccountSave.NullSafeAccount();

            /* Set Launcher Directory */
            Log.Info("CORE: Setting up current directory: " + Path.GetDirectoryName(Application.ExecutablePath));
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Application.ExecutablePath));

            if (!DetectLinux.LinuxDetected())
            {
                Log.Info("CORE: Checking current directory");

                switch (FunctionStatus.CheckFolder(Directory.GetCurrentDirectory()))
                {
                    case FolderType.IsTempFolder:
                    case FolderType.IsUsersFolders:
                    case FolderType.IsProgramFilesFolder:
                    case FolderType.IsWindowsFolder:
                    case FolderType.IsRootFolder:
                        String constructMsg = String.Empty;

                        constructMsg += "Using this location for GameLauncher is not allowed.\nThe Launcher folder/directory can NOT be in:\n\n";
                        constructMsg += "• X:\\ (Root of Drive, such as C:\\ or D:\\)\n";
                        constructMsg += "• C:\\Program Files\n";
                        constructMsg += "• C:\\Program Files (x86)\n";
                        constructMsg += "• C:\\Users (Includes 'Desktop', 'Documents', 'Downloads')\n";
                        constructMsg += "• C:\\Windows\n\n";
                        constructMsg += "Instead, move it someplace like:\n";
                        constructMsg += "• 'X:\\Soabox Race World' or 'X:\\SBRW'\n";
                        constructMsg += "(Where 'X:' is a 'Local Disk' location on `My Computer` / `This PC`)\n\n";
                        MessageBox.Show(null, constructMsg, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Environment.Exit(0);
                        break;
                }

                if (!FunctionStatus.HasWriteAccessToFolder(Path.GetDirectoryName(Application.ExecutablePath)))
                {
                    MessageBox.Show("Unable to do a Test Write to Launcher Folder\nPermission Issue");
                }

                if (!FunctionStatus.HasWriteAccessToFolder(FileSettingsSave.GameInstallation) && !string.IsNullOrEmpty(FileSettingsSave.GameInstallation))
                {
                    MessageBox.Show("Unable to do a Test Write to Game Files Folder\nPermission Issue");
                }

                /* Windows Firewall Runner */
                if (!string.IsNullOrEmpty(FileSettingsSave.FirewallLauncherStatus))
                {
                    if (FirewallManager.IsServiceRunning == true && FirewallHelper.FirewallStatus() == true)
                    {
                        string nameOfLauncher = "SBRW - Game Launcher";
                        string localOfLauncher = Assembly.GetEntryAssembly().Location;

                        string nameOfUpdater = "SBRW - Game Launcher Updater";
                        string localOfUpdater = Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "GameLauncherUpdater.exe");

                        string groupKeyLauncher = "Game Launcher for Windows";
                        string descriptionLauncher = "Soapbox Race World";

                        bool removeFirewallRule = false;
                        bool firstTimeRun = false;

                        if (FileSettingsSave.FirewallLauncherStatus == "Not Excluded" || FileSettingsSave.FirewallLauncherStatus == "Turned Off" || FileSettingsSave.FirewallLauncherStatus == "Service Stopped" || FileSettingsSave.FirewallLauncherStatus == "Unknown")
                        {
                            firstTimeRun = true;
                            FileSettingsSave.FirewallLauncherStatus = "Excluded";
                        }
                        else if (FileSettingsSave.FirewallLauncherStatus == "Reset")
                        {
                            removeFirewallRule = true;
                            FileSettingsSave.FirewallLauncherStatus = "Not Excluded";
                        }

                        /* Inbound & Outbound */
                        FirewallHelper.DoesRulesExist(removeFirewallRule, firstTimeRun, nameOfLauncher, localOfLauncher, groupKeyLauncher, descriptionLauncher, FirewallProtocol.Any);
                        FirewallHelper.DoesRulesExist(removeFirewallRule, firstTimeRun, nameOfUpdater, localOfUpdater, groupKeyLauncher, descriptionLauncher, FirewallProtocol.Any);
                    }
                    else if (FirewallManager.IsServiceRunning == true && FirewallHelper.FirewallStatus() == false)
                    {
                        FileSettingsSave.FirewallLauncherStatus = "Turned Off";
                    }
                    else
                    {
                        FileSettingsSave.FirewallLauncherStatus = "Service Stopped";
                    }

                    FileSettingsSave.SaveSettings();
                }

                /* Windows 7 Fix */
                if (WindowsProductVersion.CachedWindowsNumber == 6.1 && string.IsNullOrEmpty(FileSettingsSave.Win7UpdatePatches))
                {
                    if (ManagementSearcher.GetInstalledHotFix("KB3020369") == false || ManagementSearcher.GetInstalledHotFix("KB3125574") == false)
                    {
                        String messageBoxPopupKB = String.Empty;
                        messageBoxPopupKB = "Hey Windows 7 User, we've detected a potential issue of some missing Updates that are required.\n";
                        messageBoxPopupKB += "We found that these Windows Update packages are showing as not installed:\n\n";

                        if (ManagementSearcher.GetInstalledHotFix("KB3020369") == false) messageBoxPopupKB += "- Update KB3020369\n";
                        if (ManagementSearcher.GetInstalledHotFix("KB3125574") == false) messageBoxPopupKB += "- Update KB3125574\n";

                        messageBoxPopupKB += "\nAditionally, we must add a value to the registry:\n";

                        messageBoxPopupKB += "- HKLM/SYSTEM/CurrentControlSet/Control/SecurityProviders\n/SCHANNEL/Protocols/TLS 1.2/Client\n";
                        messageBoxPopupKB += "- Value: DisabledByDefault -> 0\n\n";

                        messageBoxPopupKB += "Would you like to add those values?";
                        DialogResult replyPatchWin7 = MessageBox.Show(null, messageBoxPopupKB, "SBRW Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                        if (replyPatchWin7 == DialogResult.Yes)
                        {
                            RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client");
                            key.SetValue("DisabledByDefault", 0x0);

                            MessageBox.Show(null, "Registry option set, Remember that the changes may require a system reboot to take effect", "SBRW Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            FileSettingsSave.Win7UpdatePatches = "1";
                        }
                        else
                        {
                            MessageBox.Show(null, "Roger that, There may be some issues connecting to the servers.", "SBRW Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            FileSettingsSave.Win7UpdatePatches = "0";
                        }

                        FileSettingsSave.SaveSettings();
                    }
                }

                /* Check if Redistributable Packages are Installed */
                await Redistributable.CheckAsync();
            }

            if (!File.Exists("servers.json"))
            {
                try
                {
                    File.WriteAllText("servers.json", "[]");
                }
                catch { /* ignored */ }
            }

            if (!string.IsNullOrEmpty(FileSettingsSave.GameInstallation))
            {
                var linksPath = Path.Combine(FileSettingsSave.GameInstallation + "\\.links");
                ModNetLinksCleanup.CleanLinks(linksPath);
            }

            if (FileSettingsSave.Proxy == "0")
            {
                Log.Info("PROXY: Starting Proxy (From Startup)");
                ServerProxy.Instance.Start();
            }

            /* Sets up Theming */
            Theming.CheckIfThemeExists();
            /* Sets Up Langauge List */
            LanguageListUpdater.GetList();
            /* Check If Updater Exists or Requires an Update */
            await UpdaterExecutable.CheckAsync();
            /* Check Up to Date Certificate Status */
            await CertificateStore.LatestAsync();
            /* Check Latest Launcher Version */
            await LauncherUpdateCheck.Latest();
            /* Check ServerList Status */
            await Task.Run(() => ServerListUpdater.GetList());

            /* Close Splash Screen */
            if (IsSplashScreenLive == true)
            {
                _SplashScreen.Abort();
            }

            /* Check If Launcher Failed to Connect to any APIs */
            if (VisualsAPIChecker.WOPLAPI == false)
            {
                DialogResult restartAppNoApis = MessageBox.Show(null, "There's no internet connection, Launcher might crash \n \nClick Yes to Close Launcher \nor \nClick No Continue", "GameLauncher has Stopped, Failed To Connect To API", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (restartAppNoApis == DialogResult.No)
                {
                    MessageBox.Show("Good Luck... \n No Really \n ...Good Luck", "GameLauncher Will Continue, When It Failed To Connect To API");
                    Log.Warning("PRE-CHECK: User has Bypassed 'No Internet Connection' Check and Will Continue");
                }

                if (restartAppNoApis == DialogResult.Yes)
                {
                    Process.GetProcessById(Process.GetCurrentProcess().Id).Kill();
                }
            }

            DiscordLauncherPresense.Start("Start Up", "540651192179752970");

            Log.Visuals("CORE: Starting MainScreen");
            Application.Run(new MainScreen());
        }
    }
}