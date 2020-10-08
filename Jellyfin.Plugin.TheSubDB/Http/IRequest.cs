using System;
using System.Net.Http;

namespace Jellyfin.Plugin.TheSubDB.Models
{
    public interface IRequest
    {
        HttpContent Body { get; }
        Uri EndPoint { get; }
        HttpMethod Method { get; }
    }
}