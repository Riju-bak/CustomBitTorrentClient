using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeCrafters.Bittorrent;

public static class Utils
{
    public static string DecodeUtf8String(object obj)
    {
        byte[]? bytes = obj as byte[];
        if (bytes == null) throw new Exception("unable to decode utf-8 string, object is not a byte array");
        return Encoding.UTF8.GetString(bytes);
    }
    
    public class ByteArrayAsStringConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string s = reader.GetString()!;
            return Encoding.UTF8.GetBytes(s);
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            string s = Encoding.UTF8.GetString(value);
            writer.WriteStringValue(s);
        }
    }
    
    public class ByteArrayComparer : IComparer<byte[]>
    {
        public int Compare(byte[]? a, byte[]? b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null || b==null) return -1;
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                int cmp = a[i].CompareTo(b[i]);
                if (cmp != 0) return cmp;
            }
            return a.Length.CompareTo(b.Length);
        }
    }

}