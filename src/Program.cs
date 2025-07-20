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
                string peerId = await client.SendHandShake(peer);
                Console.WriteLine($"Peer ID: {peerId}");
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
        else
        {
            throw new InvalidOperationException($"Invalid command: {command}");
        }
    }

}