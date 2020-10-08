namespace Jellyfin.Plugin.TheSubDB
{
    public interface ILanguage
    {
        int Count { get; }
        string Name { get; }

        string ToString();
    }
}
