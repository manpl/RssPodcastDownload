using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using CommandLine;
using CommandLine.Text;
[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace RssPodcastDownload
{
    public class Program
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof (Program));

        public static void Main(string[] args)
        {
            var options = new Options();
            if (Parser.Default.ParseArguments(args, options) && options.Valid())
            {
                var rssUri = new Uri(options.RssPath);
                var destination = new DirectoryInfo(options.DestPath);
                var latest = int.Parse(options.Latest);
                var downloader = new RssDownloader(rssUri, destination, latest);

                try
                {
                    downloader.Process();

                    Logger.Info("Finished");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            }
        }
    }

    public class RssDownloader
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(RssDownloader));

        private readonly Uri m_rssPath;
        private readonly DirectoryInfo m_destination;
        private readonly int m_latest;

        public RssDownloader(Uri rssPath, DirectoryInfo destination, int latest)
        {
            m_rssPath = rssPath;
            m_destination = destination;
            m_latest = latest;
        }

        public void Process()
        {
            Logger.Info("Processing...");

            var document = DownloadRssFeed();

            var paths = ExtractPaths(document).ToList();

            ProcessFiles(paths.Take(m_latest));
        }

        private XDocument DownloadRssFeed()
        {
            Logger.Info("Donloading rss feed");

            var client = new WebClient();
            
            var rssDocument = client.DownloadString(m_rssPath);

            XDocument document = XDocument.Parse(rssDocument);

            return document;
        }

        private IEnumerable<Uri> ExtractPaths(XDocument document)
        {
            Logger.Info("Extracting file paths");

            var allItems  = document.Descendants("item");

            foreach (var item in allItems)
            {
                var enclosure = item.Elements("enclosure").FirstOrDefault();
                if (enclosure == null) continue;
                
                var url = enclosure.Attributes("url").FirstOrDefault();
                if (url == null) continue;
                
                yield return new Uri(url.Value);
            }
        }

        private void ProcessFiles(IEnumerable<Uri> paths)
        {
            foreach (var path in paths)
            {
                ProcessSingleFile(path);
            }
        }

        private void ProcessSingleFile(Uri path)
        {
            Logger.Info("Processing file: " + path.AbsolutePath);

            var filename = GetFileName(path);

            if (!FileAlreadyExists(filename))
            {
                DownloadFile(path);
            }
            else
            {
                Logger.Info(string.Format("File '{0}' already exists", filename));
            }
        }

        private void DownloadFile(Uri path)
        {
            Logger.Info(string.Format("Downloading file: '{0}' ", path));

            var webClient = new WebClient();
            var filename = GetFileName(path);
            var localPath = GetLocalFilePath(filename);
            try
            {
                
                webClient.DownloadFile(path, localPath);
            }
            catch (Exception ex)
            {
                Logger.Error("Donloading podcast: " + path.AbsolutePath, ex);                
                
                if(File.Exists(localPath))
                    File.Delete(localPath);
            }
        }

        private string GetFileName(Uri path)
        {
            return new FileInfo(path.AbsolutePath).Name;
        }

        private bool FileAlreadyExists(string filename)
        {
            return File.Exists(GetLocalFilePath(filename));
        }

        private string GetLocalFilePath(string filename)
        {
            return Path.Combine(m_destination.FullName, filename);
        }
    }

    class Options
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(Options));

        [Option('r', "rssPath", Required = true, HelpText = "Rss file path")]
        public string RssPath { get; set; }

        [Option('d', "destPath", Required = true, HelpText = "Local destination path")]
        public string DestPath { get; set; }

        [Option('n', "latestNumber", Required = true, HelpText = "Number of latest podcasts")]
        public string Latest { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }

        public bool Valid()
        {
            if (!Uri.IsWellFormedUriString(RssPath, UriKind.RelativeOrAbsolute))
            {
                Logger.Warn("RssPath is invalid");
                return false;
            }

            if (!Directory.Exists(DestPath))
            {
                Logger.Warn("DestPath does not exist");
                return false;
            }

            int temp;
            if (!int.TryParse(Latest, out temp))
            {
                Logger.Warn("Latest is not an integer");
                return false;
            }

            return true;
        }
    }
}