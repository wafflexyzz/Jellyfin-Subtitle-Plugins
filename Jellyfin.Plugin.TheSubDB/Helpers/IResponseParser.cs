using System.Collections.Generic;

namespace Jellyfin.Plugin.TheSubDB.Helpers
{
    public interface IResponseParser
    {
        IReadOnlyList<Language> ParseGetAvailablesLanguages(string response);
        IReadOnlyList<Language> ParseSearchSubtitle(string response, bool getVersions);
    }
}
