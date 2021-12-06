using System;
using System.IO;
using System.Threading.Tasks;

namespace guildwars2.tools.alternator
{
    [Serializable]
    public class Account
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public string Name { get; set; }

        [NonSerialized] public FileInfo LoginFile;
        public DateTime LastSuccess { get; set; }
        public string LoginFilePath { get; set; }

        public Account(string name, string loginFilePath)
        {
            Name = name;
            LoginFilePath = loginFilePath;

            LoginFile = new FileInfo(loginFilePath);
        }

        public void SwapLogin(FileInfo gw2LocatDat)
        {
            if (gw2LocatDat.Exists)
            {
                if (gw2LocatDat.LinkTarget != null)
                {
                    gw2LocatDat.Delete();
                }
                else
                {
                    File.Move(gw2LocatDat.FullName, $"{gw2LocatDat.FullName}.bak", true);
                }
            }

            gw2LocatDat.CreateAsSymbolicLink(LoginFile.FullName);
            //var psi = new ProcessStartInfo("cmd.exe", $@"mklink /J ""{gw2LocatDat.FullName}"" ""{LoginFile.FullName}""")
            //{
            //    CreateNoWindow = true,
            //    UseShellExecute = false
            //};
            //Process.Start(psi).WaitForExit();

            //var res = Native.CreateSymbolicLink(gw2LocatDat.FullName, LoginFile.FullName, SymbolicLink.File);

            Logger.Debug("{0} dat file linked to: {1}", Name, LoginFile.FullName);
        }

        public async Task SwapLoginAsync(FileInfo pathToLogin)
        {

            await Task.Run(() =>
            {
                var destination = Path.Combine(pathToLogin.DirectoryName, $"orig_{pathToLogin.Name}");
                if (!File.Exists(destination)) File.Copy(pathToLogin.FullName, destination, true);

                File.Copy(LoginFile.FullName, pathToLogin.FullName, true);
            });
            await Task.Delay(200);
        }

        public void SwapLoginSymLink(FileInfo pathToLogin)
        {
            if (pathToLogin.Exists)
            {
                if (pathToLogin.LinkTarget == null)
                {
                    // Real file, rename it
                    var destination = Path.Combine(pathToLogin.DirectoryName, $"orig_{pathToLogin.Name}");
                    File.Move(pathToLogin.Name, destination, true);
                    pathToLogin.Refresh();
                }
                else
                {
                    // Symbolic link delete it
                    pathToLogin.Delete();
                }

            }

            pathToLogin.CreateAsSymbolicLink(LoginFile.FullName); // This fails silently :(
            pathToLogin.Refresh();
        }
    }
}
