using Jellyfin.Plugin.TheSubDB.Http;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.TheSubDB
{
    public interface ISubDBApi
    {
        Task<Response> DownloadSubtitle(string hash, params string[] languages);
        Task<Response> GetAvailableLanguagesAsync();
        Task<Response> SearchSubtitle(string hash, bool getVersions = false);
        Task<Response> UploadSubtitle(string subtitle, string movie);
    }
}