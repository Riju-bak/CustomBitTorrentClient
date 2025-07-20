using System.Net;
using System.Text;

namespace CodeCrafters.Bittorrent;

public class Peer
{
    public int Id { get; set; } = -1;      //Peer id = -1 implies, id has not been set
    public IPAddress Ip { get; set; }
    public int Port { get; set; }

    public Peer(IPAddress ip, int port)
    {
        Ip = ip;
        Port = port;
    }
    
    public Peer(string ip, string port)
    {
        Ip = IPAddress.Parse(ip);
        Port = int.Parse(port);
    }
}