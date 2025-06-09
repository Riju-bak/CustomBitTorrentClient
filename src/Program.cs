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

            // Uncomment this line to pass the first stage
            var encodedValue = param;
            if (Char.IsDigit(encodedValue[0]))
            {
                // Example: "5:hello" -> "hello"
                var colonIndex = encodedValue.IndexOf(':');
                if (colonIndex != -1)
                {
                    var strLength = int.Parse(encodedValue[..colonIndex]);
                    var strValue = encodedValue.Substring(colonIndex + 1, strLength);
                    Console.WriteLine(JsonSerializer.Serialize(strValue));
                }
                else
                {
                    throw new InvalidOperationException("Invalid encoded value: " + encodedValue);
                }
            }
            else if (encodedValue[0]=='i')
            {
                int eIndex = encodedValue.IndexOf('e');
                if (eIndex != -1 && eIndex == encodedValue.Length - 1)
                {
                    // i123e
                    string strNum = encodedValue.Substring(1, eIndex - 1);
                    Console.WriteLine(long.Parse(strNum));
                }
                else throw new InvalidOperationException($"Invalid encoded value: {encodedValue}");
            }
            else
            {
                throw new InvalidOperationException("Unhandled encoded value: " + encodedValue);
            }
        }
        else
        {
            throw new InvalidOperationException($"Invalid command: {command}");
        }
    }    
}

