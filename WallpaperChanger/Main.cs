using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Configurator;
using Microsoft.Win32;

namespace WallpaperChanger
{
    class MainClass
    {
        /// Будем делать вид, что мы вот этот браузер
        private static string userAgent = @"Mozilla/5.0 (Windows NT 6.1; WOW64; rv:18.0) Gecko/20100101 Firefox/18.0";
        /// Сдесь случайным образом выдают список обоев. Мы будем скачивать только первую картинку.
        private static string mixUrl = @"http://wallbase.cc/random?section=wallpapers&q=&res_opt=eqeq&res=0x0&thpp=32&purity={2}&board={0}&aspect={1}";
        /// Ссылка на страничку с картинкой в большом разрешении
        private static string imageUrl = @"http://wallbase.cc/wallpaper/{0}";
        /// Название локального файла для картинки
        private static string imageFileName = "WallpaperChanger.bmp";
        /// Название источника в логах
        private static string logSource = "WallpaperChanger";
        private static Categories categories = Categories.None;
        private static Purity purity = Purity.None;

        /// Мистические штуки, нужны для системного вызова смены обоев в винде
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern Int32 SystemParametersInfo(UInt32 action, UInt32 uParam, String vParam, UInt32 winIni);

        private static readonly UInt32 SPI_SETDESKWALLPAPER = 0x14;
        private static readonly UInt32 SPIF_UPDATEINIFILE = 0x01;
        private static readonly UInt32 SPIF_SENDWININICHANGE = 0x02;

        public static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            Registry.CurrentUser.CreateSubKey(@"Software\WallpaperChanger");
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\WallpaperChanger", true);
            
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

            try
            {
                SetWallpaper();
            }
            catch (Exception e)
            {
                string errorMessage = String.Format("Fatal error: {0}", e.ToString());

                // Пишем в лог...
                if (!EventLog.SourceExists(logSource))
                    EventLog.CreateEventSource(logSource, "Application");
                
                EventLog.WriteEntry(logSource, errorMessage, EventLogEntryType.Warning);
                
                // ...и в консоль
                Console.WriteLine(errorMessage);
            }
        }

        public static void SetWallpaper()
        {
            // Ширина и высота картинки для загрузки
            int width = SystemInformation.VirtualScreen.Width;
            int height = SystemInformation.VirtualScreen.Height;

            string uri;
            HttpWebRequest webRequest;
            HttpWebResponse webResponse;
            Stream streamResponse;
            StreamReader reader;

            string categoriesUri = "";

            if ((categories & Categories.General) == Categories.General)
                categoriesUri += "2";

            if ((categories & Categories.Anime) == Categories.Anime)
                categoriesUri += "1";

            if ((categories & Categories.HDR) == Categories.HDR)
                categoriesUri += "3";

            string purityUri = "";
            
            if ((purity & Purity.Clean) == Purity.Clean)
                purityUri += "1";
            else 
                purityUri += "0";
            
            if ((purity & Purity.Sketchy) == Purity.Sketchy)
                purityUri += "1";
            else
                purityUri += "0";

            purityUri += "0";

            // Запрашиваем страничку с обоями
            uri = String.Format(mixUrl, categoriesUri, String.Format("{0:0.00}", (double)width / (double)height), purityUri);

            #if DEBUG
            Console.WriteLine("mixUrl: " + uri);
            #endif

            webRequest = (HttpWebRequest)WebRequest.Create(uri);
            webRequest.UserAgent = userAgent;
            webResponse = (HttpWebResponse)webRequest.GetResponse();
            streamResponse = webResponse.GetResponseStream();
            
            // Читаем ее в строку
            reader = new StreamReader(streamResponse);
            string mixHtml = reader.ReadToEnd();

            #if DEBUG
            //Console.WriteLine(mixHtml);
            #endif

            string imageId = String.Empty;
            Match m;

            m = Regex.Match(mixHtml, @"<a href=""http://wallbase\.cc/wallpaper/(\d+)"" target=""_blank"">");

            // Получаем id первой попавшейся картинки
            if (m.Success)
                imageId = m.Groups[1].ToString();
            else
                throw new Exception("Link to the image page not found");

            // Запрашиваем картинку
            uri = String.Format(imageUrl, imageId);

            #if DEBUG
            Console.WriteLine("imageHtmlUrl: " + uri);
            #endif

            webRequest = (HttpWebRequest)WebRequest.Create(uri);
            webRequest.UserAgent = userAgent;
            webResponse = (HttpWebResponse)webRequest.GetResponse();
            streamResponse = webResponse.GetResponseStream();

            // Читаем ее в строку
            reader = new StreamReader(streamResponse);
            string imageHtml = reader.ReadToEnd();

            #if DEBUG
            //Console.WriteLine(imageHtml);
            #endif

            m = Regex.Match(imageHtml, @"<img src=""(http://wallpapers\.wallbase\.cc/.+/wallpaper-" + imageId + @"\.jpg)"" class=""wall stage1 wide"">");

            if (m.Success)
                uri = m.Groups[1].ToString();
            else
                throw new Exception("Link to the image not found");

            #if DEBUG
            Console.WriteLine("image uri: " + uri);
            #endif

            webRequest = (HttpWebRequest)WebRequest.Create(uri);
            webRequest.UserAgent = userAgent;
            webResponse = (HttpWebResponse)webRequest.GetResponse();
            streamResponse = webResponse.GetResponseStream();

            string imagePath = Path.GetTempPath() + imageFileName;
            
            // И сохраняем ее в файл
            Image image = Image.FromStream(streamResponse); 
            image.Save(imagePath, ImageFormat.Bmp);

            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            key.SetValue(@"WallpaperStyle", "10");
            key.SetValue(@"TileWallpaper", "0");

            // Говорим винде обновить обои
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }
    }
}