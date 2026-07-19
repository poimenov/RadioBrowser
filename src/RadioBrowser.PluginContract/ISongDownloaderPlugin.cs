namespace RadioBrowser.PluginContract;

public interface ISongDownloaderPlugin
{
    string PluginName { get; }
    Task<OperationResult<IEnumerable<Track>>> Search(string keyword, CancellationToken cancellationToken, int limit);
    Task<OperationResult> Download(Track track, string destinationPath, CancellationToken cancellationToken);
}
