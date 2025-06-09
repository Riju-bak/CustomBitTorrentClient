using System.Text.Json;

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

        // Parse command and act accordingly
        if (command == "decode")
        {
            // You can use print statements as follows for debugging, they'll be visible when running tests.
            Console.Error.WriteLine("Logs from your program will appear here!");

            var encodedValue = param;
            int index = 0;
            var decoded = DecodeEncodedValue(encodedValue, ref index);
            Console.WriteLine(JsonSerializer.Serialize(decoded));
        }
        else
        {
            throw new InvalidOperationException($"Invalid command: {command}");
        }
    }

    private static object DecodeEncodedValue(string input, ref int index)
    {
        char current = input[index];

        if (char.IsDigit(current))
        {
            // Parse string: <length>:<string>
            int colonIndex = input.IndexOf(':', index);
            int length = int.Parse(input.Substring(index, colonIndex - index));
            index = colonIndex + 1;
            string strValue = input.Substring(index, length);
            index += length;
            return strValue;
        }
        else if (current == 'i')
        {
            // Parse integer: i<digits>e
            index++; // Skip 'i'
            int endIndex = input.IndexOf('e', index);
            string numStr = input.Substring(index, endIndex - index);
            long number = long.Parse(numStr);
            index = endIndex + 1;
            return number;
        }
        else if (current == 'l')
        {
            // Parse list: l<values>e
            index++; // Skip 'l'
            var list = new List<object>();
            while (input[index] != 'e')
            {
                list.Add(DecodeEncodedValue(input, ref index));
            }
            index++; // Skip 'e'
            return list;
        }
        else if (current == 'd')
        {
            // Parse dictionary: d<key><value>e
            index++; // Skip 'd'
            var dict = new Dictionary<string, object>();
            while (input[index] != 'e')
            {
                // Keys must be strings
                var keyObj = DecodeEncodedValue(input, ref index);
                if (keyObj is not string key)
                {
                    throw new InvalidOperationException("Dictionary keys must be strings");
                }
                var value = DecodeEncodedValue(input, ref index);
                dict[key] = value;
            }
            index++; // Skip 'e'
            return dict;
        }
        else
        {
            throw new InvalidOperationException("Unhandled encoded value starting at: " + input[index]);
        }
    }
    
}

