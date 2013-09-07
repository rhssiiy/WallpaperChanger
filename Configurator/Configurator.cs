using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Configurator
{
    static class Configurator
    {
        public static Categories categories = Categories.None;
        public static Purity purity = Purity.None;
        private static RegistryKey key;

        [STAThread]
        static void Main()
        {
            Registry.CurrentUser.CreateSubKey(@"Software\WallpaperChanger");
            key = Registry.CurrentUser.OpenSubKey(@"Software\WallpaperChanger", true);

            object objCategories = key.GetValue(@"Categories");

            if (objCategories == null)
                categories = Categories.General | Categories.Anime | Categories.HDR;
            else
            {
                try
                {
                    categories = (Categories)Convert.ToByte(objCategories.ToString());
                }
                catch (FormatException)
                {
                    categories = Categories.General | Categories.Anime | Categories.HDR;
                }
            }

            object objPurity = key.GetValue(@"Purity");
            
            if (objPurity == null)
                purity = Purity.Clean;
            else
            {
                try
                {
                    purity = (Purity)Convert.ToByte(objPurity.ToString());
                }
                catch (FormatException)
                {
                    purity = Purity.Clean;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow());
        }

        public static void ApplyChanges()
        {
            key.SetValue(@"Categories", ((byte)categories).ToString());
            key.SetValue(@"Purity", ((byte)purity).ToString());

            try
            {
                Process.Start("wallpaperchanger.exe");
            }
            catch (Win32Exception)
            {
                // Ничего страшного, скорее всего просто не найден exe'шник
            }
        }
    }
}