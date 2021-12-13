using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace guildwars2.tools.alternator
{
    [Serializable]
    [DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
    public class Account
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public string Name { get; set; }
        public string? Character { get; set; }
        public string LoginFilePath { get; set; }
        public DateTime LastLogin { get; set; }
        public DateTime LastCollection { get; set; }
        public DateTime CreatedAt { get; set; }

        [NonSerialized] public FileInfo LoginFile;

        public Account(string name, string? character, string loginFilePath)
        {
            Name = name;
            Character = character;
            LoginFilePath = loginFilePath;

            LastLogin = DateTime.MinValue;
            LastCollection = DateTime.MinValue;
            CreatedAt = DateTime.Now;
            LoginFile = new FileInfo(loginFilePath);
        }

        private string DebugDisplay => $"{Name} ({Character}) {LastLogin} {LastCollection}";

        private void SwapLogin(FileInfo gw2LocalDat)
        {
            if (gw2LocalDat.Exists)
            {
                if (gw2LocalDat.LinkTarget != null)
                {
                    gw2LocalDat.Delete();
                }
                else
                {
                    File.Move(gw2LocalDat.FullName, $"{gw2LocalDat.FullName}.bak", true);
                }
            }

            // Symbolic link creation requires process to be Admin
            gw2LocalDat.CreateAsSymbolicLink(LoginFile.FullName);
            Logger.Debug("{0} dat file linked to: {1}", Name, LoginFile.FullName);
        }

        public async Task SwapLoginAsync(FileInfo gw2LocalDat)
        {
            await Task.Run(() =>
            {
                SwapLogin(gw2LocalDat);
            });
            await Task.Delay(200);
        }
    }
}
