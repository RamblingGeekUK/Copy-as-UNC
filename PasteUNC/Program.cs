using System.IO;
using System.Management;

namespace PasteUNC
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            
            string uncPath = GetUNCPath(args[0]);
            Clipboard.SetText(uncPath);
        }

        public static string GetUNCPath(string path)
        {
            if (path.StartsWith(@"\\"))
            {
                return path;
            }

            string driveLetter = path.Substring(0, 2); 
            ManagementObject mo = new ManagementObject();
            mo.Path = new ManagementPath(String.Format("Win32_LogicalDisk.DeviceID='{0}'", driveLetter));

            // DriveType 4 = Network Drive
            if (Convert.ToUInt32(mo["DriveType"]) == 4)
            {
                string uncPath = Convert.ToString(mo["ProviderName"]);

                string relativePath = path.Substring(2);
                string fullPath = uncPath + relativePath;
                return fullPath;
            }
            else
            {
                return path;
            }
        }
    }
}