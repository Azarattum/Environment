using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using SevenZipNET;

namespace Environment.Modules
{
    class Module : IModule
    {
        private readonly string Url;
        private readonly string Pattern;
        private readonly string ModuleDirectory;
        public readonly string Name;
        public string Version;
        public string[] Paths;

        string IModule.Version => Version;
        string IModule.Name => Name;
        string IModule.Type => "programs";

        public Module(string name, string url, string pattern)
        {
            Name = name;
            Url = url;
            Pattern = pattern;
            ModuleDirectory = Path.GetFullPath($"../{Name.ToLower()}");
            Version = "0.0";
        }

        public void Install()
        {
            if (!Directory.Exists("temp"))
            {
                Directory.CreateDirectory("temp");
            }

            Utils.Log($"Requesting \"{new Uri(Url).Host}\"...", ConsoleColor.DarkGray);
            WebRequest request = WebRequest.Create(Url);
            request.Method = "HEAD";
            WebResponse headers = request.GetResponse();
            string url = headers.ResponseUri.AbsoluteUri;

            using (WebClient client = new WebClient())
            {
                client.Headers.Add("user-agent", 
                    "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                string page = url == Url ? client.DownloadString(Url) : url;

                //Find version
                Regex regex = new Regex(Pattern);
                Match bestMatch = null;
                MatchCollection matches = regex.Matches(page);
                foreach (Match match in matches)
                {
                    Version version = new Version(match.Groups[1].Value);
                    if (version.CompareTo(new Version(Version)) > 0)
                    {
                        Version = version.ToString();
                        bestMatch = match;
                    }
                }

                if (bestMatch == null)
                {
                    Utils.Log($"The latest version of {Name} ({Version}) is already installed.\n", ConsoleColor.Blue);
                    if (Directory.Exists("temp"))
                    {
                        Directory.Delete("temp", true);
                    }
                    return;
                }
                if (Directory.Exists(ModuleDirectory))
                {
                    Utils.Log($"Deleting {Name} old files...", ConsoleColor.DarkYellow);
                    Directory.Delete(ModuleDirectory, true);
                }

                Version = bestMatch.Groups[1].Value;
                string file = bestMatch.Value;

                Utils.Log($"Found version {Version} of {Name}.", ConsoleColor.Cyan);

                //Download file
                if (url == Url)
                {
                    url = file.StartsWith("http") ? file : Path.Combine(Url, file);
                }
                string path = Path.Combine("temp", $"{Name + Path.GetExtension(url)}");

                string filename = Path.GetFileName(file);
                while (!filename.Contains("."))
                {
                    filename = Path.GetFileName(Path.GetDirectoryName(file));
                }

                if (!url.Contains("sourceforge.net"))
                {
                    Utils.Log($"Downloading {filename}...", ConsoleColor.DarkCyan);
                }
                client.Headers.Add("user-agent",
                    "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                client.DownloadFileCompleted += (object sender, AsyncCompletedEventArgs e) =>
                {
                    lock (e.UserState)
                    {
                        Monitor.Pulse(e.UserState);
                    }
                };

                bool init = false;
                client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                {
                    Console.CursorVisible = false;
                    int percents = e.ProgressPercentage;
                    Console.Write((!init ? "[DOWNLOAD]:" : "[INITIALZ]") + " [" + new string('=', (int)Math.Floor((double)percents / 4)) +
                        new string(' ', (int)Math.Ceiling((double)(100 - percents) / 4)) + $"] {percents}%    \r");
                };

                Object syncObject = new Object();
                lock (syncObject)
                {
                    if (url.Contains("sourceforge.net"))
                    {
                        init = true;
                        Utils.Log($"Initializing download...", ConsoleColor.DarkCyan);
                    }

                    client.DownloadFileAsync(new Uri(url), path, syncObject);
                    Monitor.Wait(syncObject);
                    init = false;

                    if (url.Contains("sourceforge.net"))
                    {
                        Utils.Log($"Downloading {filename}..." + new string(' ', 15), ConsoleColor.DarkCyan);
                        File.Delete(path);
                        client.DownloadFileAsync(new Uri(url), path, syncObject);
                        Monitor.Wait(syncObject);
                    }
                    Console.CursorVisible = true;
                }

                Utils.Log($"Unpacking {filename}..." + new string(' ', 15), ConsoleColor.DarkCyan);
                try
                {
                    SevenZipExtractor extractor = new SevenZipExtractor(path);
                    extractor.ProgressUpdated += (int progress) =>
                    {
                        Console.CursorVisible = false;
                        Console.Write("[UNPACKIN]: [" + new string('=', (int)Math.Floor((double)progress / 4)) +
                            new string(' ', (int)Math.Ceiling((double)(100 - progress) / 4)) + $"] {progress}%    \r");
                    };
                    extractor.ExtractAll("temp");
                    if (Directory.GetDirectories("temp").Length == 0
                        && Directory.GetFiles("temp").Length == 1)
                    {
                        throw new InvalidDataException();
                    }

                    Console.CursorVisible = true;
                    File.Delete(path);

                    //Determine the source after unpacking
                    string source;
                    if (Directory.GetDirectories("temp").Length == 1
                        && Directory.GetFiles("temp").Length == 0)
                    {
                        source = Directory.GetDirectories("temp")[0];
                    }
                    else
                    {
                        source = "temp";
                    }

                    Directory.Move(source, ModuleDirectory);
                }
                catch (InvalidDataException)
                {
                    Directory.CreateDirectory(ModuleDirectory);
                    File.Move(path, Path.Combine(ModuleDirectory, Path.GetFileName(url)));
                }

                if (Directory.Exists("temp")) {
                    Directory.Delete("temp", true);
                }
            }

            string addons = Path.Combine("addons", Name.ToLower());
            if (Directory.Exists(addons))
            {
                Utils.Log($"Installing {Name} addons..." + new string(' ', 10), ConsoleColor.DarkCyan);
                foreach (string dirPath in Directory.GetDirectories(addons, "*",
                    SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(dirPath.Replace(addons, ModuleDirectory));
                }

                foreach (string newPath in Directory.GetFiles(addons, "*.*",
                    SearchOption.AllDirectories))
                {
                    File.Copy(newPath, newPath.Replace(addons, ModuleDirectory), true);
                }
            }

            string script = Path.Combine(ModuleDirectory, "env-run.bat");
            if (File.Exists(script))
            {
                Utils.Log($"Evaluating installation script..." + new string(' ', 10), ConsoleColor.DarkCyan);
                ProcessStartInfo info = new ProcessStartInfo(script);
                info.WorkingDirectory = ModuleDirectory;
                info.CreateNoWindow = true;
                info.UseShellExecute = false;
                Process envRun = Process.Start(info);

                envRun.WaitForExit();
                File.Delete(script);
            }

            Utils.Log($"Module {Name} installed!" + new string(' ', 10) + "\n", ConsoleColor.DarkGreen);
        }
    }
}
