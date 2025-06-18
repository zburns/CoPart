using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace CoPartCompactRacer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(System.DateTime.Now + " Downloading New Auction File...");
            if (System.IO.File.Exists("SalesData.csv"))
                System.IO.File.Delete("SalesData.csv");

            System.Net.WebClient client = new System.Net.WebClient();
            client.DownloadFile(new System.Configuration.AppSettingsReader().GetValue("SalesDataFile", System.Type.GetType("System.String")).ToString(), "SalesData.csv");

            Console.WriteLine(System.DateTime.Now + " Importing File...");
            System.Data.DataTable dt = ReadCsvToDataTable("SalesData.csv");
            Console.WriteLine(System.DateTime.Now + " Filtering File Based on Config File...");
            System.Data.DataTable results = FilterTable(dt);
            Console.WriteLine(System.DateTime.Now + " Building Hi-Res Images File...");

            DataTable images = new DataTable();
            images.Columns.Add("LotNumber");
            images.Columns.Add("ImageNumber");
            images.Columns.Add("ImageUrl");
            images.Columns.Add("ThumbUrl");

            foreach (System.Data.DataRow dr in results.Rows)
            {
                ExtractHdImages(images, dr);
            }

            Console.WriteLine(System.DateTime.Now + " Building HTML Results...");
            GenerateHtmlReport(results, images, "Results.html");

            UploadFiles();
        }

        public static DataTable ReadCsvToDataTable(string filePath)
        {
            DataTable table = new DataTable();
            using (StreamReader reader = new StreamReader(filePath))
            {
                string line = reader.ReadLine();
                if (line == null)
                    return table;

                string[] headers = ParseCsvLine(line);
                int i = 0;
                while (i < headers.Length)
                {
                    table.Columns.Add(headers[i]);
                    i++;
                }

                while (!reader.EndOfStream)
                {
                    line = reader.ReadLine();
                    string[] fields = ParseCsvLine(line);
                    table.Rows.Add(fields);
                }
            }
            return table;
        }

        private static string[] ParseCsvLine(string line)
        {
            List<string> fields = new List<string>();
            StringBuilder field = new StringBuilder();
            bool inQuotes = false;
            int i = 0;

            while (i < line.Length)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(field.ToString());
                        field.Length = 0;
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                i++;
            }
            fields.Add(field.ToString());
            return fields.ToArray();
        }

        public static System.Data.DataTable FilterTable(System.Data.DataTable sourceTable)
        {
            System.Data.DataTable filteredTable = sourceTable.Clone();
            System.Data.DataTable watchedTable = sourceTable.Clone();

            string[] yardNumbers = new System.Configuration.AppSettingsReader().GetValue("YardNumbers", System.Type.GetType("System.String")).ToString().Split(',');
            string[] makes = new System.Configuration.AppSettingsReader().GetValue("Makes", System.Type.GetType("System.String")).ToString().Split(',');
            string[] models = new System.Configuration.AppSettingsReader().GetValue("Models", System.Type.GetType("System.String")).ToString().Split(',');
            string[] damages = new System.Configuration.AppSettingsReader().GetValue("Damage", System.Type.GetType("System.String")).ToString().Split(',');
            string[] cylinders = new System.Configuration.AppSettingsReader().GetValue("Cylinders", System.Type.GetType("System.String")).ToString().Split(',');

            double odometerMax = int.Parse(new System.Configuration.AppSettingsReader().GetValue("Odometer", System.Type.GetType("System.String")).ToString());
            string drive = new System.Configuration.AppSettingsReader().GetValue("Drive", System.Type.GetType("System.String")).ToString();
            string fuel = new System.Configuration.AppSettingsReader().GetValue("FuelType", System.Type.GetType("System.String")).ToString();
            string runDrive = new System.Configuration.AppSettingsReader().GetValue("RunsDrives", System.Type.GetType("System.String")).ToString();

            bool showonlysaledates = Convert.ToBoolean(new System.Configuration.AppSettingsReader().GetValue("ShowOnlyLotsWithSaleDate", System.Type.GetType("System.Boolean")));
            string ignorefile = new System.Configuration.AppSettingsReader().GetValue("IgnoreLotNumberFile", System.Type.GetType("System.String")).ToString();
            System.Collections.ArrayList ignores = new System.Collections.ArrayList();
            bool ignoreexceedsvalue = Convert.ToBoolean(new System.Configuration.AppSettingsReader().GetValue("IgnoreRepairExceedsValue", System.Type.GetType("System.Boolean")));

            string watchingfile = new System.Configuration.AppSettingsReader().GetValue("AlreadyWatchingFile", System.Type.GetType("System.String")).ToString();
            System.Collections.ArrayList watches = new System.Collections.ArrayList();


            if (System.IO.File.Exists(watchingfile))
            {
                string[] tmp = System.IO.File.ReadAllLines(watchingfile);
                foreach (string t in tmp)
                {
                    watches.Add(t.Trim());
                }
            }

            if (ignoreexceedsvalue)
            {
                ignores.Clear();
                if (System.IO.File.Exists(ignorefile))
                {
                    string[] tmp = System.IO.File.ReadAllLines(ignorefile);
                    foreach (string t in tmp)
                    {
                        ignores.Add(t.Trim());
                    }
                }

                int k = 0;
                while (k < sourceTable.Rows.Count)
                {
                    DataRow row = sourceTable.Rows[k];
                    if (Convert.ToDouble(row["Repair cost"]) > Convert.ToDouble(row["Est. Retail Value"]) && !ignores.Contains(row["Lot number"].ToString().Trim()))
                    {
                        System.IO.File.AppendAllText(ignorefile,row["Lot number"].ToString().Trim() + "\r\n");
                    }
                    k++;
                }
            }

            ignores.Clear();
            if (System.IO.File.Exists(ignorefile))
            {
                string[] tmp = System.IO.File.ReadAllLines(ignorefile);
                foreach (string t in tmp)
                {
                    ignores.Add(t.Trim());
                }
            }

            int i = 0;
            while (i < sourceTable.Rows.Count)
            {
                DataRow row = sourceTable.Rows[i];
                string yard = row["Yard number"].ToString();
                string make = row["Make"].ToString().ToUpper();
                string model = row["Model Group"].ToString().ToUpper();
                string damage = row["Damage Description"].ToString().ToUpper();
                string odometerStr = row["Odometer"].ToString().Replace(",", "");
                string driveVal = row["Drive"].ToString();
                string fuelVal = row["Fuel Type"].ToString();
                string cylindersVal = row["Cylinders"].ToString();
                string runDriveVal = row["Runs/Drives"].ToString();

                if (Convert.ToDouble(odometerStr) < odometerMax)
                {
                    if (Contains(yardNumbers, yard) &&
                        Contains(makes, make) &&
                        Contains(models, model) &&
                        Contains(damages, damage) &&
                        driveVal == drive &&
                        fuelVal == fuel &&
                        Contains(cylinders, cylindersVal) &&
                        runDriveVal == runDrive)
                    {
                        if (!ignores.Contains(row["Lot number"].ToString()) && !watches.Contains(row["Lot number"].ToString()))
                        {
                            if (showonlysaledates)
                            {
                                if (row["Sale Date M/D/CY"].ToString() != "0")
                                    filteredTable.ImportRow(row);
                            }
                            else
                            {
                                filteredTable.ImportRow(row);
                            }
                        }

                        if (watches.Contains(row["Lot number"].ToString()))
                        {
                            watchedTable.ImportRow(row);
                        }
                    }
                }
                i++;
            }

            #region "Watched HTML File"
            System.Data.DataTable wimages = new DataTable();
            wimages.Columns.Add("LotNumber");
            wimages.Columns.Add("ImageNumber");
            wimages.Columns.Add("ImageUrl");
            wimages.Columns.Add("ThumbUrl");

            foreach (System.Data.DataRow dr in watchedTable.Rows)
            {
                ExtractHdImages(wimages, dr);
            }

            GenerateHtmlReport(watchedTable, wimages, "Watched.html");
            #endregion

            return filteredTable;
        }

        private static bool Contains(string[] array, string value)
        {
            int i = 0;
            while (i < array.Length)
            {
                if (array[i].Trim().ToUpper() == value.ToUpper())
                    return true;
                i++;
            }
            return false;
        }





        public static void ExtractHdImages(System.Data.DataTable table, System.Data.DataRow dr)
        {
            string json = String.Empty;
            try
            {
                json = GetJson(dr["Image URL"].ToString());
            }
            catch 
            { 
            }

            if (json.Length > 0)
            {
                try
                {
                    JObject root = JObject.Parse(json);
                    JArray lotImages = (JArray)root["lotImages"];

                    int i = 0;
                    while (i < lotImages.Count)
                    {
                        JObject imageEntry = (JObject)lotImages[i];
                        string sequence = imageEntry["sequence"].ToString();

                        JArray links = (JArray)imageEntry["link"];
                        string hdUrl = null;
                        string thumbUrl = null;

                        int j = 0;
                        while (j < links.Count)
                        {
                            JObject link = (JObject)links[j];
                            bool isHd = link["isHdImage"].ToObject<bool>();
                            bool isThumb = link["isThumbNail"].ToObject<bool>();

                            if (isHd)
                                hdUrl = link["url"].ToString().Trim();

                            if (isThumb)
                                thumbUrl = link["url"].ToString().Trim();

                            j++;
                        }

                        if (hdUrl != null && thumbUrl != null)
                        {
                            DataRow row = table.NewRow();
                            row["LotNumber"] = dr["Lot number"].ToString().Trim();
                            row["ImageNumber"] = sequence;
                            row["ImageUrl"] = hdUrl;
                            row["ThumbUrl"] = thumbUrl;
                            table.Rows.Add(row);
                        }

                        i++;
                    }
                }
                catch
                {

                }
            }
        }

        public static string GetJson(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }

        public static void GenerateHtmlReport(DataTable filteredData, DataTable imageData, string outputPath)
        {
            StringBuilder html = new StringBuilder();

            html.AppendLine("<html>");
            html.AppendLine("<head><title>Filtered <strong>" + filteredData.Rows.Count + "</strong> Results as of " + System.DateTime.Now + "</title></head>");
            html.AppendLine("<body>");
            html.AppendLine("<h1>Filtered <strong>" + filteredData.Rows.Count + "</strong> Vehicles as of " + System.DateTime.Now + "</h1>");

            int i = 0;
            while (i < filteredData.Rows.Count)
            {
                DataRow row = filteredData.Rows[i];
                string lotNumber = row["Lot number"].ToString();

                html.AppendLine("<div style='border:1px solid #ccc; margin:10px; padding:10px;'>");
                html.AppendLine("<table border='1' cellspacing='0' cellpadding='5'>");

                int j = 0;
                while (j < filteredData.Columns.Count)
                {
                    html.AppendLine("<tr><td><b>" + filteredData.Columns[j].ColumnName + "</b></td><td>" + row[j].ToString() + "</td></tr>");
                    j++;
                }

                html.AppendLine("</table>");
                html.AppendLine("<p><b>Images:</b></p>");
                html.AppendLine("<p><b>Lot Number:</b> " + lotNumber + "</p>");

                DataRow[] imageRows = imageData.Select("LotNumber = '" + lotNumber + "'");

                if (imageRows.Length > 0)
                {
                    html.AppendLine("<div>");
                    int k = 0;
                    while (k < imageRows.Length)
                    {
                        string hdUrl = imageRows[k]["ImageUrl"].ToString();
                        string thumbUrl = imageRows[k]["ThumbUrl"].ToString();

                        html.AppendLine("<a href='" + hdUrl + "' target='_blank'>");
                        html.AppendLine("<img src='" + thumbUrl + "' height='100' style='margin:5px;' />");
                        html.AppendLine("</a>");

                        k++;
                    }
                    html.AppendLine("</div>");
                }
                else
                {
                    html.AppendLine("<p>No HD images found.</p>");
                }

                html.AppendLine("</div>");
                i++;
            }

            html.AppendLine("</body></html>");

            File.WriteAllText(outputPath, html.ToString(), Encoding.UTF8);
        }

        static private void UploadFiles()
        {
            string chilkat = new System.Configuration.AppSettingsReader().GetValue("Chilkat", System.Type.GetType("System.String")).ToString();
            string host = new System.Configuration.AppSettingsReader().GetValue("Host", System.Type.GetType("System.String")).ToString();
            string port = new System.Configuration.AppSettingsReader().GetValue("Port", System.Type.GetType("System.String")).ToString();
            string username = new System.Configuration.AppSettingsReader().GetValue("UserName", System.Type.GetType("System.String")).ToString();
            string password = new System.Configuration.AppSettingsReader().GetValue("Password", System.Type.GetType("System.String")).ToString();

            Chilkat.Global glob = new Chilkat.Global();
            glob.UnlockBundle(chilkat);

            Chilkat.SFtp sftp = new Chilkat.SFtp();
            string hostname = host;
            sftp.Connect(hostname, Convert.ToInt16(port));
            sftp.AuthenticatePw(username, password);
            sftp.InitializeSftp();

            string handle1 = sftp.OpenFile("/home/www/website/compact/results.html", "writeOnly", "createTruncate");
            sftp.UploadFile(handle1, System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\Results.html");
            sftp.CloseHandle(handle1);

            string handle2 = sftp.OpenFile("/home/www/website/compact/watched.html", "writeOnly", "createTruncate");
            sftp.UploadFile(handle2, System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\Watched.html");
            sftp.CloseHandle(handle2);


            sftp.Dispose();
            sftp = null;
        }
    }
}
