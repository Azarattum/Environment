using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Environment.Modules
{
    class NpmModule : IModule
    {
        public readonly string Name;
        public string Version;

        string IModule.Version => Version;
        string IModule.Name => Name;
        string IModule.Type => "npm";

        public NpmModule(string name)
        {
            Name = name;
            Version = "0.0";
        }

        public void Install()
        {
            Process npm = new Process();
            if (!File.Exists("../nodejs/npm.cmd"))
            {
                Utils.Log($"Unable to install {Name}!", ConsoleColor.Red);
                Utils.Log("NPM is not installed!\n", ConsoleColor.DarkRed);
                return;
            }
            else
            {
                Utils.Log($"Requesting \"npmjs\"...", ConsoleColor.DarkGray);
                npm.StartInfo.FileName = @"../nodejs/npm.cmd";
                npm.StartInfo.Arguments = $"view {Name} version";
                npm.StartInfo.RedirectStandardOutput = true;
                npm.StartInfo.RedirectStandardError = true;
                npm.StartInfo.UseShellExecute = false;
                npm.StartInfo.CreateNoWindow = true;
                npm.Start();
            }

            Version version = null;
            try
            {
                version = new Version(npm.StandardOutput.ReadLine());
                npm.WaitForExit();
            }
            catch
            {
                Utils.Log("Unable to retrive version!\n", ConsoleColor.DarkRed);
                return;
            }
            
            if (version.CompareTo(new Version(Version)) <= 0)
            {
                Utils.Log($"The latest version of {Name} ({Version}) is already installed.\n", ConsoleColor.Blue);
                return;
            }

            Version = version.ToString();
            Utils.Log($"Found version {Version} of {Name}.", ConsoleColor.Cyan);

            npm.StartInfo.Arguments = $"install -g {Name}";
            Utils.Log($"Adding package {Name} to NodeJS...", ConsoleColor.DarkCyan);

            npm.Start();
            npm.WaitForExit();

            string errors = npm.StandardError.ReadToEnd();
            if (errors != null && errors.Contains("ERR!"))
            {
                throw new Exception(errors);
            }

            Utils.Log($"Module {Name} installed!" + new string(' ', 10) + "\n", ConsoleColor.DarkGreen);
        }
    }
}
