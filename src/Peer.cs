using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace CodeCrafters.Bittorrent;

public class Peer
{
    public int Id { get; set; } = -1;      //Peer id = -1 implies, id has not been set
    public IPAddress Ip { get; set; }
    public int Port { get; set; }
    
    public Peer(string ip, string port)
    {
        Ip = IPAddress.Parse(ip);
        Port = int.Parse(port);
    }
    
    public byte[] EncodeHandShake(byte[] torrentInfoHash)
    {
        string protocolString = "BitTorrent protocol";
        byte[] bytes = new byte[68];
        int dstOffset = 0;
    
        Console.WriteLine($"The protocolString length: {protocolString.Length}");
        bytes[dstOffset] = (byte)(protocolString.Length);  //length of the protocol string (BitTorrent protocol) which is 19
        dstOffset++;
        
        Buffer.BlockCopy(Encoding.ASCII.GetBytes(protocolString), 0, bytes, dstOffset, protocolString.Length);
        dstOffset += protocolString.Length;
        
        Buffer.BlockCopy(new byte[]{0,0,0,0,0,0,0,0}, 0, bytes, dstOffset, 8);
        dstOffset += 8;
        
        Buffer.BlockCopy(torrentInfoHash, 0, bytes, dstOffset, torrentInfoHash.Length);
        dstOffset += torrentInfoHash.Length;
    
        if (Id == -1)
        {
            //Generate random 20 byte peerId and use it
            byte[] randomPeerIdBytes = new byte[20];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomPeerIdBytes);
            }
            Buffer.BlockCopy(randomPeerIdBytes, 0, bytes, dstOffset, 20);
        }
    
        return bytes;
    }
}