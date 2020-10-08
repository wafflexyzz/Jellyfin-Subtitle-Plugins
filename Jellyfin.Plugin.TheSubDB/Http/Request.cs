using System;
using System.Net.Http;

namespace Jellyfin.Plugin.TheSubDB.Models
{
    public class Request : IRequest
    {
        public Uri EndPoint { get; set; }
        public HttpMethod Method { get; set; }
        public HttpContent Body { get; set; }
    }
}
