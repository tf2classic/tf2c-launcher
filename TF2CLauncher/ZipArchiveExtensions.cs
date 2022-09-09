using System;
using System.IO;
using System.IO.Compression;

using System.Windows.Forms;

public static class ZipArchiveExtensions
{
    static int entryAmount = 0;
    static int entryExtractedAmount = 0;

    public static int ExtractToDirectory(this ZipArchive archive, string destinationDirectoryName, bool overwrite, Button b, ProgressBar pBar)
    {
        if (!overwrite)
        {
            archive.ExtractToDirectory(destinationDirectoryName);
            return 0;
        }
        foreach (ZipArchiveEntry file in archive.Entries)
        {
            entryAmount++;
        }

        try { pBar.Maximum = entryAmount; }
        catch (Exception e) { }

        foreach (ZipArchiveEntry file in archive.Entries)
        {
            string completeFileName = Path.Combine(destinationDirectoryName, file.FullName);
            if (file.Name == "")
            {// Assuming Empty for Directory
                Directory.CreateDirectory(Path.GetDirectoryName(completeFileName));
                
                entryExtractedAmount++;

                updateButtonAndBar(b, pBar);

                continue;
            }

            // Before extracting a file make sure that the directory it is in exists, if it does not then we should create one.
            if (!Directory.Exists(Path.GetDirectoryName(completeFileName)) && Path.GetDirectoryName(completeFileName) != "")
                Directory.CreateDirectory(Path.GetDirectoryName(completeFileName));

            try
            {
                file.ExtractToFile(completeFileName, true);
                entryExtractedAmount++;
            }
            catch(IOException e)
            {
                MessageBox.Show("Another process is using \"" + completeFileName + "\".\nMake sure Team Fortress 2 Classic is closed while updating the game. "
                                + "There could possibly be other processes using the file.", "An error occurred while updating the game!",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            updateButtonAndBar(b, pBar);
        }
        return 0;
    }
    private static void updateButtonAndBar(Button b , ProgressBar pBar)
    {
        b.Text = "Extracting (" + entryExtractedAmount + "/" + entryAmount + ")";
        pBar.Value = entryExtractedAmount;
    }

    public static bool ExtractToDirectory(this ZipArchive archive, string destinationDirectoryName, Action<int> progress)
    {
        foreach (ZipArchiveEntry file in archive.Entries)
        {
            entryAmount++;
        }

        /*try { pBar.Maximum = entryAmount; }
        catch (Exception e) { }*/

        foreach (ZipArchiveEntry file in archive.Entries)
        {
            string completeFileName = Path.Combine(destinationDirectoryName, file.FullName);
            if (file.Name == "")
            {// Assuming Empty for Directory
                Directory.CreateDirectory(Path.GetDirectoryName(completeFileName));

                entryExtractedAmount++;

                int p = (int)((entryExtractedAmount / (float)entryAmount) * 100);
                progress(p);

                continue;
            }

            // Before extracting a file make sure that the directory it is in exists, if it does not then we should create one.
            if (!Directory.Exists(Path.GetDirectoryName(completeFileName)) && Path.GetDirectoryName(completeFileName) != "")
                Directory.CreateDirectory(Path.GetDirectoryName(completeFileName));

            try
            {
                file.ExtractToFile(completeFileName, true);
                entryExtractedAmount++;
            }
            catch (IOException e)
            {
                MessageBox.Show("Another process is using \"" + completeFileName + "\".\nMake sure Team Fortress 2 Classic is closed while updating the game. "
                                + "There could possibly be other processes using the file.", "An error occurred while updating the game!",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            int c = (int)((entryExtractedAmount / (float)entryAmount) * 100);
            progress(c);
        }
        return true;
    }
}