using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;

namespace protohackers;

public class UnusualDatabaseProgram : UdpServerBase
{
    private readonly ConcurrentDictionary<byte[], byte[]> _data = new(new ArrayComparer());
    private byte[] Version => "version=Ken's Key-Value Store 1.0"u8.ToArray();
    protected override async Task ProcessDatagram(byte[] data, IPEndPoint endPoint)
    {
        Console.WriteLine("Processing datagram..");
        if(data.Length > 1000) return;
        int pos = 0;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == '=')
            {
                pos = i;
                break;
            }

            if (i == data.Length - 1)
            {
                pos = -1;
            }
        }

        if (data.SequenceEqual("version"u8.ToArray()))
        {
            Console.WriteLine("--> version");
            await SendAsync(Version, endPoint);
        }
        else if (pos == -1)
        {
            if (_data.TryGetValue(data, out var value))
            {
                await SendAsync(value, endPoint);
            }
        }
        else if (pos == 0)
        {
            Console.WriteLine("--> add empty key");
            _data.TryAdd(Array.Empty<byte>(), data[1..]);
        }
        else if(pos == data.Length - 1)
        {
            Console.WriteLine("--> add empty val");
            _data.TryAdd(data[..^1], Array.Empty<byte>());
        }
        else
        {
            Console.WriteLine("--> add");
            _data.TryAdd(data[..pos], data[(pos + 1)..]);
        }
    }
    private class ArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y)
        {
            return y != null && x != null && x.SequenceEqual(y);
        }

        public int GetHashCode(byte[] obj)
        {
            var md5 = System.Security.Cryptography.MD5.HashData(obj);
            return BinaryPrimitives.ReadInt32LittleEndian(md5);
        }
    }
}