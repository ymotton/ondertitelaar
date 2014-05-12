using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using SimpleBrowser;

namespace Ondertitelaar.Controllers
{
    public class OndertitelComController : ApiController
    {
        public HttpResponseMessage Get(string name, string imdb = null)
        {
            var browser = new Browser();
            browser.UseGZip = true;
            var detailsAnchorUrl = GetDetailsUrl(browser, name, imdb);
            var downloadAnchorUrl = GetDownloadUrl(browser, detailsAnchorUrl);
            var downloadStream = GetDownloadStream(downloadAnchorUrl);
            return PrepareResponse(name, downloadStream);
        }

        HttpResponseMessage PrepareResponse(string name, Stream downloadStream)
        {
            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
            result.Content = new StreamContent(downloadStream);
            result.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = string.Format("{0}.srt", name)
            };
            result.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            return result;
        }
        Stream GetDownloadStream(string downloadAnchorUrl)
        {
            var bytes = GetResponseBytes(downloadAnchorUrl);
            var zipFile = new ZipArchive(new MemoryStream(bytes));
            var srtEntry = zipFile.Entries
                .FirstOrDefault(e => e.FullName.EndsWith(".srt", StringComparison.OrdinalIgnoreCase));
            if (srtEntry == null)
            {
                NotFound("No .srt found in archive");
            }
            var downloadStream = srtEntry.Open();
            return downloadStream;
        }
        string GetDownloadUrl(Browser browser, string detailsAnchorUrl)
        {
            browser.Navigate(detailsAnchorUrl);

            var downloadAnchor = browser.Find(ElementType.Anchor, FindBy.PartialText, "DOWNLOAD");
            if (!downloadAnchor.Exists)
            {
                NotFound("download anchor not found");
            }
            var downloadAnchorUrl = downloadAnchor.GetAttribute("href");
            return downloadAnchorUrl;
        }
        string GetDetailsUrl(Browser browser, string name, string imdb)
        {
            string url = string.Format("http://ondertitel.com/zoeken.php?trefwoord={0}&zoeken=", name);
            browser.Navigate(url);

            var results = browser.Find("ul", FindBy.Class, "subtitle-list");
            if (!results.Exists)
            {
                NotFound("ul not found");
            }

            var listItems = results.Select("li");
            if (!listItems.Exists)
            {
                NotFound("li not found");
            }

            HtmlResult listItem;
            // If we have the imdb id, then only pick matching listItems
            if (!string.IsNullOrWhiteSpace(imdb))
            {
                var imdbUrl = string.Format("http://www.imdb.com/title/{0}/", imdb);

                listItem =
                    (from li in listItems
                     let anchor = li.Select("a.imdb-info_sub")
                     where anchor.Exists
                     where anchor.GetAttribute("href") == imdbUrl
                     select li).FirstOrDefault();
            }
            else
            {
                listItem =
                    (from li in listItems
                     let anchor = li.Select("a.recent")
                     where anchor.Exists
                     where anchor.Value.Contains(name)
                     select li).FirstOrDefault();
            }

            if (listItem == null)
            {
                NotFound("Matching li not found");
            }

            var detailsAnchor = listItem.Select("a.recent");
            var detailsAnchorUrl = "http://ondertitel.com" + detailsAnchor.GetAttribute("href");
            return detailsAnchorUrl;
        }
        
        void NotFound(string message)
        {
            throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(message)
            });
        }
        byte[] GetResponseBytes(string url)
        {
            byte[] content;
            using (var client = new WebClient())
            {
                content = client.DownloadData(url);
            }
            return content;
        }
    }

    public class SearchResult
    {
        public string Name { get; set; }
        public string Imdb { get; set; }
        public string Value { get; set; }
    }
}