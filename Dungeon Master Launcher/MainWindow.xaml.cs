using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Windows;

namespace Dungeon_Master_Launcher
{
    internal enum LauncherStatus : short
    {
        Ready,
        Failed,
        DownloadingGame,
        DownloadingUpdate
    }
    
    public partial class MainWindow
    {
        private const string BuildZipLink = "https://drive.google.com/uc?id=1x9FYc4Y5Z8WsA38qqcTGEt4RtbkYiuuf";
        private const string VersionTxtLink = "https://drive.google.com/uc?id=1a9iadcTlxFeA5o0KRsVIaO4AF4EZyN_u";

        private readonly string _rootPath;
        private readonly string _versionFile;
        private readonly string _gameZip;
        private readonly string _gameExe;
        
        private LauncherStatus _status;
        private LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                PlayButton.Content = _status switch
                {
                    LauncherStatus.Ready => "Play",
                    LauncherStatus.Failed => "Update Failed - Retry",
                    LauncherStatus.DownloadingGame => "Downloading Game...",
                    LauncherStatus.DownloadingUpdate => "Downloading Update...",
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }
        
        public MainWindow()
        {
            InitializeComponent();

            _rootPath = Directory.GetCurrentDirectory();
            _versionFile = Path.Combine(_rootPath, "Version.txt");
            _gameZip = Path.Combine(_rootPath, "Build.zip");
            _gameExe = Path.Combine(_rootPath, "Build", "DungeonMaster.exe");
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            CheckForUpdates();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_gameExe) && Status == LauncherStatus.Ready)
            {
                Process.Start(new ProcessStartInfo(_gameExe)
                {
                    WorkingDirectory = Path.Combine(_rootPath, "Build")
                });
                Close();
            }
            else if (Status == LauncherStatus.Failed)
            {
                CheckForUpdates();
            }
        }

        private void CheckForUpdates()
        {
            if (!File.Exists(_versionFile))
            {
                InstallGameFiles(false);
                return;
            }
            
            var localVersion = new Version(File.ReadAllText(_versionFile));
            VersionLabel.Text = $"v{localVersion.ToString()}";

            try
            {
                var remoteVersion = new Version(new WebClient().DownloadString(VersionTxtLink));
                if (localVersion == remoteVersion)
                {
                    Status = LauncherStatus.Ready;
                }
                else
                {
                    InstallGameFiles(true, remoteVersion);
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show($"Error checking for game updates: {ex}");
            }
        }

        private void InstallGameFiles(bool isUpdate, Version? remoteVersion = null)
        {
            remoteVersion ??= Version.Zero;

            try
            {
                var webClient = new WebClient();

                if (isUpdate)
                {
                    Status = LauncherStatus.DownloadingUpdate;
                }
                else
                {
                    Status = LauncherStatus.DownloadingGame;
                    remoteVersion = new Version(webClient.DownloadString(VersionTxtLink));
                }

                webClient.DownloadFileCompleted += GameDownloadCompletedCallback;
                webClient.DownloadFileAsync(new Uri(BuildZipLink), _gameZip, remoteVersion);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show($"Error installing game files: {ex}");
            }
        }

        private void GameDownloadCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                var buildDirectory = Path.Combine(_rootPath, "Build");
                if (Directory.Exists(buildDirectory))
                {
                    Directory.Delete(buildDirectory, true);
                }
                
                ZipFile.ExtractToDirectory(_gameZip, _rootPath);
                File.Delete(_gameZip);
                
                var remoteVersion = ((Version) e.UserState).ToString();
                File.WriteAllText(_versionFile, remoteVersion);
                VersionLabel.Text = $"v{remoteVersion}";
                
                Status = LauncherStatus.Ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show($"Error finishing download: {ex}");
            }
        }
    }
    
    internal readonly struct Version : IEquatable<Version>
    {
        internal static readonly Version Zero = new(0, 0, 0);

        private readonly ushort _major;
        private readonly ushort _minor;
        private readonly ushort _subminor;

        private Version(ushort major, ushort minor, ushort subminor)
        {
            _major = major;
            _minor = minor;
            _subminor = subminor;
        }

        internal Version(string version)
        {
            var versionStrings = version.Split('.');
            if (versionStrings.Length != 3)
            {
                _major = 0;
                _minor = 0;
                _subminor = 0;
                return;
            }

            _major = ushort.Parse(versionStrings[0]);
            _minor = ushort.Parse(versionStrings[1]);
            _subminor = ushort.Parse(versionStrings[2]);
        }

        public static bool operator ==(Version version1, Version version2)
        {
            return version1._major == version2._major
                   && version1._minor == version2._minor
                   && version1._subminor == version2._subminor;
        }

        public static bool operator !=(Version version1, Version version2)
        {
            return !(version1 == version2);
        }

        public bool Equals(Version obj)
        {
            return this == obj;
        }

        public override bool Equals(object obj)
        {
            return obj != null && Equals((Version) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 3049;
                hash = hash * 5039 + _major.GetHashCode();
                hash = hash * 883 + _minor.GetHashCode();
                hash = hash * 9719 * _subminor.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{_major}.{_minor}.{_subminor}";
        }
    }
}
