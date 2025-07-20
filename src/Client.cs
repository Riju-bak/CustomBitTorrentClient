using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace CodeCrafters.Bittorrent;

public class Client
{
    public byte[] ClientId { get; set; }  //The client has it's own Id, since client is also a peer in the bittorrent P2P network
    public Torrent Torrent { get; set; }

    public List<Peer> Peers { get; set; }

    public TcpClient TcpClient { get; set; }

    public Client(Torrent torrent)
    {
        //Generate random 20 byte peerId for the client
        byte[] randomPeerIdBytes = new byte[20];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomPeerIdBytes);
        }

        ClientId = randomPeerIdBytes;

        Torrent = torrent;
        Peers = new();
        TcpClient = new TcpClient();
    }

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

            foreach (IPEndPoint peerIp in peerIps)
            {
                Console.WriteLine(peerIp);
                Peers.Add(new Peer(peerIp.Address, peerIp.Port));
            }
        }
    }

    public async Task SendHandShake()
    {
        NetworkStream stream = TcpClient.GetStream();

        byte[] EncodeHandShake(byte[] torrentInfoHash, byte[] clientIdBytes)
        {
            string protocolString = "BitTorrent protocol";
            byte[] bytes = new byte[68];
            int dstOffset = 0;
    
            Console.WriteLine($"The protocolString length: {protocolString.Length}");
            bytes[dstOffset] = (byte)(protocolString.Length);  //length of the protocol string (BitTorrent protocol) which is 19
            dstOffset++;
        
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(protocolString), 0, bytes, dstOffset, protocolString.Length);
            dstOffset += protocolString.Length;
        
            Buffer.BlockCopy(new byte[]{0,0,0,0,0,0,0,0}, 0, bytes, dstOffset, 8);
            dstOffset += 8;
        
            Buffer.BlockCopy(torrentInfoHash, 0, bytes, dstOffset, torrentInfoHash.Length);
            dstOffset += torrentInfoHash.Length;
    
            Buffer.BlockCopy(clientIdBytes, 0, bytes, dstOffset,  clientIdBytes.Length);    
            return bytes;
        }

        byte[] bytes = EncodeHandShake(Torrent.Info.InfoHash, ClientId);
        
        await stream.WriteAsync(bytes, 0, bytes.Length); //actually send the handshake message
        
        //NOTE for learning. Don't close the stream, it also closes the TCP Connection and we don't want that.
        // stream.Close(); 
    }

    public async Task<string> ReadPeerHandshakeResponse()
    {
        NetworkStream stream = TcpClient.GetStream();
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
        return String.Join("", peerIdBytes.Select(x => x.ToString("x2")));
    }

    public async Task ConnectTo(Peer peer)
    {
        await TcpClient.ConnectAsync(new IPEndPoint(peer.Ip, peer.Port));
    }

    public void Disconnect() => TcpClient.Close();

    public async Task DownloadPiece(string pieceOutputPath, string pieceIndex)
    {
        await DiscoverPeers();      //Get the list of peers from the tracker
            
        //For now it is safe to assume that all peers have all pieces available. Using a single peer for now.
        //TODO: Add support for multiple peers
        Peer peer = Peers[0];
        await ConnectTo(peer);
        await SendHandShake();
        await ReadPeerHandshakeResponse();
        await ExchangePeerMessages();
        
        Disconnect();
    }

    private async Task ExchangePeerMessages()
    {
        // Now read the BitField message (after handshake)
        await WaitForBitFieldMessage();

        // await SendInterestedMessage();


    }

    private async Task WaitForBitFieldMessage()
    {
        NetworkStream stream = this.TcpClient.GetStream();
        
        // First read the 4-byte length prefix
        byte[] lengthPrefix = new byte[4];
        int totalRead = 0;
        while (totalRead < 4)
        {
            int read = await stream.ReadAsync(lengthPrefix, totalRead, 4 - totalRead);
            if (read == 0)
                throw new Exception("Connection closed before reading length prefix of BitField.");
            totalRead += read;
        }
        int messageLength = BitConverter.ToInt32(lengthPrefix.Reverse().ToArray(), 0);  // Big-endian to int

        // Read the rest of the message (messageLength bytes)
        byte[] message = new byte[messageLength];
        totalRead = 0;
        while (totalRead < messageLength)
        {
            int read = await stream.ReadAsync(message, totalRead, messageLength - totalRead);
            if (read == 0)
                throw new Exception("Connection closed before reading full BitField message.");
            totalRead += read;
        }

        // Check that the message is a BitField (ID 5)
        if (message[0] != 5)
            throw new Exception($"Expected BitField message (ID 5), got ID {message[0]} instead.");
    }

    public async Task HandleHandshake(Peer peer)
    {
        await this.ConnectTo(peer);
        await this.SendHandShake();
        string peerId = await this.ReadPeerHandshakeResponse();
        Console.WriteLine($"Peer ID: {peerId}");
    }
}