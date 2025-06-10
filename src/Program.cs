using System.Text;
using System.Text.Json;
using CodeCrafters.Bittorrent;

public class Program
{
    public static void Main(string[] args)
    {
        // Parse arguments
        string? command;
        string? param;
        switch (args.Length)
        {
            case 0:
            case 1:
                throw new InvalidOperationException("Usage: your_program.sh <command> <param>");
            default:
                (command, param) = (args[0], args[1]);
                break;
        }

        if (param.Contains(".torrent"))
        {
            string torrentFile = param;
            Torrent torrent = Torrent.LoadFromFile(torrentFile);
            
            //A torrent file has been passed as a param
            if(command == "info")
            {
                Console.WriteLine($"Tracker URL: {torrent.Announce}");
                Console.WriteLine($"Length: {torrent.Info.Length}");
                Console.WriteLine($"Info Hash: {torrent.Info.HexStringHash}");
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

