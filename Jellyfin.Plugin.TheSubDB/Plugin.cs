using System;
using System.Collections.Generic;
using Jellyfin.Plugin.TheSubDB.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.TheSubDB
{
    public class Plugin : BasePlugin<PluginConfiguration>
    {
        public override string Name => "TheSubDB Subtitles";

        public override Guid Id => Guid.Parse("7ea6dafb-c232-4f89-9dd1-f62624df7a90");

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin Instance { get; private set; }


    }
}
