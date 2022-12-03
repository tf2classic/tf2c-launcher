//#define FAKE_TREE_FILE
//#define PRE_RELEASE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Reflection;

namespace TF2CLauncher
{
    public partial class MainForm : Form
    {
        string executableFilename = Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location);
        int currentVersionNumber;
        const String serverURL = "https://launcher.tf2classic.com/tf2classic-public/";

        private Steam steam;

        public MainForm()
        {
            InitializeComponent();


            String imageUrl = serverURL + "header.png" + "?random=" + new Random().Next().ToString();
            if (CheckForUrl(imageUrl))
                pictureBox1.ImageLocation = imageUrl;


            steam = new Steam();

            //Remove old launcher versions.
            if (File.Exists(executableFilename + ".old"))
            {
                File.Delete(executableFilename + ".old");
            }

            //Remove the .remove file if present
            //this prevents anyone malicious from distributing a version of the game that has a .remove that deletes
            //certain files and potentially harming the users computer when presssing the update button.
            String rmFile = getGamePath() + @"\.remove";
            if (File.Exists(rmFile))
            {
                File.Delete(rmFile);
            }

            tabControl1.Selecting += new TabControlCancelEventHandler(tabControl1_Selecting);

            try
            {
                StreamReader sr = new StreamReader(getGamePath() + @"\config.txt");
                sdkPathBox.Text = sr.ReadLine();
                paramBox.Text = sr.ReadLine();
                sr.Close();
            }
            catch (Exception sre)
            {
                detectSDK();
            }

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            versionLabel.Text = "Launcher version " + version.Major + "." + version.Minor;
#if PRE_RELEASE
            versionLabel.ForeColor = Color.Red;
            versionLabel.Text += " [pre-release]";
#endif
            downloadTreeFile();
        }

        public static bool CheckForUrl(String url)
        {
            try
            {
                using (var client = new WebClient())
                using (client.OpenRead(url))
                    return true;
            }
            catch
            {
                return false;
            }
        }

        public void downloadFile(WebClient client,string url, string filename, bool noCaching = false)
        {
            string urlWFile = string.Concat(url , filename);
            
            if( noCaching )
            {
                Random random = new Random();
                urlWFile = urlWFile + "?random=" + random.Next().ToString();
            }
            Uri uri = new Uri(urlWFile);
            client.DownloadFileAsync(uri, filename);
        }

        private void downloadTreeFile()
        {
            try
            {
                WebClient webClient = new WebClient();
                webClient.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(downloadFileCompletedTree);
#if FAKE_TREE_FILE
                downloadFileCompletedTree(null, null);
#else
                downloadFile(webClient, serverURL, "tree.txt", true);
#endif
                webClient.Dispose();
            }
            catch (Exception e)
            {
                statusPanel.Text = "problem getting tree";
            }
        }

        public void DownloadFile(Uri url, string outputFilePath)
        {
            const int BUFFER_SIZE = 16 * 1024;
            using (var outputFileStream = File.Create(outputFilePath, BUFFER_SIZE))
            {
                var req = WebRequest.Create(url);
                using (var response = req.GetResponse())
                {
                    using (var responseStream = response.GetResponseStream())
                    {
                        var buffer = new byte[BUFFER_SIZE];
                        int bytesRead;
                        do
                        {
                            bytesRead = responseStream.Read(buffer, 0, BUFFER_SIZE);
                            outputFileStream.Write(buffer, 0, bytesRead);
                        } while (bytesRead > 0);
                    }
                }
            }
        }

        void downloadFileCompletedTree(object sender, AsyncCompletedEventArgs e)
        {
            if(e != null && e.Error != null)
            {
                statusPanel.ForeColor = Color.Red;
                statusPanel.Text = "The backend server could not be reached.";
                return;
            }

            tree = new Tree(serverURL);

            updateListBox();

            if (!File.Exists(getGamePath() + @"\gameinfo.txt"))
            {
                launchButton.Enabled = false;
                updateButton.Enabled = false;

                placementWarning();

                // The launcher is not correctly positioned, so don't display any popup to update the game or anything.
                return;
            }

            if (fetchLocalVersionNumber() && checkForUpdates())
            {
                String updateName = getLatestTreeVersionString(1);
                if (updateName == "")
                    updateName = "A new patch";

                statusPanel.Text = "A new patch is available for download!";
                updateButton.Enabled = true;
                updateButton.Text = "Update available";

                // There is an update available, the update process is now handled by tf2cdownloader so we show a popup.
                new UpdatePopup().ShowDialog();
            }
            else
            {
                statusPanel.Text = "Your build appears to be up to date";
            }
        }

