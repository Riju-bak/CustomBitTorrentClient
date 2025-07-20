using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CodeCrafters.Bittorrent;

public class Client
{
    public Torrent Torrent { get; set; }

    public Client(Torrent torrent) => Torrent = torrent;

    public async Task DiscoverPeers()
    {
        //This is a temp bullshit method. It's just to see if I can pass the discover peers stage
        using (HttpClient client = new())
        {
            string? trackerUrl = this.Torrent.Tracker.Address;

            var requestUri = $"{trackerUrl}?" +
                             $"info_hash={Torrent.Info.UrlSafeInfoHash}" +
                             $"&peer_id=00000000000000000000" +
                             $"&port=6881" +
                             "&uploaded=0" +
                             "&downloaded=0" +
                             $"&left={Torrent.Info.Length}"+
                             "&compact=1";
            HttpResponseMessage response = await client.GetAsync(requestUri);
            Console.Error.WriteLine($"request uri to tracker: {requestUri}");
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"Unable to reach tracker: {trackerUrl}");
                return;
            }
            
            byte[] data = await response.Content.ReadAsByteArrayAsync();
            Dictionary<string, object>? info = Bencoding.Decode(data) as Dictionary<string, object>;
            
            if (!(info?["peers"] is byte[] peerInfo))
            {
                Console.Error.WriteLine("Invalid peer info is not correct.");
                return;
            }
            
            List<IPEndPoint> peerIps = new();
            for (int offset = 0; offset < peerInfo.Length; offset += 6)
            {
                IPAddress ipAddress = new IPAddress([ peerInfo[offset], peerInfo[offset+1], peerInfo[offset+2], peerInfo[offset+3]]);
                int port = (peerInfo[offset + 4] << 8) + peerInfo[offset + 5]; //Big endian to int
                peerIps.Add(new IPEndPoint(ipAddress, port));
            }
            
            foreach(IPEndPoint peerIp in peerIps)
                Console.WriteLine(peerIp);
        }
    }

    public async Task<string> SendHandShake(Peer peer)
    {
        TcpClient tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(new IPEndPoint(peer.Ip, peer.Port));
        NetworkStream stream = tcpClient.GetStream();

        byte[] bytes = peer.EncodeHandShake(Torrent.Info.InfoHash);

        // Console.Write("EncodedHandShakeBytes: ");
        // foreach(byte b in bytes)
        //     Console.Write($"{b} ");
        // Console.WriteLine();
        
        await stream.WriteAsync(bytes, 0, bytes.Length); //actually send the handshake message   
        
        // read the 68 byte handshake response
        byte[] response = new byte[68];
        int totalRead = 0;
        while (totalRead < 68)
        {
            int read = await stream.ReadAsync(response, totalRead, 68 - totalRead);
            if (read == 0)
                throw new Exception("Connection closed before the full handshake was received.");
            totalRead += read;
        }
        
        //Extract and assign peerID (last 20 bytes)
        byte[] peerIdBytes = new byte[20];
        Array.Copy(response, 48, peerIdBytes, 0, 20);
        
        stream.Close();
        tcpClient.Close();
        
        return String.Join("", peerIdBytes.Select(x => x.ToString("x2")));
    }
    
}