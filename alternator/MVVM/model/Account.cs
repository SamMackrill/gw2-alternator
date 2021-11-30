using System.IO;
using System.Threading;

namespace alternator.model
{
    public class Account
    {
        public string Name { get; set; }
        public FileInfo LoginFile { get; set; }

        public Account(string name, FileInfo loginFile)
        {
            Name = name;
            LoginFile = loginFile;
        }

        public void SwapLogin(FileInfo pathToLogin)
        {
            var destination = Path.Combine(pathToLogin.DirectoryName, $"orig_{pathToLogin.Name}");
            if (!File.Exists(destination)) File.Copy(pathToLogin.Name, destination, true);

            File.Copy(LoginFile.FullName, pathToLogin.FullName, true);
            Thread.Sleep(1000);
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
