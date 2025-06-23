using System.Security.Cryptography;

namespace CodeCrafters.Bittorrent;

public class Torrent
{
    public string Announce { get; set; } = null!; //Tracker url
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
        torrent.Announce = Utils.DecodeUtf8String(obj["announce"]);

        if (obj.ContainsKey("comment"))
            torrent.Comment = Utils.DecodeUtf8String(obj["comment"]);
        if(obj.ContainsKey("created by"))
            torrent.CreatedBy = Utils.DecodeUtf8String(obj["created by"]);
        if(obj.ContainsKey("creation date"))
            torrent.CreationDate = Utils.DecodeUtf8String(obj["creation date"]);

        torrent.Info = Info.BencodingObjectToTorrentInfo(obj["info"]);
        return torrent;
    }
}

public class Info
{
    public int Length { get; set; }
    public string Name { get; set; } = null!;
    public int PieceLength { get; set; }            //number of bytes in each piece, an integer
    public byte[][] PieceHashes { get; set; } = null!;

    public byte[] Hash { get; set; } = null!;

    public string HexStringHash
    {
        get
        {
            return String.Join("", Hash.Select(x => x.ToString("x2")));
        }
    }

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
        info.Hash = SHA1.Create().ComputeHash(bytes);
        
        return info;
    }
}

