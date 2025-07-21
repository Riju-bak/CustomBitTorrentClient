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

            Buffer.BlockCopy(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, 0, bytes, dstOffset, 8);
            dstOffset += 8;

            Buffer.BlockCopy(torrentInfoHash, 0, bytes, dstOffset, torrentInfoHash.Length);
            dstOffset += torrentInfoHash.Length;

            Buffer.BlockCopy(clientIdBytes, 0, bytes, dstOffset, clientIdBytes.Length);
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

        return String.Join("", peerIdBytes.Select(x => x.ToString("x2")));
    }

    public async Task ConnectTo(Peer peer)
    {
        await TcpClient.ConnectAsync(new IPEndPoint(peer.Ip, peer.Port));
    }

    public void Disconnect()
    {
        Console.Error.WriteLine("Closing the connection to peer ...");
        TcpClient.Close();
    }
    
    public async Task SavePieceToDisk(byte[] pieceData, string outputPath)
    {
        using FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await fs.WriteAsync(pieceData, 0, pieceData.Length);
        await fs.FlushAsync(); // ensure it's written to disk
        Console.Error.WriteLine($"Saved piece to {outputPath}");
    }

    private bool VerifyPiece(byte[] pieceData, byte[] expectedHash)
    {
        byte[] actualHash = SHA1.Create().ComputeHash(pieceData);
        return actualHash.SequenceEqual(expectedHash);
    }

    public async Task DownloadPiece(string pieceOutputPath, string pieceIndex)
    {
        int pieceIdx = int.Parse(pieceIndex);
        await DiscoverPeers(); //Get the list of peers from the tracker

        //For now it is safe to assume that all peers have all pieces available. Using a single peer for now.
        //TODO: Add support for multiple peers
        Peer peer = Peers[0];
        await ConnectTo(peer);
        await SendHandShake();
        await ReadPeerHandshakeResponse();
        
        byte[] fullPieceData = await GetFullPiece(pieceIdx);
        
        bool isValid = VerifyPiece(fullPieceData, Torrent.Info.PieceHashes[pieceIdx]);
        if (isValid)
        {
            //Save piece to disk
            Console.Error.WriteLine("Piece integrity is valid => saving to disk");
            await SavePieceToDisk(fullPieceData, pieceOutputPath);
        }
        else
        {
            //Handle invalid piece.
            //Re-request the piece
            // might retry a few times before giving up on a bad piece.
        }
            
        Disconnect();
    }
    
    private async Task<byte[]> GetFullPiece(int pieceIndex)
    {
        // Now read the BitField message (after handshake)
        await WaitForBitFieldMessage();

        await SendInterestedMessage();

        await WaitForUnchokeMessage();
        
        await SendRequestForAllBlocksInPiece(pieceIndex);

        Dictionary<int, byte[]>blocks = await WaitForEachPieceMessage();
        
        
        // //Put the blocks together
        byte[] fullPiece = new byte[Torrent.Info.PieceLength];
        foreach (var kvp in blocks.OrderBy(kvp => kvp.Key))
        {
            Array.Copy(kvp.Value, 0, fullPiece, kvp.Key, kvp.Value.Length);
        }
        
        return fullPiece;
    }

    private async Task WaitForBitFieldMessage()
    {
        //WARNING: Not exactly waiting for BitFieldMessage => Might cause issues later.
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

        int messageLength = BitConverter.ToInt32(lengthPrefix.Reverse().ToArray(), 0); // Big-endian to int

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

    private async Task SendInterestedMessage()
    {
        NetworkStream stream = this.TcpClient.GetStream();

        // Send 'interested' message (length: 1, ID: 2). Payload is empty
        byte[] interestedMessage = new byte[5];
        interestedMessage[0] = 0; // length prefix (4 bytes) = 0 0 0 1
        interestedMessage[1] = 0;
        interestedMessage[2] = 0;
        interestedMessage[3] = 1;
        interestedMessage[4] = 2; // message ID = 2 (interested)

        await stream.WriteAsync(interestedMessage, 0, interestedMessage.Length);
    }

    private async Task WaitForUnchokeMessage()
    {
        //WARNING: Not exactly waiting for unchoke => might cause issues later
        NetworkStream stream = this.TcpClient.GetStream();
        // Read 4-byte length prefix
        byte[] lengthPrefix = new byte[4];
        int totalRead = 0;
        while (totalRead < 4)
        {
            int read = await stream.ReadAsync(lengthPrefix, totalRead, 4 - totalRead);
            if (read == 0)
                throw new Exception("Connection closed before reading unchoke length prefix.");
            totalRead += read;
        }

        int messageLength = BitConverter.ToInt32(lengthPrefix.Reverse().ToArray(), 0);

        // Read the next `messageLength` bytes (should be 1)
        if (messageLength != 1)
            throw new Exception($"Expected unchoke message length of 1, got {messageLength}.");

        // Read the message ID
        int messageId = stream.ReadByte(); //Really? will this actually read the correct byte??
        if (messageId != 1)
            throw new Exception($"Expected unchoke message (ID 1), but got ID {messageId}.");
    }

    private async Task SendRequestForAllBlocksInPiece(int pieceIndex)
    {
        NetworkStream stream = this.TcpClient.GetStream();

        int blockSize = 16 * 1024; //Each block is 16KB
        int pieceLength = this.Torrent.Info.PieceLength;
        
        Console.Error.WriteLine($"Sending req for all blocks piecelength: {Torrent.Info.PieceLength}");
        Console.Error.WriteLine($"Sending req for all blocks infohash: {Torrent.Info.HexStringInfoHash}");
        
        int numBlocks = (int)Math.Ceiling((double)pieceLength / blockSize);

        for (int blockIndex = 0; blockIndex < numBlocks; blockIndex++)
        {
            int begin = blockIndex * blockSize;
            int length = Math.Min(blockSize, pieceLength - begin);

            Console.Error.WriteLine($"Req block {blockIndex} len: {length}");

            byte[]
                request = new byte[17]; // 4(msg_len) + 1(msg_id) + 4(piece index) + 4(byte offset in piece) + 4(length of block) = 17B total

            // Length prefix = 13
            //This converts the integer 13 from host byte order (the order used by the local machine) to network byte order (big-endian).
            byte[] lengthPrefix = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(13));

            Array.Copy(lengthPrefix, 0, request, 0, 4);

            request[4] = 6; // message ID = 6 (request)

            // Piece index
            Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(pieceIndex)), 0, request, 5, 4);

            // Begin offset
            Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(begin)), 0, request, 9, 4);

            // Block length
            Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(length)), 0, request, 13, 4);

            await stream.WriteAsync(request, 0, request.Length);
        }

    }

    private async Task<Dictionary<int, byte[]>> WaitForEachPieceMessage()
    {
        NetworkStream stream = this.TcpClient.GetStream();

        int blockSize = 16 * 1024; //Each block is 16KB
        int pieceLength = this.Torrent.Info.PieceLength;
        int numBlocks = (int)Math.Ceiling((double)pieceLength / blockSize);

        Dictionary<int, byte[]> blocks = new();

        while (blocks.Count < numBlocks)
        {
            Console.Error.WriteLine($"Downloading block {blocks.Count}");
            // Read length prefix
            byte[] lengthPrefix = new byte[4];
            await stream.ReadAsync(lengthPrefix, 0, 4);
            int messageLength = BitConverter.ToInt32(lengthPrefix.Reverse().ToArray(), 0);

            // Read rest of the message
            byte[] message = new byte[messageLength];
            await stream.ReadAsync(message, 0, messageLength);

            byte messageId = message[0];

            if (messageId != 7)
            {
                Console.WriteLine($"Ignoring message ID {messageId}");
                continue;
            }

            // Parse index and begin
            int index = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(message, 1)); // 0-based piece index
            int begin = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(message, 5)); // 0-based byte offset within the piece

            // Get block (the data for the piece, usually 16 * 1024 bytes long)
            int blockOffset = 9;
            int blockLength = message.Length - blockOffset;
            byte[] block = new byte[blockLength];
            Array.Copy(message, blockOffset, block, 0, blockLength);

            blocks[begin] = block;
        }
        return blocks;
    }

    public async Task HandleHandshake(Peer peer)
    {
        await this.ConnectTo(peer);
        await this.SendHandShake();
        string peerId = await this.ReadPeerHandshakeResponse();
        Console.WriteLine($"Peer ID: {peerId}");
    }
}

public static class NetworkStreamExtensions
{
    public static async Task ReadExactlyAsync(this NetworkStream stream, byte[] buffer, int offset, int count)
    {
        int readTotal = 0;
        while (readTotal < count)
        {
            int read = await stream.ReadAsync(buffer, offset + readTotal, count - readTotal);
            if (read == 0)
                throw new IOException("Connection closed while reading data.");
            readTotal += read;
        }
    }
}
