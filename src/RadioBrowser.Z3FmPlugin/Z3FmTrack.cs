using RadioBrowser.PluginContract;

namespace RadioBrowser.Z3FmPlugin;

public class Z3FmTrack
{
    public Z3FmTrack(int id, string title, string artist, int artist_id, string href, int storage_group, string date_create, string icon, int duration)
    {
        Id = id;
        Title = title;
        Artist = artist;
        Artist_Id = artist_id;
        Href = href;
        Storage_Group = storage_group;
        Date_Create = date_create;
        Icon = icon;
        Duration = duration;
    }
    public int Id { get; set; }
    public string Title { get; set; }
    public string Artist { get; set; }
    public int Artist_Id { get; set; }
    public string Href { get; set; }
    public int Storage_Group { get; set; }
    public string Date_Create { get; set; }
    public string Icon { get; set; }
    public int Duration { get; set; }
    public Track ToTrack()
    {
        return new Track(this.Id.ToString(), this.Title, this.Artist, this.Href.Split('/').Last(), TimeSpan.FromSeconds(this.Duration));
    }
}
