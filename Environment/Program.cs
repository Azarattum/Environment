using Environment.Modules;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using SevenZipNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Environment
{
    class Program
    {
        static List<IModule> Modules = new List<IModule>();
        static string ProjectsDirectory;

        static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Application.ExecutablePath));
            Console.OutputEncoding = Encoding.UTF8;

            if (!Regex.IsMatch(Application.ExecutablePath, @"^[1-9a-zA-Z.*/\\:']+$"))
            {
                Utils.Log("ATTENTION! Path contains illegal characters, some programs might behave wrongly!", ConsoleColor.DarkYellow);
            }

            if (args.Length == 0)
            {
                Help();
                return;
            }

            switch (args[0].ToLower())
            {
                case "init": Init(); break;
                case "help": Help(); break;
                default:
                    if (Intialize())
                    {
                        switch (args[0].ToLower())
                        {
                            case "install": Install(); break;
                            case "projects": Projects(); break;
                            case "unfold": Unfold(); break;
                            case "fold": Fold(); break;
                            default:
                                Utils.Log("Unrecognised command! Use \"help\" to see available list.\n");
                                break;
                        }
                    } else
                    {
                        Utils.Log("Environment is not intialized! Use \"init\" first!\n", ConsoleColor.DarkRed);
                    }
                    break;
            }
        }


        private static bool Intialize()
        {
            if (!File.Exists("7z.NET.dll") ||
                !File.Exists("7za.exe") ||
                !File.Exists("Newtonsoft.Json.dll") ||
                !File.Exists("config.json") ||
                !File.Exists("modules.json")
            )
            {
                return false;
            } else
            {
                Thread init = new Thread(() =>
                {
                    #region Load modules
                    dynamic modulesData;
                    using (StreamReader modules = new StreamReader("modules.json"))
                    {
                        string json = modules.ReadToEnd();
                        modulesData = JObject.Parse(json);
                    }

                    foreach (dynamic moduleData in modulesData.programs)
                    {
                        if (!moduleData.enabled.Value) continue;
                        if (moduleData.name.ToString().Contains(" "))
                        {
                            Utils.Log($"Invalid name: \"{moduleData.name.ToString()}\"!", ConsoleColor.DarkRed);
                            continue;
                        }

                        Module module = new Module(
                            name: moduleData.name.ToString(),
                            url: moduleData.url.ToString(),
                            pattern: moduleData.pattern.ToString()
                        );
                        module.Version = moduleData.version.ToString();
                        module.Paths = ((JArray)moduleData.path).Select(x => x.ToString()).ToArray();

                        Modules.Add(module);
                    }

                    foreach (dynamic moduleData in modulesData.npm)
                    {
                        if (!moduleData.enabled.Value) continue;
                        if (moduleData.name.ToString().Contains(" "))
                        {
                            Utils.Log($"Invalid name: \"{moduleData.name.ToString()}\"!", ConsoleColor.DarkRed);
                            continue;
                        }

                        NpmModule module = new NpmModule(
                            name: moduleData.name.ToString()
                        );
                        module.Version = moduleData.version.ToString();

                        Modules.Add(module);
                    }

                    foreach (dynamic moduleData in modulesData.composer)
                    {
                        if (!moduleData.enabled.Value) continue;
                        if (moduleData.name.ToString().Split('/').Length != 2)
                        {
                            Utils.Log($"Invalid name: \"{moduleData.name.ToString()}\"!", ConsoleColor.DarkRed);
                            continue;
                        }

                        ComposerModule module = new ComposerModule(
                            name: moduleData.name.ToString()
                        );
                        module.Version = moduleData.version.ToString();

                        Modules.Add(module);
                    }
                    #endregion

                    #region Load config
                    dynamic configData;
                    using (StreamReader config = new StreamReader("config.json"))
                    {
                        string json = config.ReadToEnd();
                        configData = JObject.Parse(json);
                        ProjectsDirectory = Path.GetFullPath(configData["projectsDirectory"].Value);
                    }
                    #endregion
                });
                init.Start();
                init.Join();
                return true;
            }
        }

        private static void Help()
        {
            Utils.Log("Help:", ConsoleColor.Cyan);
            Console.WriteLine("  • " + "help" + "\t\t" + 
                "Shows this menu.");
            Console.WriteLine("  • " + "init" + "\t\t" +
                "Initializes the environment.");
            Console.WriteLine("  • " + "install" + "\t\t" + 
                "Installs and update all modules (modules.json).");
            Console.WriteLine("  • " + "unfold" + "\t\t" +
                "Unfolds the development environment.");
            Console.WriteLine("  • " + "fold" + "\t\t" +
                "Folds the development environment back.");
            Console.WriteLine("  • " + "projects" + "\t\t" +
                "Opens projects folder (config.json).");

            Console.WriteLine();
        }

        private static void Install()
        {
            Utils.Log("Starting installation...\n");

            if (Directory.Exists("temp"))
            {
                Utils.Log("Deleting temp files from previous installations...\n");
                Directory.Delete("temp", true);
            }

            foreach (IModule module in Modules)
            {
                try
                {
                    Utils.Log($"Installing {module.Name}...");
                    module.Install();

                    //Update versions
                    JObject data;
                    using (StreamReader modules = new StreamReader("modules.json"))
                    {
                        string json = modules.ReadToEnd();
                        data = JObject.Parse(json);
                    }

                    foreach (dynamic item in data[module.Type])
                    {
                        if (item.name.ToString() == module.Name)
                        {
                            item["version"] = module.Version;
                        }
                    }
                    
                    using (StreamWriter modules = new StreamWriter("modules.json", false))
                    {
                        modules.Write(data.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Utils.Log($"Unable to install {module.Name}!", ConsoleColor.Red);
                    Utils.Log($"An exception occured:\n{ex.ToString()}\n", ConsoleColor.DarkRed);
                }
            }

            Utils.Log("Installation completed!");
        }

        private static void Projects()
        {
            if (Directory.Exists(ProjectsDirectory))
            {
                Process.Start("explorer.exe", ProjectsDirectory);
            }
            else
            {
                Utils.Log("Specified projects directory does not exist! Chage it in config.json!",
                    ConsoleColor.DarkRed);
            }
        }

        private static void Init()
        {
            if (File.Exists("config.json"))
            {
                Utils.Log("The environment is already initialized!\n", ConsoleColor.Red);
                return;
            }

            string path = "./programs/environment";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            Utils.Log("Generating directory tree...");

            Utils.Log("The program will be moved to /programs/environment");

            Utils.Log("Unpacking libraries...");
            File.WriteAllBytes(Path.Combine(path, "7z.NET.dll"), Properties.Resources.zip_lib);
            File.WriteAllBytes(Path.Combine(path, "7za.exe"), Properties.Resources.zip_exe);
            File.WriteAllBytes("7z.NET.dll", Properties.Resources.zip_lib);
            File.WriteAllBytes("7za.exe", Properties.Resources.zip_exe);
            File.WriteAllBytes(Path.Combine(path, "Newtonsoft.Json.dll"), Properties.Resources.json_lib);

            Utils.Log("Creating default \"config.json\" and \"modules.json\"...");
            File.WriteAllText(Path.Combine(path, "config.json"), Encoding.UTF8.GetString(Properties.Resources.config));
            File.WriteAllText(Path.Combine(path, "modules.json"), Encoding.UTF8.GetString(Properties.Resources.modules));

            Utils.Log("Unpacking addons...");
            File.WriteAllBytes(Path.Combine(path, "addons.zip"), Properties.Resources.addons);

            Thread extract = new Thread(() =>
            {
                SevenZipExtractor extractor = new SevenZipExtractor(Path.Combine(path, "addons.zip"));
                extractor.ExtractAll(path);
                File.Delete(Path.Combine(path, "addons.zip"));
            });
            Thread.Sleep(100);
            extract.Start();
            extract.Join();

            Intialize();
            Utils.Log("Creating projects directory...");
            if (!Directory.Exists("projects"))
            {
                Directory.CreateDirectory("projects");
            }

            Utils.Log("Generating shortcuts...");
            if (!Directory.Exists("shortcuts"))
            {
                Directory.CreateDirectory("shortcuts");
            }

            string fold = "@\"../programs/environment/env.exe\" fold";
            File.WriteAllText("shortcuts/fold.bat", fold);
            string unfold = "@\"../programs/environment/env.exe\" unfold";
            File.WriteAllText("shortcuts/unfold.bat", unfold);
            string projects = "@\"../programs/environment/env.exe\" projects";
            File.WriteAllText("shortcuts/projects.bat", projects);
            string install = "@\"../programs/environment/env.exe\" install";
            File.WriteAllText("shortcuts/install.bat", install);

            Utils.Log("Moving the program...");
            File.Copy(Application.ExecutablePath, Path.Combine(path, "env.exe"));

            ProcessStartInfo info = new ProcessStartInfo();
            info.Arguments = "/C timeout /t 1 & choice /C Y /N /D Y /T 3 & Del " +
                           Application.ExecutablePath + " 7z.NET.dll 7za.exe";
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.CreateNoWindow = true;
            info.FileName = "cmd.exe";
            info.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
            Process.Start(info);

            Utils.Log("Done!\n", ConsoleColor.DarkGreen);
        }

        private static void Unfold()
        {
            RegistryKey env = Registry.CurrentUser.OpenSubKey("Environment", true);
            if (env.GetValue("original_path") != null)
            {
                Utils.Log("Environment is already unfolded. Fold it first!\n", ConsoleColor.Red);
                return;
            }

            Utils.Log("Unfolding the environment...");

            string originalPath = (env.GetValue("path") != null) ? env.GetValue("path").ToString() : "";
            

            Utils.Log("Modifying path variable...");
            string modifiedPath = originalPath.TrimEnd(';');

            foreach (IModule module in Modules)
            {
                if (module is Module)
                {
                    Module program = (Module)module;
                    foreach (string path in program.Paths)
                    {
                        string newPath = Path.Combine("..", program.Name.ToLower(), path);
                        newPath = Path.GetFullPath(newPath);
                        if (Directory.Exists(newPath))
                        {
                            modifiedPath += ";" + newPath;
                        }
                    }
                }
            }
            modifiedPath += ";" + Path.GetFullPath(".");

            if (originalPath != modifiedPath)
            {
                env.SetValue("original_path", originalPath);
                env.SetValue("path", modifiedPath);

                var user = EnvironmentVariableTarget.User;
                var process = EnvironmentVariableTarget.Process;
                System.Environment.SetEnvironmentVariable("PATH", modifiedPath);
                System.Environment.SetEnvironmentVariable("PATH", modifiedPath, user);
                System.Environment.SetEnvironmentVariable("PATH", modifiedPath, process);
            }

            Utils.Log("Done!\n", ConsoleColor.DarkGreen);
        }

        private static void Fold()
        {
            RegistryKey env = Registry.CurrentUser.OpenSubKey("Environment", true);
            if (env.GetValue("original_path") == null)
            {
                Utils.Log("Environment is not unfolded. Unfolded it first!\n", ConsoleColor.Red);
                return;
            }

            Utils.Log("Folding the environment...");

            string originalPath = env.GetValue("original_path").ToString();
            string oldPath = env.GetValue("path").ToString();

            Utils.Log("Returning path back to original...");
            env.SetValue("path", originalPath);
            env.DeleteValue("original_path");

            var user = EnvironmentVariableTarget.User;
            var process = EnvironmentVariableTarget.Process;
            System.Environment.SetEnvironmentVariable("PATH", originalPath);
            System.Environment.SetEnvironmentVariable("PATH", originalPath, user);
            System.Environment.SetEnvironmentVariable("PATH", originalPath, process);

            Utils.Log("Done!\n", ConsoleColor.DarkGreen);
        }
    }
}
