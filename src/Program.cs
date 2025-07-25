using System.Net;
using System.Text;
using System.Text.Json;

namespace CodeCrafters.Bittorrent;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Parse arguments
        string? command;
        string? param;
        string peerIpInfo = null!;
        switch (args.Length)
        {
            case 0:
            case 1:
                throw new InvalidOperationException("Usage: your_program.sh <command> <param>");
            case 3:
                //To handle tests - ./your_program.sh handshake sample.torrent <peer_ip>:<peer_port>
                (command, param, peerIpInfo) = (args[0], args[1], args[2]);
                break;
            default:
                (command, param) = (args[0], args[1]);
                break;
        }

        if (param.Contains(".torrent"))
        {
            Client client = new Client(Torrent.LoadFromFile(param));
            
            //A torrent file has been passed as a param
            if(command == "info")
            {
                Console.WriteLine($"Tracker URL: {client.Torrent.Tracker.Address}");
                Console.WriteLine($"Length: {client.Torrent.Info.Length}");
                Console.WriteLine($"Info Hash: {client.Torrent.Info.HexStringInfoHash}");
                Console.WriteLine($"Piece Length: {client.Torrent.Info.PieceLength}");
                Console.WriteLine($"Piece Hashes: {client.Torrent.Info.HexStringPieceHash}");
            }
            else if (command == "peers")
                await client.DiscoverPeers();
            else if (command == "handshake")
            {
                //To handle tests - ./your_program.sh handshake sample.torrent <peer_ip>:<peer_port>
                string peerIp, peerPort;
                string[] peerIpInfoSplit = peerIpInfo!.Split(':');
                (peerIp, peerPort) = (peerIpInfoSplit[0], peerIpInfoSplit[1]);
                
                Peer peer = new Peer( peerIp,  peerPort);
                await client.HandleHandshake(peer);
            }
        }
        
        else if (command == "decode")
        {
            byte[] encodedValueBytes = Encoding.UTF8.GetBytes(param);
            var options = new JsonSerializerOptions();
            options.Converters.Add(new Utils.ByteArrayAsStringConverter());
            
            object decoded = Bencoding.Decode(encodedValueBytes);
            Console.WriteLine(JsonSerializer.Serialize(decoded, options));
        }
        else if (command == "download_piece")
        {
            //Handle tests ./your_program.sh download_piece -o /tmp/test-piece sample.torrent <piece_index>
            string pieceOutputPath, torrentFileName, pieceIndex;
            (pieceOutputPath, torrentFileName, pieceIndex) = (args[2], args[3], args[4]);
            
            Client client = new Client(Torrent.LoadFromFile(torrentFileName));
            
            Console.Error.WriteLine($"Tracker URL: {client.Torrent.Tracker.Address}");
            Console.Error.WriteLine($"Length: {client.Torrent.Info.Length}");
            Console.Error.WriteLine($"Info Hash: {client.Torrent.Info.HexStringInfoHash}");
            Console.Error.WriteLine($"Piece Length: {client.Torrent.Info.PieceLength}");
            Console.Error.WriteLine($"Piece Hashes: {client.Torrent.Info.HexStringPieceHash}");


            await client.DownloadPiece(pieceOutputPath, pieceIndex);
        }
        else
        {
            throw new InvalidOperationException($"Invalid command: {command}");
        }
    }

}