        private void placementWarning()
        {
            statusPanel.ForeColor = Color.Red;
            statusPanel.Text = "Incorrect launcher placement";
            MessageBox.Show("The launcher has to be placed in the root directory of the mod in order to launch or update the game!\n\nIf you have moved the launcher it is advised to move it back into the tf2classic folder. Try creating a shortcut if you want to run it from somewhere else.", "Incorrect launcher placement",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        bool checkForUpdates()
        {
            return getLatestTreeVersion() > getLocalVersionNumber();
        }

        String getLatestTreeVersionString( int field = 0 )
        {
            StreamReader sr = new StreamReader("tree.txt");

            String line;
            String[] lastLine = new String[] { "0" };
            while ((line = sr.ReadLine()) != null)
                lastLine = line.Split(';');

            sr.Close();

            if( field < lastLine.Length)
                return lastLine[field];

            return ""; 
        }

        int getLatestTreeVersion( )
        {
            Debug.WriteLine(getLatestTreeVersionString());
            return Int32.Parse(getLatestTreeVersionString());
        }

        int getNextTreeVersion()
        {
            List<int> tree = new List<int>();

            String line;
            StreamReader sr = new StreamReader("tree.txt");
            //sr.ReadLine(); //Skip first line
            while ((line = sr.ReadLine()) != null)
            {
                tree.Add(Int32.Parse(line.Split(';')[0]));
            }
            sr.Close();

           int currentTreePosition = tree.FindIndex(x => x == getLocalVersionNumber());
           return tree[currentTreePosition + 1];
        }

        public List<Patch> getTree()
        {
            return new Tree(serverURL).getTree();
        }

        public void updateListBox()
        {
            List<Patch> tree = getTree();

            listBox1.DrawMode = DrawMode.OwnerDrawVariable;
            listBox1.MeasureItem += lstChoices_MeasureItem;
            listBox1.DrawItem += lstChoices_DrawItem;

            for (int i = tree.Count - 1; i > 0; i--)
            {
                String label = tree[i].getName();

                if(tree[i].getPublishDate() !=  "")
                    label += "\n" + "Released on " + tree[i].getPublishDate();

                int childCount = tree[i].getChildCount();
                if (childCount > 1)
                    label += "\n" + "Consists of " + childCount + " chunks, only displaying the first";

                if (tree[i].getParentVersion() < 0 || tree[i].getParentVersion() == tree[i].getVersion())
                    listBox1.Items.Add(label);
            }

            //Task.Factory.StartNew(() => update());
        }

        private int ItemMargin = 5;
        private void lstChoices_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            ListBox lst = sender as ListBox;
            string txt = (string)lst.Items[e.Index];

            SizeF txt_size = e.Graphics.MeasureString(txt, this.Font);

            e.ItemHeight = (int)txt_size.Height + 2 * ItemMargin;
            e.ItemWidth = (int)txt_size.Width;
        }

        private void lstChoices_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;

            ListBox lst = sender as ListBox;
            string txt = (string)lst.Items[e.Index];

            using (SolidBrush bBrush = new SolidBrush(Color.Black))
            using(SolidBrush wBrush = new SolidBrush(Color.White))
            using (SolidBrush gBrush = new SolidBrush(Color.FromArgb(130, 135, 144)))
            {
                e.Graphics.FillRectangle(wBrush, new RectangleF(new PointF(e.Bounds.X,e.Bounds.Y),new SizeF(e.Bounds.Width,e.Bounds.Height)));
                e.Graphics.DrawString(txt, this.Font, bBrush, e.Bounds.Left, e.Bounds.Top + ItemMargin);
                e.Graphics.FillRectangle(gBrush, new RectangleF(new PointF(e.Bounds.X, e.Bounds.Y + e.Bounds.Height - 1), new SizeF(e.Bounds.Width, 1)));
            }
        }

