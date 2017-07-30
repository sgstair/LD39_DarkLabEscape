using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LD39_sgstair
{
    class FileDialog
    {
        public static string GetOpenFilename(string title, string extension, string description)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = title;
            ofd.DefaultExt = "." + extension;
            ofd.Filter = description + "|*." + extension;
            ofd.CheckFileExists = true;

            bool? result = ofd.ShowDialog();

            if (result == true)
            {
                return ofd.FileName;
            }
            return null;
        }

        public static string GetSaveFilename(string title, string extension, string description)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = title;
            sfd.DefaultExt = "." + extension;
            sfd.Filter = description + "|*." + extension; ;

            bool? result = sfd.ShowDialog();

            if (result == true)
            {
                return sfd.FileName;
            }
            return null;

        }
    }
}
