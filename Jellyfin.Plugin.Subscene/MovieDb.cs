using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.Subscene.Models;
using MediaBrowser.Common;

namespace Jellyfin.Plugin.Subscene
{
    public class MovieDb
    {
        private const string token = "d9d7bb04fb2c52c2b594c5e30065c23c";// Get https://www.themoviedb.org/ API token
        private readonly string _movieUrl = "https://api.themoviedb.org/3/movie/{0}?api_key={1}";
        private readonly string _tvUrl = "https://api.themoviedb.org/3/tv/{0}?api_key={1}";
        private readonly string _searchMovie = "https://api.themoviedb.org/3/find/{0}?api_key={1}&external_source={2}";

        private readonly HttpClient _httpClient;
        private readonly IApplicationHost _appHost;
        public MovieDb(HttpClient httpClient, IApplicationHost appHost)
        {
            _httpClient = httpClient;
            _appHost = appHost;
        }

        public async Task<MovieInformation> GetMovieInfo(string id)
        {
            var url = string.Format(_movieUrl, id, token);
            using var request = CreateRequest(url);
            using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
            {
                if (response.Content.Headers.ContentLength <= 0)
                    return null;

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonSerializer.Deserialize<MovieInformation>(json);
            }
        }

        public async Task<FindMovie> SearchMovie(string id)
        {
            var type = id.StartsWith("tt") ? MovieSourceType.imdb_id : MovieSourceType.tvdb_id;
            var url = string.Format(_searchMovie, id, token, type.ToString());
            
            using var request = CreateRequest(url);
            using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
            {
                if (response.Content.Headers.ContentLength <= 0)
                    return null;

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonSerializer.Deserialize<FindMovie>(json);
            }
        }

        public async Task<TvInformation> GetTvInfo(string id)
        {
            var movie = await SearchMovie(id);

            if (movie?.tv_episode_results == null || !movie.tv_episode_results.Any())
                return null;

            var url = string.Format(_tvUrl, movie.tv_episode_results.First().show_id, token);
            using var request = CreateRequest(url);
            using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
            {
                if (response.Content.Headers.ContentLength <= 0)
                    return null;

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonSerializer.Deserialize<TvInformation>(json);
            }
        }


        private HttpRequestMessage CreateRequest(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", $"Jellyfin/{_appHost?.ApplicationVersion}");
            return request;
        }
    }
}