        public void progress(int percentage)
        {
            updateButton.Text = "Updating(" + percentage + "%)";
            updateProgressBar.Value = percentage;
        }

        public void update()
        {
            updateButton.Enabled = false;
            launchButton.Enabled = false;

            Tree tree = new Tree(serverURL);

            bool success = true;

            while (currentVersionNumber < tree.getLatestVersionNumber())
            {
                Patch nextPatch = tree.getNextPatchFromVersion(currentVersionNumber);

                statusPanel.ForeColor = Color.Black;
                statusPanel.Text = "Installing patch " + nextPatch.getVersion() + " \"" + nextPatch.getName() + "\"";

                Patch.InstallError installErr = nextPatch.install(progress, getGamePath());
                if (installErr != Patch.InstallError.INSTALL_OK)
                {
                    if (installErr == Patch.InstallError.HASH_ERROR)
                        MessageBox.Show("The hash of patch \"" + nextPatch.getName() + "\" did not match!", "An error occurred!", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    success = false;
                    break;
                }

                currentVersionNumber++;
                saveLocalVersionNumber();

                /* After installing a patch we can now check if there is any new version of the launcher in place. 
                 * If so then we will attempt to install that update. This makes sure that we have the correct version before installing
                 * other patches that depend upon certain features in this launcher build.*/
                applyLauncherUpdates();
            }

            if (success)
            {
                statusPanel.ForeColor = Color.Black;
                statusPanel.Text = "Game updated successfully!";
            }
            else
            {
                statusPanel.ForeColor = Color.Red;
                statusPanel.Text = "A problem occurred while updating, cancelled update.";
            }

            updateButton.Enabled = !success;
            updateButton.Text = "Update";
            launchButton.Enabled = true;
            updateProgressBar.Value = 0;
        }

        int getLocalVersionNumber()
        {
            return currentVersionNumber;
        }

        bool fetchLocalVersionNumber()
        {
            try
            {
                StreamReader sr = new StreamReader(getGamePath() + @"\rev.txt");
                String line = sr.ReadLine();
                sr.Close();

                currentVersionNumber = Int32.Parse(line);


                if (currentVersionNumber < tree.getEarliestVersionNumber())
                {
                    MessageBox.Show("Illegal version number \"" + currentVersionNumber + "\", as a result you will not be able to update the game!", "An error occurred!",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                    updateButton.Enabled = false;
                    return false;
                }
            }
            catch( IOException e)
            {
                if (!File.Exists(getGamePath() + @"\rev.txt"))
                {
                    currentVersionNumber = tree.getEarliestVersionNumber();
                    saveLocalVersionNumber();
                }
            }
            return true;
        }

        void saveLocalVersionNumber()
        {
            StreamWriter sw = new StreamWriter(getGamePath() + @"\rev.txt");
            sw.WriteLine(currentVersionNumber);
            sw.Close();
        }

        Tree tree;

        private void updateButton_Click(object sender, EventArgs e)
        {
            new UpdatePopup().ShowDialog();
            /*statusPanel.ForeColor = Color.Black;
            statusPanel.Text = "Updating...";

            updateProgressBar.Maximum = 100;
            updateProgressBar.Value = 0;

            if (!File.Exists(getGamePath() + @"\gameinfo.txt"))
            {
                statusPanel.ForeColor = Color.Red;
                statusPanel.Text = "This application must be placed in the root directory of the mod to apply updates!";
                return;
            }

            Task.Factory.StartNew(() => update()); //New method.*/
        }

        void applyLauncherUpdates()
        {
            if (File.Exists(executableFilename + ".updated"))
            {
                File.Move(executableFilename, executableFilename + ".old");
                File.Move(executableFilename + ".updated", executableFilename);

                while (!File.Exists(executableFilename))
                {
                    Thread.Sleep(50);
                }

                DialogResult result = MessageBox.Show("An update to the launcher has been installed.\nWould you like to restart the launcher?", "An update to the launcher has been installed.", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if(result == DialogResult.Yes)
                {
                    Process.Start(executableFilename);
                    Environment.Exit(0);
                }
            }
        }

        private void launchGame()
        {
            string command = "-steam -game \"" + getGamePath() + "\" " + paramBox.Text;
            try
            {
                if (steam.isSteamInstalled())
                {
                    HashSet<String> libraryFolders = steam.getLibraryFolders();
                    InstallationStatus status = steam.getAppIdStatus(243750, libraryFolders);

                    if (!status.isInstalled())
                    {
                        Process.Start("steam://install/243750");
                        statusPanel.ForeColor = Color.Red;
                        statusPanel.Text = "Source SDK Base 2013 Multiplayer must be installed to run the game.";
                        return;
                    }
                    /*else
                    {
                        if (status.isUpdating())
                        {
                            statusPanel.ForeColor = Color.Red;
                            statusPanel.Text = "Source SDK Base 2013 Multiplayer is still updating or being installed.";
                            return;
                        }
                    }*/
                }

                if (!sdkPathBox.Text.EndsWith("hl2.exe") || !File.Exists(sdkPathBox.Text))
                {
                    statusPanel.ForeColor = Color.Red;
                    statusPanel.Text = "The configured sdk path has to be set to run \"hl2.exe\"!";

                    DialogResult result = MessageBox.Show("Your sdk path is configured incorrectly.\nWould you like to try automatically detect this location?", "SDK Path configuration error", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        detectSDK();
                        saveConfiguration();

                        statusPanel.ForeColor = Color.Black;
                        statusPanel.Text = "Configuration updated, retrying launch...";
                        launchGame();
                    }

                    return;
                }

                if (!File.Exists(getGamePath() + @"\gameinfo.txt"))
                {
                    statusPanel.ForeColor = Color.Red;
                    statusPanel.Text = "This application must be placed in the root directory of the mod in order to function correctly!";
                    return;
                }

                /*Mutex sourceEngineMutex;
                Mutex.TryOpenExisting("hl2_singleton_mutex", out sourceEngineMutex);

                if (sourceEngineMutex != null)
                {
                    statusPanel.ForeColor = Color.Red;
                    statusPanel.Text = "Only one instance of the game can be running at one time.";
                    return;
                }*/

                Process tf2c = new Process();
                tf2c.StartInfo.FileName = sdkPathBox.Text;
                tf2c.StartInfo.Arguments = command;

                if (tf2c.Start())
                {
                    Dispose();
                    Close();
                }
            }
            catch (Exception launchException)
            {
                statusPanel.ForeColor = Color.Red;
                statusPanel.Text = "A Problem occurred while launching the game!";
            }
        }

        private void launchButton_Click(object sender, EventArgs e)
        {
            launchGame();
        }

        private void sdkBrowseButton_Click(object sender, EventArgs e)
        {
            if (fileBrowseDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.sdkPathBox.Text = fileBrowseDialog.FileName;
            }
        }

        void tabControl1_Selecting(object sender, TabControlCancelEventArgs e)
        {
            saveConfiguration();
        }

        public void saveConfiguration()
        {
            try
            {
                StreamWriter sw = new StreamWriter(getGamePath() + @"\config.txt");
                sw.WriteLine(sdkPathBox.Text);
                sw.WriteLine(paramBox.Text);
                sw.Close();
            }
            catch (Exception swe)
            {
                statusPanel.ForeColor = Color.Red;
                statusPanel.Text = "A Problem occurred while saving the configuration!";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            detectSDK();
        }

        private void detectSDK(Boolean updatePathBox = true)
        {
            if (steam.isSteamInstalled())
            {
                HashSet<String> libraryFolders = steam.getLibraryFolders();
                InstallationStatus status = steam.getAppIdStatus(243750, libraryFolders);

                if (!status.isInstalled())
                {
                    Process.Start("steam://install/243750");
                }
                else if (updatePathBox)
                {
                    sdkPathBox.Text = status.getInstallationDirectory() + @"\hl2.exe";
                }
            }
        }

        private String getGamePath()
        {
            String path = AppDomain.CurrentDomain.BaseDirectory;
            return AppDomain.CurrentDomain.BaseDirectory.Remove(path.Length - 1);
        }
    }
}
