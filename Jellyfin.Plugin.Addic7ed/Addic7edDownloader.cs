using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Jellyfin.Plugin.Addic7ed.Configuration;
using MediaBrowser.Common;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Addic7ed
{
    public class Addic7edDownloader : ISubtitleProvider
    {

        private static readonly CultureInfo _usCulture = CultureInfo.ReadOnly(new CultureInfo("en-US"));
        private readonly ILogger<Addic7edDownloader> _logger;
        private readonly IFileSystem _fileSystem;
        private DateTime _lastRateLimitException;
        private DateTime _lastLogin;
        private int _rateLimitLeft = 40;
        private readonly HttpClient _httpClient;
        private readonly IApplicationHost _appHost;
        private ILocalizationManager _localizationManager;
      
        private readonly IServerConfigurationManager _config;
      
        private readonly string _baseUrl = "https://www.addic7ed.com";
     
        public Addic7edDownloader(ILogger<Addic7edDownloader> logger, HttpClient httpClient, IServerConfigurationManager config, IFileSystem fileSystem, ILocalizationManager localizationManager)
        {
            _logger = logger;
            _httpClient = httpClient;
            _config = config;
            _fileSystem = fileSystem;
            _localizationManager = localizationManager;
        }
        public int Order => 3;





        public string Name
        {
            get { return "Addic7ed"; }
        }

        private PluginConfiguration GetOptions()
        {
            return Plugin.Instance.Configuration;
        }

        public IEnumerable<VideoContentType> SupportedMediaTypes
        {
            get
            {
                return new[] { VideoContentType.Episode, VideoContentType.Movie };
            }
        }

        private string NormalizeLanguage(string language)
        {
            if (language != null)
            {
                var culture = _localizationManager.FindLanguageInfo(language);
                if (culture != null)
                {
                    return culture.ThreeLetterISOLanguageName;
                }
            }

            return language;
        }

        private async Task<MatchCollection> GetMatches(Stream stream, string pattern)
        {
            using (var reader = new StreamReader(stream))
            {
                var text = (await reader.ReadToEndAsync().ConfigureAwait(false)).Replace("\n", "").Replace("\t", "");
                return Regex.Matches(HttpUtility.HtmlDecode(text), pattern);
            }
        }

        private async Task<List<MatchCollection>> GetMatches(Stream stream, string[] patterns)
        {
            using (var reader = new StreamReader(stream))
            {
                var text = (await reader.ReadToEndAsync().ConfigureAwait(false)).Replace("\n", "").Replace("\t", "");

                var matches = new List<MatchCollection>();
                foreach (var pattern in patterns)
                {
                    matches.Add(Regex.Matches(HttpUtility.HtmlDecode(text), pattern));
                }
                return matches;
            }
        }

        private async Task<HttpResponseMessage> GetResponse(string url, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/{url}");
            request.Headers.Add("Referer", _baseUrl);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0)");
            
            return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private async Task Login(CancellationToken cancellationToken)
        {
            if ((DateTimeOffset.UtcNow - _lastLogin).TotalMinutes < 1)
            {
                return;
            }
            var options = GetOptions();

            var username = options.Username;
            var password = options.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                _logger.LogDebug("No Username");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.LogDebug("No Password");
                return;
            }

            var contentData = new Dictionary<string, string>
            {
                { "username", username },
                { "password", password },
                { "Submit", "Log in" }
            };

            var formUrlEncodedContent = new FormUrlEncodedContent(contentData);
            var requestContentBytes = await formUrlEncodedContent.ReadAsStringAsync().ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/dologin.php")
            {
                Content = new StringContent(requestContentBytes, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded")
            };
            request.Headers.Add("Referer", _baseUrl);
            
            using (var res = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (content.Contains("User <b></b> doesn't exist"))
                    {
                        _logger.LogDebug("User doesn't exist");
                        return;
                    }
                    if (content.Contains("Wrong password"))
                    {
                        _logger.LogDebug("Wrong password");
                        return;
                    }
                    _logger.LogDebug($"{username} Logged in");
                }
            }

            _lastLogin = DateTime.Now;
        }

        private async Task<string> GetShow(string name, CancellationToken cancellationToken)
        {
            var shows = await GetShows(cancellationToken).ConfigureAwait(false);
            return shows.ContainsKey(name) ? shows[name] : "";
        }

        private async Task<Dictionary<string, string>> GetShows(CancellationToken cancellationToken)
        {
            var shows = new Dictionary<string, string>();
            using (var res = await GetResponse("", cancellationToken).ConfigureAwait(false))
            {

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    var showPattern = "<option value=\"(\\d+)\" >(.*?)</option>";
                    var content = await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    var showMatches = await GetMatches(content, showPattern).ConfigureAwait(false);
                    foreach (Match show in showMatches)
                    {
                        if (!shows.ContainsKey(show.Groups[2].Value))
                        {
                            shows.Add(show.Groups[2].Value, show.Groups[1].Value);
                        }
                    }
                }

                return shows;
            }
        }

        private IEnumerable<Addic7edResult> GetEpisode(IEnumerable<Addic7edResult> episodes, int? episodeNum, string language)
        {
            return episodes.Where(i => int.Parse(i.Episode) == episodeNum)
                           .Where(i => i.Language.Equals(language));
        }

        private async Task<IEnumerable<Addic7edResult>> ParseEpisode(HttpResponseMessage res)
        {
            var trPattern = "<tr class=\"epeven completed\">(.*?)</tr>";
            var tdPattern = "<td.*?>(.*?)</td>";

            var content = await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var trMatches = await GetMatches(content, trPattern).ConfigureAwait(false);
            var episodes = new List<Addic7edResult>();
            foreach (Match tr in trMatches)
            {
                var tds = Regex.Matches(tr.Groups[1].Value, tdPattern);
                var result = new Addic7edResult
                {
                    Season = tds[0].Groups[1].Value,
                    Episode = tds[1].Groups[1].Value,
                    Title = (Regex.Match(tds[2].Groups[1].Value, "<a.*?>(.+?)</a>")).Groups[1].Value,
                    Language = NormalizeLanguage(tds[3].Groups[1].Value),
                    Version = tds[4].Groups[1].Value,
                    Completed = tds[5].Groups[1].Value,
                    HearingImpaired = tds[6].Groups[1].Value,
                    Corrected = tds[7].Groups[1].Value,
                    HD = tds[8].Groups[1].Value,
                    Download = (Regex.Match(tds[9].Groups[1].Value, "<a href=\"/(.+?)\">Download</a>")).Groups[1].Value,
                    Multi = tds[10].Groups[1].Value
                };
                episodes.Add(result);
            }

            return episodes;
        }

        private async Task<IEnumerable<Addic7edResult>> GetSeason(string id, int? season, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return new List<Addic7edResult>();
            }
            using (var res = await GetResponse($"ajax_loadShow.php?show={id}&season={season}", cancellationToken).ConfigureAwait(false))
            {
                return await ParseEpisode(res).ConfigureAwait(false);
            }
        }

        private async Task<Dictionary<string, string>> GetMovies(string name, CancellationToken cancellationToken)
        {
            using (var res = await GetResponse($"srch.php?search={name}&Submit=Search", cancellationToken).ConfigureAwait(false))
            {
                var aPattern = "<a href=\"movie/(\\d+)\" debug=\"\\d+\">(.*?)</a><";
                var content = await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var aMatches = await GetMatches(content, aPattern).ConfigureAwait(false);
                var movies = new Dictionary<string, string>();
                foreach (Match a in aMatches)
                {
                    if (!movies.ContainsKey(a.Groups[2].Value))
                    {
                        movies.Add(a.Groups[2].Value, a.Groups[1].Value);
                    }
                }

                return movies;
            }
        }

        private async Task<IEnumerable<Addic7edResult>> ParseMovie(string movie, CancellationToken cancellationToken)
        {
            using (var res = await GetResponse($"movie/{movie}", cancellationToken).ConfigureAwait(false))
            {
                var titlePattern = "<title>.*?Download (.+?) subtitles.*?</title>";
                var verPattern = "Version (.+?),.*?MBs";
                var langPattern = "class=\"language\">(.*?)<";
                var downPattern = "<a class=\"buttonDownload\" href=\"/(.*?)\">";

                var content = await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var matches = await GetMatches(content, new[] { verPattern, langPattern, downPattern, titlePattern }).ConfigureAwait(false);

                var results = new List<Addic7edResult>();
                for (int i = 0; i < matches.FirstOrDefault().Count; i++)
                {
                    var result = new Addic7edResult
                    {
                        Version = matches[0][i].Groups[1].Value,
                        Language = NormalizeLanguage(matches[1][i].Groups[1].Value),
                        Download = matches[2][i].Groups[1].Value,
                        Title = matches[3][0].Groups[1].Value
                    };
                    results.Add(result);
                }

                return results;
            }
        }

        private async Task<IEnumerable<Addic7edResult>> GetMovie(string name, int? productionYear, string language, CancellationToken cancellationToken)
        {
            var movies = await GetMovies(name, cancellationToken).ConfigureAwait(false);
            var namePattern = $"{name} ({productionYear})";
            var movie = movies.ContainsKey(namePattern) ? movies[namePattern] : "";
            if (string.IsNullOrWhiteSpace(movie))
            {
                return new List<Addic7edResult>();
            }
            var results = await ParseMovie(movie, cancellationToken).ConfigureAwait(false);

            return results.Where(i => i.Language.Equals(language));
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> SearchEpisode(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.SeriesName) &&
                request.ParentIndexNumber.HasValue &&
                request.IndexNumber.HasValue &&
                !string.IsNullOrWhiteSpace(request.Language))
            {

                var showId = await GetShow(request.SeriesName, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(showId))
                {
                    return Array.Empty<RemoteSubtitleInfo>();
                }
                var season = await GetSeason(showId, request.ParentIndexNumber, cancellationToken).ConfigureAwait(false);
                var episode = GetEpisode(season, request.IndexNumber, request.Language);
                if (episode.Count() == 0)
                {
                    _logger.LogDebug("No Episode Found");
                    return Array.Empty<RemoteSubtitleInfo>();
                }

                return episode.Select(i => new RemoteSubtitleInfo
                {
                    Id = $"{i.Download.Replace("/", ",")}:{i.Language}",
                    ProviderName = Name,
                    Name = $"{i.Title} - {i.Version} {(i.HearingImpaired.Count() > 0 ? "- Hearing Impaired" : "")}",
                    Format = "srt",
                    ThreeLetterISOLanguageName = i.Language
                });

            }

            return Array.Empty<RemoteSubtitleInfo>();
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> SearchMovie(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.Name) &&
                request.ProductionYear.HasValue &&
                !string.IsNullOrWhiteSpace(request.Language))
            {
                var movie = await GetMovie(request.Name, request.ProductionYear, request.Language, cancellationToken).ConfigureAwait(false);
                if (movie.Count() == 0)
                {
                    _logger.LogDebug("No Movie Found");
                    return Array.Empty<RemoteSubtitleInfo>();
                }

                return movie.Select(i => new RemoteSubtitleInfo
                {
                    Id = $"{i.Download.Replace("/", ",")}:{i.Language}",
                    ProviderName = Name,
                    Name = $"{i.Title} - {i.Version}",
                    Format = "srt",
                    ThreeLetterISOLanguageName = i.Language
                });
            }

            return Array.Empty<RemoteSubtitleInfo>();
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            //await Login(cancellationToken).ConfigureAwait(false);
            if (request.ContentType.Equals(VideoContentType.Episode))
            {
                return await SearchEpisode(request, cancellationToken).ConfigureAwait(false);
            }
            if (request.ContentType.Equals(VideoContentType.Movie))
            {
                return await SearchMovie(request, cancellationToken).ConfigureAwait(false);
            }

            return Array.Empty<RemoteSubtitleInfo>();
        }

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            var idParts = id.Split(new[] { ':' }, 2);
            var download = idParts[0].Replace(",", "/");
            var language = idParts[1];
            var format = "srt";

            using (var stream = await GetResponse(download, cancellationToken).ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(stream.ContentType) ||
                    stream.ContentType.Contains(format))
                {
                    var ms = new MemoryStream();
                    await stream.Content.CopyToAsync(ms).ConfigureAwait(false);
                    ms.Position = 0;
                    return new SubtitleResponse()
                    {
                        Language = language,
                        Stream = ms,
                        Format = format
                    };
                }

                return new SubtitleResponse();
            }
        }

        public void Dispose()
        {
            
        }

        public class Addic7edResult
        {
            public string Season { get; set; }
            public string Episode { get; set; }
            public string Title { get; set; }
            public string Language { get; set; }
            public string Version { get; set; }
            public string Completed { get; set; }
            public string HearingImpaired { get; set; }
            public string Corrected { get; set; }
            public string HD { get; set; }
            public string Download { get; set; }
            public string Multi { get; set; }
            public string Id { get; set; }
        }

    }
}
