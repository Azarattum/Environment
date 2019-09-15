using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Environment.Modules
{
    class ComposerModule : IModule
    {
        public readonly string Name;
        public readonly string Postfix;
        public string Version;
        private readonly string Url = "https://packagist.org/packages/";

        string IModule.Version => Version;
        string IModule.Name => Name;
        string IModule.Type => "composer";

        public ComposerModule(string name)
        {
            Name = name.Split('/')[0];
            Postfix = name.Split('/')[1];
            Version = "0.0";
            Url += name;
        }

        public void Install()
        {
            Process composer = new Process();
            if (!File.Exists("../composer/composer.cmd"))
            {
                Utils.Log($"Unable to install {Name}!", ConsoleColor.Red);
                Utils.Log("Composer is not installed!\n", ConsoleColor.DarkRed);
                return;
            }

            Utils.Log($"Requesting \"{new Uri(Url).Host}\"...", ConsoleColor.DarkGray);
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("user-agent",
                    "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                string page = client.DownloadString(Url);

                //Find version
                Regex regex = new Regex("<span class=\"version-number\">v(([0-9]+[.]?)+)</span>");
                MatchCollection matches = regex.Matches(page);
                bool isNewVersion = false;
                foreach (Match match in matches)
                {
                    Version v = new Version(match.Groups[1].Value);
                    if (v.CompareTo(new Version(Version)) > 0)
                    {
                        Version = v.ToString();
                        isNewVersion = true;
                    }
                }
                if (!isNewVersion)
                {
                    Utils.Log($"The latest version of {Name} ({Version}) is already installed.\n", ConsoleColor.Blue);
                    return;
                }
            }
            Utils.Log($"Found version {Version} of {Name}.", ConsoleColor.Cyan);

            Utils.Log($"Adding package {Name} to Composer...", ConsoleColor.DarkCyan);
            composer.StartInfo.FileName = @"../composer/composer.cmd";
            composer.StartInfo.Arguments = $"global require {Name}/{Postfix}";
            composer.StartInfo.RedirectStandardOutput = true;
            composer.StartInfo.RedirectStandardError = true;
            composer.StartInfo.UseShellExecute = false;
            composer.StartInfo.CreateNoWindow = true;

            composer.Start();

            string line = "";
            int all = 0, done = 0;
            while ((line = composer.StandardError.ReadLine()) != null)
            {
                Regex regex = new Regex("Package operations: ([0-9]+) installs");
                Match match = regex.Match(line);
                if (match.Length > 0)
                {
                    all = int.Parse(match.Groups[1].Value);
                }

                if (line.Contains("- Installing")) done++;

                if (all > 0)
                {
                    Console.CursorVisible = false;
                    int percents = (int)Math.Round(((float)done / (float)all) * 100);
                    Console.Write("[DOWNLOAD]: [" + new string('=', (int)Math.Floor((double)percents / 4)) +
                            new string(' ', (int)Math.Ceiling((double)(100 - percents) / 4)) + $"] {percents}%    \r");
                }
            }
            
            composer.WaitForExit();
            Console.CursorVisible = true;

            string errors = composer.StandardError.ReadToEnd();
            if (errors != null && errors.Contains("failed"))
            {
                throw new Exception(errors);
            }

            Utils.Log($"Module {Name} installed!" + new string(' ', 15) + "\n", ConsoleColor.DarkGreen);
        }
    }
}
