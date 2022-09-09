using System;
using System.Text;
using System.Net;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace TF2CLauncher
{
    public class Patch
    {
        private int prevVersion;
        private int version;
        private String name;
        private String dateString;
        private String hash;
        private int parentVersion;
        private Tree tree;

        public Patch(Tree tree, int prevVersion, int version, String name, String dateString, String hash, int parentVersion)
        {
            this.tree = tree;
            this.prevVersion = prevVersion;
            this.version = version;
            this.name = name;
            this.dateString = dateString;
            this.hash = hash;
            this.parentVersion = parentVersion;
        }

        public int getPrevVersion()
        {
            return prevVersion;
        }

        public int getVersion()
        {
            return version;
        }

        public String getName()
        {
            return name;
        }

        public String getPublishDate()
        {
            return dateString;
        }

        public String getHash()
        {
            return hash;
        }

        public int getParentVersion()
        {
            return parentVersion;
        }

        public int getChildCount()
        {
            if (parentVersion < 0)
                return 1;

            int count = 0;
            foreach(Patch patch in tree.getTree())
            {
                if (patch.parentVersion == this.version)
                    count++;
            }
            return count;
        }

        public String getFilename()
        {
            return "patch" + getPrevVersion() + "_" + getVersion() + ".zip";
        }

        public override String ToString()
        {
            return "[Patch " + getVersion() + "] " + getName() + (getPublishDate() != "" ? " (" + getPublishDate() + ")" : "");
        }

        public String getInfo()
        {
            return getPrevVersion() + " to " + getVersion() + " " + getName() + " " + getPublishDate() + " " + getFilename() + " " + getHash();
        }

        public void download(Action<int> progress)
        {
            const int BUFFER_SIZE = 16 * 1024;
            using (FileStream outputFileStream = File.Create(getFilename(), BUFFER_SIZE))
            {
                HttpWebRequest req = WebRequest.Create(new Uri(tree.getRepo() + getFilename())) as HttpWebRequest;
                req.KeepAlive = false;
                using (var response = req.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        Int64 fileSize = response.ContentLength;
                        Int64 totalBytesRead = 0;

                        var buffer = new byte[BUFFER_SIZE];
                        int bytesRead;
                        do
                        {
                            bytesRead = responseStream.Read(buffer, 0, BUFFER_SIZE);
                            outputFileStream.Write(buffer, 0, bytesRead);

                            totalBytesRead += bytesRead;

                            int p = (int)((totalBytesRead / (float)fileSize) * 100);
                            progress(p);
                        } while (bytesRead > 0);
                    }
                }
            }
        }

        void removeFiles(String installDir)
        {
            String file = installDir + @"\.remove";

            if (File.Exists(file))
            {
                using (StreamReader reader = new StreamReader(file))
                {
                    String line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        String fileToRemove = installDir + "/" + line;
                        if (File.Exists(fileToRemove))
                        {
                            //listBox1.Items.Add("Removed " + fileToRemove);
                            File.Delete(fileToRemove);
                        }
                        else if (Directory.Exists(fileToRemove))
                        {
                            Directory.Delete(fileToRemove);

                            while (Directory.Exists(fileToRemove))
                            {
                                Thread.Sleep(50);
                            }
                        }
                    }
                }
                File.Delete(file);
            }
        }

        public InstallError install(Action<int> progress, String installDir)
        {
            // Download the patch file.
            download(progress);

            // Verify hash of the patch.
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream fs = File.OpenRead(getFilename()))
            {
                byte[] hashBytes = sha256.ComputeHash(fs);
                //String hashStr = Convert.ToBase64String(hash);
                string hashStr = byteArrayToString(hashBytes);
                Console.Out.WriteLine(hashStr);

                if (hashStr != hash)
                    return InstallError.HASH_ERROR;
            }

            // Extract the patch file.
            using (ZipArchive archive = ZipFile.OpenRead(getFilename()))
            {
                // If there was a problem during extraction the installation was not successful.
                if (!ZipArchiveExtensions.ExtractToDirectory(archive, installDir, progress))
                    return InstallError.EXTRACT_ERROR;
            }

            // Delete the patch file since it has already been extracted.
            File.Delete(getFilename());

            // Remove any files listed for removal within the patch(".remove" file).
            removeFiles(installDir);

            // We got to the end of the installation, it was thus successful.
            return InstallError.INSTALL_OK;
        }

        public enum InstallError
        {
            INSTALL_OK,
            EXTRACT_ERROR,
            HASH_ERROR
        }

        string byteArrayToString(byte[] arrInput)
        {
            int i;
            StringBuilder sOutput = new StringBuilder(arrInput.Length);
            for (i = 0; i < arrInput.Length; i++)
            {
                sOutput.Append(arrInput[i].ToString("X2"));
            }
            return sOutput.ToString().ToLower();
        }
    }
}
