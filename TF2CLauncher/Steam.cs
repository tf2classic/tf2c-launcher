using System;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;

namespace TF2CLauncher
{
    /* 
     * A class that makes use of the filesystem to check for installed steam applications and their status.
     * Allows you to check if a certain appid is fully installed or still downloading/updating. 
     */
    class Steam
    {
        private String installationFolder;

        public Steam()
        {
            installationFolder = fetchInstallationFolder();
        }

        public bool isSteamInstalled()
        {
            return installationFolder != null;
        }

        public String getInstallationFolder()
        {
            return installationFolder;
        }
        
        private String fetchInstallationFolder()
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam\");

            //Seems like steam is either not installed or it's a 32bit system.
            if (key == null)
            {
                key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam\");
            }

            //If the key is still null then steam just isn't installed.
            if (key == null) return null;

            return key.GetValue("InstallPath").ToString();    
        }

        public bool startAppId(int appId, String additionalParams)
        {
            String steamExecutable = fetchInstallationFolder() + @"\steam.exe";
            Process launchProcess = new Process();
            launchProcess.StartInfo.FileName = steamExecutable;
            launchProcess.StartInfo.Arguments = "-applaunch " + appId + " " + additionalParams;
            return launchProcess.Start();
        }

        public String getSourcemodsFolder()
        {
            return installationFolder + @"\SteamApps\sourcemods";
        }

        public HashSet<String> getLibraryFolders()
        {
            HashSet<String> folders = new HashSet<String>();
            folders.Add(installationFolder);

            using (StreamReader reader = new StreamReader(installationFolder + @"\SteamApps\libraryfolders.vdf"))
            {
                String line;
                while ((line = reader.ReadLine()) != null)
                {
                    //Regex that matches line that contain a library folder specificiation.
                    Regex regex = new Regex("^( *\t*)*\"path\"( *\t*)*\".*\"$");
                    if (regex.IsMatch(line))
                    {
                        String folder = Regex.Replace(line, "^( *\t*)*\"path\"( *\t*)*", "").Replace("\"", "").Replace(@"\\", "\\");
                        //This check ensures only actual existing folders referenced in the file will be used.
                        if (Directory.Exists(folder) && !folders.Contains(folder))
                        {
                            folders.Add(folder);
                        }
                    }
                }
            }
            return folders;
        }

        public InstallationStatus getAppIdStatus(int appid)
        {
            return getAppIdStatus(appid, getLibraryFolders());
        }

        public InstallationStatus getAppIdStatus(int appid, HashSet<String> libraryFolders)
        {
            foreach (String folder in libraryFolders)
            {
                String filename = folder + "/SteamApps/appmanifest_" + appid + ".acf";
                if (File.Exists(filename))
                {
                    String stateFlags = getKeyValue("StateFlags", filename);
                    String installDir = folder + @"\SteamApps\common\" + getKeyValue("installdir", filename);

                    if (stateFlags == "4") return new InstallationStatus(true, false, installDir); //Flags: 4
                    return new InstallationStatus(true,true, installDir); //Flags: 2, 512, 1024
                }
            }
            return new InstallationStatus(false, false, null);
        }

        private String getKeyValue(String key, String filename)
        {
            using (StreamReader reader = new StreamReader(filename))
            {
                String line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    //Regex that matches line that contain a library folder specificiation.
                    String keyRegx = "^\"" + key + "\"( *\t*)*";
                    String valueRegx = "\".*\"$";

                    Regex regex = new Regex(keyRegx + valueRegx);
                    if (regex.IsMatch(line))
                    {
                        String value = Regex.Replace(line, keyRegx, "").Replace("\"", "").Replace(@"\\", "\\");
                        return value;
                    }
                }
            }
            return null;
        }
    }

    class InstallationStatus
    {
        private bool installed;
        private bool updating;
        private String installationDirectory;

        public InstallationStatus(bool installed, bool updating, String installationDirectory)
        {
            this.installed = installed;
            this.updating = installed ? updating : false; //If it is not installed it cannot be updating so set the flag to false.
            this.installationDirectory = installationDirectory;
        }

        public bool isInstalled()
        {
            return installed;
        }

        public bool isUpdating()
        {
            return updating;
        }

        public String getInstallationDirectory()
        {
            return installationDirectory;
        }
    }
}
