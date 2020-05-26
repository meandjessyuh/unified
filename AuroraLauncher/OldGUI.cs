﻿using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using static System.Environment;

namespace AuroraLauncher
{
    public partial class OldGUI : Form
    {
        class Installed
        {
            public class Installation
            {
                public string InstallLocation { get; set; }

                public string AppName { get; set; }
                public string AppVersion { get; set; }
            }

            public Installation[] InstallationList { get; set; }
        }

        static string _installedPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), "Epic\\UnrealEngineLauncher\\LauncherInstalled.dat");

        static Process _clientProcess;
        /// <summary>
        /// 0 = None, 1 = BattlEye, 2 = EasyAntiCheat
        /// </summary>
        static byte _clientAnticheat;

        public OldGUI()
        {
            InitializeComponent();

#if FDEV
            Text += " (built for FDev)";

            linkLabelDiscord.Text = "Join our Discord.";
            linkLabelDiscord.Visible = true;
#endif

            if (!File.Exists(_installedPath))
                textBoxFortnitePath.Text = "Couldn't find Fortnite path, please use the \"...\" button to specify your Fortnite path!";
            else
            {
                var installed = JsonConvert.DeserializeObject<Installed>(File.ReadAllText(_installedPath));

                foreach (Installed.Installation installation in installed.InstallationList)
                {
                    if (installation.AppName == "Fortnite")
                        textBoxFortnitePath.Text = installation.InstallLocation;
                }
            }
        }

        private void linkLabelDiscord_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
#if FDEV
            Process.Start("https://discord.gg/fdev");
#endif
        }

        private bool IsValidPath(string path)
        {
            var drive = new Regex(@"^[a-zA-Z]:\\$");
            if (!drive.IsMatch(path.Substring(0, 3)))
                return false;

            var invalidCharacters = new string(Path.GetInvalidPathChars());
            invalidCharacters += @":/?*" + "\"";

            var badCharacter = new Regex("[" + Regex.Escape(invalidCharacters) + "]");
            if (badCharacter.IsMatch(path.Substring(3, path.Length - 3)))
                return false;

            return true;
        }

        private void buttonLaunch_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBoxUsername.Text) || textBoxUsername.Text.Length < 3)
            {
                MessageBox.Show("Username cannot be empty or below 3 characters.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            textBoxUsername.Text = Regex.Replace(textBoxUsername.Text, "[^a-zA-Z0-9_.]+", string.Empty, RegexOptions.Compiled);

            // Sigh...
            try
            {
                if (!IsValidPath(textBoxFortnitePath.Text))
                {
                    MessageBox.Show("Invalid Fortnite path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            catch
            {
                MessageBox.Show("Invalid Fortnite path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var clientPath = Path.Combine(textBoxFortnitePath.Text, $"FortniteGame\\Binaries\\Win64\\{Build.ClientExecutable}");

            if (!File.Exists(clientPath))
            {
                MessageBox.Show($"\"{Build.ClientExecutable}\" was not found, please make sure it exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var nativePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Build.ClientNative);

            if (!File.Exists(nativePath))
            {
                MessageBox.Show($"\"{Build.ClientNative}\" was not found, please make sure it exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var formattedArguments = $"-AUTH_LOGIN=\"{textBoxUsername.Text}@unused.com\" -AUTH_PASSWORD=unused -AUTH_TYPE=epic";

            _clientAnticheat = 2; // TODO: Add to launcher.

            if (_clientAnticheat == 0) // None
                formattedArguments += $" {Build.ClientArguments} -noeac -nobe -fltoken=none";
            else if (_clientAnticheat == 1) // BattlEye
                formattedArguments += $" {Build.ClientArguments} -noeac -fromfl=be -fltoken={Build.BEToken}";
            else if (_clientAnticheat == 2) // EasyAntiCheat
                formattedArguments += $" {Build.ClientArguments} -nobe -fromfl=eac -fltoken={Build.EACToken}";

            _clientProcess = new Process
            {
                StartInfo = new ProcessStartInfo(clientPath, formattedArguments)
                {
                    UseShellExecute = false,

                    RedirectStandardOutput = true,

                    CreateNoWindow = false
                }
            };

            _clientProcess.Start();

            var clientHandle = Win32.OpenProcess(Win32.PROCESS_CREATE_THREAD | Win32.PROCESS_QUERY_INFORMATION |
                Win32.PROCESS_VM_OPERATION | Win32.PROCESS_VM_WRITE | Win32.PROCESS_VM_READ, false, _clientProcess.Id);

            var loadLibrary = Win32.GetProcAddress(Win32.GetModuleHandle("kernel32.dll"), "LoadLibraryA");

            var size = (uint)((nativePath.Length + 1) * Marshal.SizeOf(typeof(char)));
            var address = Win32.VirtualAllocEx(clientHandle, IntPtr.Zero,
                size, Win32.MEM_COMMIT | Win32.MEM_RESERVE, Win32.PAGE_READWRITE);

            Win32.WriteProcessMemory(clientHandle, address,
                Encoding.Default.GetBytes(nativePath), size, out UIntPtr bytesWritten);

            Win32.CreateRemoteThread(clientHandle, IntPtr.Zero, 0, loadLibrary, address, 0, IntPtr.Zero);
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialogBrowse.ShowDialog() == DialogResult.OK)
                textBoxFortnitePath.Text = folderBrowserDialogBrowse.SelectedPath;
        }

        private void GUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_clientProcess != null)
            {
                if (!_clientProcess.HasExited)
                {
                    MessageBox.Show("You cannot close Aurora Launcher while Fortnite is running!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    e.Cancel = true;
                }
            }
        }

        private void buttonInfo_Click(object sender, EventArgs e)
        {
#if FDEV
            string text = "Having problems using FDev? Make sure you follow these guidelines:\n" +
                "- Your Fortnite is up-to-date.\n" +
                "- Your username does not contain special characters.\n\n" +
                "If you still seem to have problems, then please join our Discord server: https://discord.gg/fdev";

            MessageBox.Show(text, "Info");
#endif
        }

        private void GUI_KeyPress(object sender, KeyPressEventArgs e)
        {
            // TODO: Add Enter button support.
        }
    }
}