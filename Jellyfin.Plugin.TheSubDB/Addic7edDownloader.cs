using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Jellyfin.Plugin.TheSubDB.Configuration;
using Jellyfin.Plugin.TheSubDB.Helpers;
using Jellyfin.Plugin.TheSubDB.Http;
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

namespace Jellyfin.Plugin.TheSubDB
{
    public class Addic7edDownloader : ISubtitleProvider
    {

        private static readonly CultureInfo _usCulture = CultureInfo.ReadOnly(new CultureInfo("en-US"));
        private readonly ILogger<Addic7edDownloader> _logger;
        private readonly IFileSystem _fileSystem;
        // These fields are preserved for future rate limiting implementation
        #pragma warning disable CS0169, CS0414
        private DateTime _lastRateLimitException;
        private DateTime _lastLogin;
        private int _rateLimitLeft = 40;
        #pragma warning restore CS0169, CS0414
        private readonly HttpClient _httpClient;
#pragma warning disable CS0169
        private readonly IApplicationHost _appHost;
        #pragma warning restore CS0169
        private ILocalizationManager _localizationManager;
      
        private readonly IServerConfigurationManager _config;

        private readonly SubDBClient _client;

        private readonly string _baseUrl = "https://www.addic7ed.com";
     
        public Addic7edDownloader(ILogger<Addic7edDownloader> logger, HttpClient httpClient, IServerConfigurationManager config, IFileSystem fileSystem, ILocalizationManager localizationManager)
        {
            _logger = logger;
            _httpClient = httpClient;

            _client = new SubDBClient(new System.Net.Http.Headers.ProductHeaderValue("Desktop-Client", "1.0"));
            _config = config;
            _fileSystem = fileSystem;
            _localizationManager = localizationManager;
        }
        public int Order => 3;





        public string Name
        {
            get { return "TheSubDB"; }
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


        public void Dispose()
        {
            // No resources to dispose
        }

        public Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
