using System.Diagnostics;
using System.IO.Compression;

namespace BHME_Updater
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var currentPath = Directory.GetCurrentDirectory();
            string currentVersion = "";
            if (File.Exists("Sound Space Hop Editor.exe"))
                currentVersion = FileVersionInfo.GetVersionInfo("Sound Space Hop Editor.exe").FileVersion ?? "";

            var newVersion = CheckVersion();
            var file = $"SSHE{newVersion}.zip";

            if (newVersion != "")
            {
                Console.WriteLine("Starting download...");

                try
                {
                    WebClient.DownloadFile($"https://github.com/TominoCZ/BeatHopEditor/releases/download/{newVersion}/{file}", file);
                    ExtractFile();
                }
                catch
                {
                    Console.WriteLine("Failed to download new editor version");
                    Quit();
                }
            }

            bool IsNewer(string oldVersion, string checkVersion)
            {
                if (string.IsNullOrWhiteSpace(oldVersion))
                    return true;

                string[] oldSplit = oldVersion.Split('.');
                string[] checkSplit = checkVersion.Split('.');

                for (int i = 0; i < oldSplit.Length; i++)
                {
                    string old = oldSplit[i];
                    string check = checkSplit[i];
                    int oldI = int.Parse(old);
                    int checkI = int.Parse(check);

                    if (oldI < checkI)
                        return true;
                    else if (oldI > checkI)
                        return false;
                }

                return false;
            }

            string[] GetOverwrites()
            {
                try
                {
                    List<string> overwrites = new();

                    var overwriteList = WebClient.DownloadString("https://raw.githubusercontent.com/TominoCZ/BeatHopEditor/main/updater_overwrite");
                    var split = overwriteList.Split('\n');
                    var overwriteVersion = "";

                    int i = 0;
                    while (i < split.Length && !string.IsNullOrWhiteSpace(split[i]))
                    {
                        overwrites.Add(split[i]);
                        i++;
                    }

                    for (int j = i; j < split.Length; j++)
                    {
                        var line = split[j];
                        if (j == 0 || string.IsNullOrWhiteSpace(split[j - 1]))
                            overwriteVersion = line;

                        if (!string.IsNullOrWhiteSpace(line) && line != overwriteVersion && IsNewer(currentVersion, overwriteVersion))
                            overwrites.Add(line);
                    }

                    return overwrites.ToArray();
                }
                catch
                {
                    Console.WriteLine("Failed to fetch overwrites");
                    Quit();
                }

                return Array.Empty<string>();
            }

            string CheckVersion()
            {
                try
                {
                    var redirect = WebClient.GetRedirect("https://github.com/TominoCZ/BeatHopEditor/releases/latest");

                    if (!string.IsNullOrWhiteSpace(redirect))
                    {
                        var version = redirect[(redirect.LastIndexOf("/") + 1)..];

                        if (version != currentVersion)
                            return version;
                    }
                }
                catch
                {
                    Console.WriteLine("Failed to check version");
                    Quit();
                }

                return "";
            }

            void KillProcess()
            {
                foreach (var process in Process.GetProcessesByName("Sound Space Hop Editor"))
                    process.Kill();

                Thread.Sleep(500);
            }

            bool IsInOverwrites(string[] list, string fileName)
            {
                foreach (var line in list)
                    if (fileName == line)
                        return true;

                return false;
            }

            void ExtractFile()
            {
                var overwriteList = GetOverwrites();

                Console.WriteLine("Completed, extracting...");

                KillProcess();

                using (ZipArchive archive = ZipFile.OpenRead(file))
                {
                    foreach (var entry in archive.Entries)
                    {
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(currentPath, entry.FullName)) ?? "");
                            entry.ExtractToFile(Path.Combine(currentPath, entry.FullName), IsInOverwrites(overwriteList, entry.FullName));
                        }
                        catch { }
                    }
                }

                Console.WriteLine("Completed, launching...");

                File.Delete(file);
                Process.Start("Sound Space Hop Editor");

                Quit();
            }

            void Quit()
            {
                Thread.Sleep(1500);
                Environment.Exit(0);
            }
        }
    }
}
