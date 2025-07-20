using System.Net;

namespace CodeCrafters.Bittorrent;

public class Tracker
{
    public event EventHandler<List<IPEndPoint>>? PeerListUpdated;

    protected virtual void OnPeerListUpdated(List<IPEndPoint> e)
    {
        PeerListUpdated?.Invoke(this, e);
    }
    
    public string? Address { get; set; }

    public Tracker(string address) => Address = address;

}

public enum TrackerEvent
{
    Started,
    Paused,
    Stopped
}