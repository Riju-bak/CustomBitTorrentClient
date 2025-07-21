using System.Text;

namespace CodeCrafters.Bittorrent;

public static class Bencoding
{
    private static readonly byte DictionaryStart = (byte)'d';
    public static readonly byte DictionaryEnd = (byte)'e';

    public static readonly byte ListStart = (byte)'l';
    public static readonly byte ListEnd = (byte)'e';

    public static readonly byte NumberStart = (byte)'i';
    public static readonly byte NumberEnd = (byte)'e';

    public static readonly byte ByteArrayDivider = (byte)':';

    #region Decode
    public static object Decode(byte[] bytes)
    {
        IEnumerator<byte> enumerator = ((IEnumerable<byte>)bytes).GetEnumerator();
        enumerator.MoveNext();

        return DecodeNextObject(enumerator);
    }
    

    private static object DecodeNextObject(IEnumerator<byte> enumerator)
    {
        if (enumerator.Current == DictionaryStart)
            return DecodeDictionary(enumerator);
        
        if (enumerator.Current == ListStart)
            return DecodeList(enumerator);

        if (enumerator.Current == NumberStart)
            return DecodeNumber(enumerator);

        //Anything other than dictionary, list or number must be a byte[]?
        // Why? Well string can be thought of as byte[], so that's covered
        //Other than that there's piece hashes, which contains binary blob that C# string cannot handle.
        //C# string is a sequence of unicode characters. piece hashes contains bytes that cannot be interpreted as unicode chars
        //Best to use byte[]
        return DecodeByteArray(enumerator);
    }

    public static object DecodeFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"unable to find file: {path}");
        byte[] bytes = File.ReadAllBytes(path);
        return Decode(bytes);
    }

    private static Dictionary<string, object> DecodeDictionary(IEnumerator<byte> enumerator)
    {
        Dictionary<string, object> dict = new();
        List<string> keys = new();
        while (enumerator.MoveNext())
        {
            if (enumerator.Current == DictionaryEnd) break;

            // It's safe to assume that all keys are valid UTF8 strings
            string key = Encoding.UTF8.GetString(DecodeByteArray(enumerator));
            enumerator.MoveNext();
            object val = DecodeNextObject(enumerator);
            
            keys.Add(key);
            dict.Add(key, val);
        }
        
        //TODO: Figure out what happens if this verification is skipped
        //verify incoming dictionary is sorted correctly,
        //else we won't be able to create identical encoding otherwise
        // var sortedKeys = keys.OrderBy(x => BitConverter.ToString(Encoding.UTF8.GetBytes(x)));
        // if (!keys.SequenceEqual(sortedKeys))
        //     throw new Exception("error loading dictionary: keys not sorted");
        
        return dict;
    }


    private static List<object> DecodeList(IEnumerator<byte> enumerator)
    {
        List<object> list = new();
        while (enumerator.MoveNext())
        {
            if (enumerator.Current == ListEnd) break;
            list.Add(DecodeNextObject(enumerator));
        }

        return list;
    }

    private static long DecodeNumber(IEnumerator<byte> enumerator)
    {
        List<byte> bytes = new List<byte>();

        while (enumerator.MoveNext())
        {
            if (enumerator.Current == NumberEnd) break;
            bytes.Add(enumerator.Current);
        }

        string numAsString = Encoding.UTF8.GetString(bytes.ToArray());
        return long.Parse(numAsString);
    }

    private static byte[] DecodeByteArray(IEnumerator<byte> enumerator)
    {
        //ByteArray = string.
        //Instead of string we're using byte[] bcoz C# treats string as a sequence of unicode characters 
        List<byte> lengthBytes = new List<byte>();

        do
        {
            if (enumerator.Current == ByteArrayDivider)
                break;
            lengthBytes.Add(enumerator.Current);
        } while (enumerator.MoveNext());

        string lengthString = Encoding.UTF8.GetString(lengthBytes.ToArray());
        int length;
        if (!Int32.TryParse(lengthString, out length))
            throw new Exception("unable to parse length of byte array");

        byte[] bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            enumerator.MoveNext();
            bytes[i] = enumerator.Current;
        }

        return bytes;
    }
    #endregion
    
    #region Encode

    public static byte[] Encode(object obj)
    {
        MemoryStream buffer = new MemoryStream();
        EncodeNextObject(buffer, obj);
        return buffer.ToArray();
    }

    private static void EncodeNextObject(MemoryStream buffer, object obj)
    {
        if (obj is byte[])
            EncodeByteArray(buffer, (byte[])obj);
        
        //This will likely never happen because the decoder decodes bencoded strings into byte[] 
        else if (obj is string)
            EncodeString(buffer, (string)obj);
        
        else if (obj is long)
            EncodeNumber(buffer, (long)obj);
        
        else if (obj.GetType() == typeof(List<object>))
            EncodeList(buffer, (List<object>)obj);
        
        else if (obj.GetType() == typeof(Dictionary<string, object>))
            EncodeDictionary(buffer, (Dictionary<string, object>)obj);

        else
            throw new Exception($"Unable to encode type: {obj.GetType()}");
    }


    private static void EncodeNumber(MemoryStream buffer, long input)
    {
        buffer.Append(NumberStart);
        buffer.Append(Encoding.UTF8.GetBytes(Convert.ToString(input)));
        buffer.Append(NumberEnd);
    }

    private static void EncodeByteArray(MemoryStream buffer, byte[] input)
    {
        buffer.Append(Encoding.UTF8.GetBytes(Convert.ToString(input.Length)));
        buffer.Append(ByteArrayDivider);
        buffer.Append(input);
    }

    private static void EncodeString(MemoryStream buffer, string input)
    {
        EncodeByteArray(buffer, Encoding.UTF8.GetBytes(input));
    }

    private static void EncodeList(MemoryStream buffer, List<object> input)
    {
        buffer.Append(ListStart);
        foreach(var elem in input)
            EncodeNextObject(buffer, elem);
        buffer.Append(ListEnd);
    }

    
    private static void EncodeDictionary(MemoryStream buffer, Dictionary<string, object> input)
    {
        buffer.Append(DictionaryStart);
        
        // sort the key by their raw bytes, not their string representation
        var sortedKeys = input.Keys.OrderBy(x => Encoding.UTF8.GetBytes(x), new Utils.ByteArrayComparer());
        foreach (string key in sortedKeys)
        {
            EncodeString(buffer, key);
            EncodeNextObject(buffer, input[key]);
        }
        buffer.Append(DictionaryEnd);
    }
    
    


    
    #endregion
    
    
}

public static class MemoryStreamExtensions
{
 
    //Append a single value to a MemoryStream
    public static void Append(this MemoryStream stream, byte value)
    {
        stream.Append(new[] { value });
    }
    
    
    //Append a byte[] to a MemoryStream
    public static void Append(this MemoryStream stream, byte[] values)
    {
        stream.Write(values, 0, values.Length);
    }
}

