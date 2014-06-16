using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace RssGen
{
    class Program
    {
        static void Main(string[] args)
        {
            var processor = new Processor();
            processor.Start();

            if (Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["AutoClose"]) == false)
            {
                Console.WriteLine("Process Complete. Press Enter to Close.");
                Console.ReadLine();
            }
        }
    }

    public class Processor
    {
        List<FileInfo> _files;

        #region settings

        string _path;
        string _urlPrefix;
        bool _minify;
        string _podcastTitle;
        string _podcastHomepage;
        string _language;
        string _copyright;
        string _subtitle;
        string _author;
        string _descriptionSummary;
        bool _explicit;
        string _email;
        string _artworkUrl;
        string _category;
        string _subCategory;
        string _podcastRssUrl;
        string _fileExtensionFilter;
        string _outputFilename;
        bool _backupExistingFeedFirst;

        private string ReadSetting(string key)
        {
            return HttpUtility.HtmlEncode(System.Configuration.ConfigurationManager.AppSettings[key]);
        }

        private void LoadSettings()
        {
            _path = ReadSetting("DirectoryPath");
            _urlPrefix = ReadSetting("UrlPrefix");
            _minify = Convert.ToBoolean(ReadSetting("Minify"));
            _podcastTitle = ReadSetting("PodcastTitle");
            _podcastHomepage = ReadSetting("PodcastHomepage");
            _language = ReadSetting("Language");
            _copyright = ReadSetting("Copyright");
            _subtitle = ReadSetting("Subtitle");
            _author = ReadSetting("Author");
            _descriptionSummary = ReadSetting("DescriptionSummary");
            _explicit = Convert.ToBoolean(ReadSetting("Explicit"));
            _email = ReadSetting("Email");
            _artworkUrl = ReadSetting("ArtworkUrl");
            _category = ReadSetting("Category");
            _subCategory = ReadSetting("SubCategory");
            _podcastRssUrl = ReadSetting("PodcastRssUrl");
            _fileExtensionFilter = ReadSetting("FileExtensionFilter");
            _outputFilename = ReadSetting("OutputFilename");
            _backupExistingFeedFirst = Convert.ToBoolean(ReadSetting("BackupExistingFeedFirst"));
        }

        #endregion

        #region ctor
        public Processor()
        {
            LoadSettings();
        }
        #endregion

        public void Start()
        {
            _files = GetFiles();
            SortFiles();

            string header = BuildHeader();
            string items = BuildItems();
            string footer = BuildFooter();

            WriteFile(header, items, footer);
        }

        private List<FileInfo> GetFiles()
        {
            var files = new List<FileInfo>();

            if (Directory.Exists(_path))
            {
                var fileNames = Directory.GetFiles(_path);

                foreach (string fileName in fileNames)
                {
                    var fi = new FileInfo(fileName);

                    if (string.IsNullOrEmpty(_fileExtensionFilter))
                    {
                        files.Add(fi);
                    }
                    else
                    {
                        if (fi.Extension.Equals(_fileExtensionFilter, StringComparison.InvariantCultureIgnoreCase))
                            files.Add(fi);
                    }
                }
            }

            return files;
        }

        private void SortFiles()
        {
            // newest to oldest
            _files = _files.OrderBy(x => x.CreationTimeUtc).Reverse().ToList();
        }

        private string BuildItems()
        {
            StringBuilder items = new StringBuilder();

            foreach (var file in _files)
            {
                TagLib.File f = TagLib.File.Create(file.FullName);
                string title = f.Tag.Title;

                string url = _urlPrefix + file.Name;

                StringBuilder sb = new StringBuilder();

                sb.Append("<item>\n");
                sb.AppendFormat("\t<title>{0}</title>\n", HttpUtility.HtmlEncode(title));
                sb.AppendFormat("\t<pubDate>{0}</pubDate>\n", file.CreationTimeUtc.ToString("r"));
                sb.AppendFormat("\t<guid isPermaLink=\"false\">{0}</guid>\n", url);
                sb.AppendFormat("\t<enclosure url=\"{0}\" length=\"{1}\" type=\"audio/mpeg\" />\n", url, file.Length);
                sb.Append("\t<itunes:subtitle></itunes:subtitle>\n");
                sb.Append("\t<itunes:summary></itunes:summary>\n");
                sb.Append("</item>\n");

                items.AppendLine(sb.ToString());
            }

            return items.ToString();
        }

        private string BuildHeader()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            sb.Append("<rss xmlns:itunes=\"http://www.itunes.com/dtds/podcast-1.0.dtd\" version=\"2.0\">\n");
            sb.Append("\t<channel>\n");
            sb.AppendFormat("\t\t<title>{0}</title>\n", _podcastTitle);
            sb.AppendFormat("\t\t<link>{0}</link>\n", _podcastHomepage);
            sb.AppendFormat("\t\t<language>{0}</language>\n", _language);
            sb.AppendFormat("\t\t<copyright>{0}</copyright>\n", _copyright);
            sb.AppendFormat("\t\t<itunes:subtitle>{0}</itunes:subtitle>\n", _subtitle);
            sb.AppendFormat("\t\t<itunes:author>{0}</itunes:author>\n", _author);
            sb.AppendFormat("\t\t<itunes:summary>{0}</itunes:summary>\n", _descriptionSummary);
            sb.AppendFormat("\t\t<description>{0}</description>\n", _descriptionSummary);
            sb.AppendFormat("\t\t<itunes:explicit>{0}</itunes:explicit>\n", _explicit ? "yes" : "no");
            sb.Append("\t\t<itunes:owner>\n");
            sb.AppendFormat("\t\t\t<itunes:name>{0}</itunes:name>\n", _podcastTitle);
            sb.AppendFormat("\t\t\t<itunes:email>{0}</itunes:email>\n", _email);
            sb.Append("\t\t</itunes:owner>\n");
            sb.AppendFormat("\t\t<itunes:image href=\"{0}\" />\n", _artworkUrl);
            sb.AppendFormat("\t\t<itunes:category text=\"{0}\">\n", _category);
            sb.AppendFormat("\t\t\t<itunes:category text=\"{0}\" />\n", _subCategory);
            sb.Append("\t\t</itunes:category>\n");
            sb.AppendFormat("\t\t<atom10:link xmlns:atom10=\"http://www.w3.org/2005/Atom\" rel=\"self\" type=\"application/rss+xml\" href=\"{0}\" />\n", _podcastRssUrl);

            return sb.ToString();
        }

        private string BuildFooter()
        {
            string footer = "";
            footer += "\t</channel>\n";
            footer += "</rss>\n";
            return footer;
        }

        private string Minify(string input)
        {
            // http://stackoverflow.com/questions/8913138/minify-indented-json-string-in-net
            //return Regex.Replace(input, "(\"(?:[^\"\\\\]|\\\\.)*\")|\\s+", "$1");

            Regex RegexBetweenTags = new Regex(@">(?! )\s+", RegexOptions.Compiled);
            Regex RegexLineBreaks = new Regex(@"([\n\s])+?(?<= {2,})<", RegexOptions.Compiled);

            input = RegexBetweenTags.Replace(input, ">");
            input = RegexLineBreaks.Replace(input, "<");

            return input.Trim();
        }

        private void WriteFile(string header, string items, string footer)
        {
            string output = header + items + footer;
            if (_minify) output = Minify(output);

            string outputPath = Path.Combine(_path, _outputFilename);

            if (_backupExistingFeedFirst && File.Exists(outputPath))
            {
                File.Move(outputPath, string.Format("{0}.backup{1:yyyyMMddHHmmss}", outputPath, DateTime.Now));
            }

            using (StreamWriter outfile = new StreamWriter(outputPath))
            {
                outfile.Write(output);
            }
        }

    }
}