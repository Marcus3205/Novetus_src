﻿#region Usings
using System;
using System.IO;
using System.Windows.Forms;
#endregion

#region Icon Loader

public class IconLoader
{
    private OpenFileDialog openFileDialog1;
    private string installOutcome = "";
    public bool CopyToItemDir = false;
    public string ItemDir = "";
    public string ItemName = "";
    public string ItemPath = "";

    public IconLoader()
    {
        openFileDialog1 = new OpenFileDialog()
        {
            FileName = "Select an icon .png file",
            Filter = "Portable Network Graphics image (*.png)|*.png",
            Title = "Open icon .png"
        };
    }

    public void setInstallOutcome(string text)
    {
        installOutcome = text;
    }

    public string getInstallOutcome()
    {
        return installOutcome;
    }

    public void LoadImage()
    {
        string ItemNameFixed = ItemName.Replace(" ", "");
        string dir = CopyToItemDir ? ItemDir + "\\" + ItemNameFixed : GlobalPaths.extradir + "\\icons\\" + GlobalVars.UserConfiguration.PlayerName;

        if (openFileDialog1.ShowDialog() == DialogResult.OK)
        {
            try
            {
                using (Stream str = openFileDialog1.OpenFile())
                {
                    using (Stream output = new FileStream(dir + ".png", FileMode.Create))
                    {
                        byte[] buffer = new byte[32 * 1024];
                        int read;

                        while ((read = str.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, read);
                        }
                    }

                    str.Close();
                }

                if (CopyToItemDir)
                {
                    ItemPath = openFileDialog1.FileName;
                }

                installOutcome = "Icon " + openFileDialog1.SafeFileName + " installed!";
            }
            catch (Exception ex)
            {
                installOutcome = "Error when installing icon: " + ex.Message;
            }
        }
    }
}
#endregion