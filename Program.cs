using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using HtmlAgilityPack;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace WBFetchSelenium {
    internal class Program {
        private static object _fileLock = new object();

        static void Main(string[] args) {
            string domain = args.First();
            string dataUrl = $"https://web.archive.org/cdx/search/cdx?url={domain}/*&output=json&fl=timestamp,original&collapse=urlkey";

            using (var client = new System.Net.WebClient()) {
                client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36");
                string response = client.DownloadString(dataUrl);

                var data = JsonConvert.DeserializeObject<List<List<string>>>(response);
                var urls = data.Skip(1).Select(x => x[1]).ToList();

                using(var xl = new XLWorkbook()) {
                    #region Initialize excel sheet
                    var sheet = xl.Worksheets.Add("Sheet1"); // TODO: set to FQDN
                    sheet.Cell(1, 1).Value = "URL";
                    sheet.Cell(1, 2).Value = "Page Title";
                    sheet.Cell(1, 3).Value = "Remarks"; // only used if exception is raised or if row gets skipped
                    #endregion

                    #region Chrome setup
                    var options = new ChromeOptions();
                    options.AddArgument("--headless");
                    options.AddArgument("--disable-gpu");
                    //options.AddArgument("--no-sandbox");
                    options.AddArgument("--disable-dev-shm-usage");
                    #endregion

                    using (var drv = new ChromeDriver(options)) {
                        int index = 2;

                        foreach (var url in urls) {
                            try {
                                drv.Navigate().GoToUrl(url);

                                #region Skip over localhost (if present)
                                if (drv.Url.Contains("localhost") || drv.Url.Contains("127.0.0.1")) {
                                    Console.WriteLine($"Skipped {url}");

                                    sheet.Cell(index, 1).Value = url;
                                    sheet.Cell(index, 3).Value = "Skipped: Redirected to localhost";
                                    index++;

                                    continue;
                                }
                                #endregion

                                var wait = new WebDriverWait(drv, TimeSpan.FromSeconds(10));
                                wait.Until(d => d.Title.Length > 0);

                                string pageTitle = drv.Title.Trim();

                                Console.WriteLine($"{url} -> {pageTitle}");

                                sheet.Cell(index, 1).Value = url;
                                sheet.Cell(index, 2).Value = pageTitle;
                                index++;
                            }
                            catch (Exception ex) {
                                Console.WriteLine($"Error fetching {url}: {ex.Message}");

                                sheet.Cell(index, 1).Value = url;
                                sheet.Cell(index, 3).Value = $"Error: {ex.Message}";
                                index++;
                            }
                        }

                        xl.SaveAs("titles.xlsx"); // TODO: set filename to FQDN (e.g. ent.example.com)
                    }

                    Console.WriteLine("Scan completed.");
                }
            }
        }
    }
}