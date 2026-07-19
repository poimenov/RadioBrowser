namespace RadioBrowser.PluginContract;

public class Track
{
    public Track(string id, string title, string artist, string fileName, TimeSpan duration)
    {
        Id = id;
        Title = title;
        Artist = artist;
        FileName = fileName;
        Duration = duration;
    }
    public string Id { get; set; }
    public string Title { get; set; }
    public string Artist { get; set; }
    public TimeSpan Duration { get; set; }
    public string DisplayDuration => String.Format("{0:D2}:{1:D2}", Duration.Minutes, Duration.Seconds);
    public string FileName { get; set; }
}
