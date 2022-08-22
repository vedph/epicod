using HtmlAgilityPack;
using ScrapySharp.Html.Forms;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Epicod.Scraper.Clauss
{
    public static class Experiment
    {
        public static async Task GoHttpClient()
        {
            FormUrlEncodedContent formContent = new(
                new[]
                {
                    new KeyValuePair<string, string>("p_provinz", "Galatia"),
                    new KeyValuePair<string, string>("s_sprache", "en"),
                    new KeyValuePair<string, string>("r_auswahl", "und"),
                    new KeyValuePair<string, string>("r_sortierung", "Provinz"),
                    new KeyValuePair<string, string>("cmd_submit", "go"),
                });

            using (HttpClient client = new(handler: new HttpClientHandler
            {
                // 8888 = Fiddler standard port
                Proxy = new WebProxy(new Uri("http://localhost:8888")),
                UseProxy = true
            }))
            {
                HttpResponseMessage response = await client.PostAsync(
                    "https://db.edcs.eu/epigr/epitest.php", formContent);
                string content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine(content);
            }
        }

        public static void Go()
        {
            // using browser
            ScrapingBrowser browser = new();
            browser.AllowAutoRedirect = true;
            browser.IgnoreCookies = true;
            WebPage home = browser.NavigateToPage(new Uri("https://db.edcs.eu/epigr/epitest.php"));

            PageWebForm form = home.FindForm("epi");
            form["p_provinz"] = "Galatia";
            form["s_sprache"] = "en";
            form["r_auswahl"] = "und";
            form["r_sortierung"] = "Provinz";
            form["cmd_submit"] = "go";

            form.Method = HttpVerb.Post;
            WebPage result = form.Submit();
            Debug.WriteLine(result);
        }
    }
}