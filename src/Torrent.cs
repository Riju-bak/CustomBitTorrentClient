using System.Net;
using System.Security.Cryptography;

namespace CodeCrafters.Bittorrent;

public class Torrent
{
    public Tracker Tracker { get; set; } = null!; //Might have to update this to List<Tracker> to support for multiple trackers
    public string Comment { get; set; } = null!;
    public string CreatedBy { get; set; } = null!;
    public string CreationDate { get; set; } = null!;
    public Info Info { get; set;} = null!;

    public static Torrent LoadFromFile(string path)
    {
        object bencodingObj = Bencoding.DecodeFile(path);
        return BencodingObjectToTorrent(bencodingObj);
    }

    public static Torrent BencodingObjectToTorrent(object bencodingObj)
    {
        Dictionary<string, object> obj = (Dictionary<string, object>)bencodingObj;
        
        Console.Error.WriteLine($"obj: {obj["announce"]}");

        if (!obj.ContainsKey("announce"))
            throw new Exception("Missing announce section");

        if (!obj.ContainsKey("info"))
            throw new Exception("Missing info section");
        
        Torrent torrent = new();
        torrent.Tracker = new Tracker(Utils.DecodeUtf8String(obj["announce"]));

        if (obj.ContainsKey("comment"))
            torrent.Comment = Utils.DecodeUtf8String(obj["comment"]);
        if(obj.ContainsKey("created by"))
            torrent.CreatedBy = Utils.DecodeUtf8String(obj["created by"]);
        if(obj.ContainsKey("creation date"))
            torrent.CreationDate = Utils.DecodeUtf8String(obj["creation date"]);

        torrent.Info = Info.BencodingObjectToTorrentInfo(obj["info"]);
        return torrent;
    }
    
    public async Task DiscoverPeers()
    {
        //This is a temp bullshit method. It's just to see if I can pass the discover peers stage
        using (HttpClient client = new())
        {
            string trackerUrl = this.Tracker.Address;

            var requestUri = $"{trackerUrl}?" +
                             $"info_hash={this.Info.UrlSafeInfoHash}" +
                             $"&peer_id=00000000000000000000" +
                             $"&port=6881" +
                             "&uploaded=0" +
                             "&downloaded=0" +
                             $"&left={this.Info.Length}"+
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

}

public class Info
{
    public int Length { get; set; }     //Size of the file in bytes
    public string Name { get; set; } = null!;
    public int PieceLength { get; set; }            //number of bytes in each piece, an integer
    public byte[][] PieceHashes { get; set; } = null!;

    public byte[] InfoHash { get; set; } = null!;

    public string HexStringInfoHash
    {
        get
        {
            return String.Join("", InfoHash.Select(x => x.ToString("x2")));
        }
    }

    public string UrlSafeInfoHash => string.Concat(InfoHash.Select(b => $"%{b:X2}"));   //custom getter with user expression body

    public string HexStringPieceHash
    {
        get
        {
            //Here SelectMany flattens the byte[][] into IEnumerable<string> 
            return String.Concat(PieceHashes.SelectMany(pieceHash => pieceHash.Select(x => x.ToString("x2"))));
        }
    }


    public static Info BencodingObjectToTorrentInfo(object obj)
    {
        Info info = new();
        Dictionary<string, object> infoDict = (Dictionary<string, object>)obj;
        if (infoDict.ContainsKey("length"))
            info.Length = Convert.ToInt32(infoDict["length"]);
        if (infoDict.ContainsKey("name"))
            info.Name = Utils.DecodeUtf8String(infoDict["name"]);
        if (infoDict.ContainsKey("piece length"))
            info.PieceLength = Convert.ToInt32(infoDict["piece length"]);

        if (infoDict.ContainsKey("pieces"))
        {
            byte[] concatenatedPieceHashes = (byte[])infoDict["pieces"];
            int pieceCount = info.Length / info.PieceLength;
            info.PieceHashes = new byte[pieceCount][];

            for (int i = 0; i < pieceCount; i++)
            {
                info.PieceHashes[i] = new byte[20];
                Buffer.BlockCopy(concatenatedPieceHashes, i * 20, info.PieceHashes[i], 0, 20); 
            }
        }

        byte[] bytes = Bencoding.Encode(infoDict);
        info.InfoHash = SHA1.Create().ComputeHash(bytes);
        
        return info;
    }
}

