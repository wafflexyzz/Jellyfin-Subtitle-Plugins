using System.Collections.Generic;
using System.Net;

namespace Jellyfin.Plugin.TheSubDB.Http
{
    public interface IResponse
    {
        object Body { get; }
        IReadOnlyDictionary<string, string> Headers { get; }
        HttpStatusCode StatusCode { get; }
    }
}