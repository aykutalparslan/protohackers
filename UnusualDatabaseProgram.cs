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
            await SendAsync(Version, endPoint);
        }
        else if (pos == -1)
        {
            if (_data.TryGetValue(data, out var value))
            {
                await SendAsync(data.Concat(new[] { (byte)'=' }).Concat(value).ToArray(), endPoint);
            }
        }
        else if (pos == 0)
        {
            _data.AddOrUpdate(Array.Empty<byte>(), data[1..],
                (key, oldValue) => data[1..]);
        }
        else if(pos == data.Length - 1)
        {
            _data.AddOrUpdate(data[..^1], Array.Empty<byte>(),
                (key, oldValue) => Array.Empty<byte>());
        }
        else
        {
            _data.AddOrUpdate(data[..pos], data[(pos + 1)..],
                (key, oldValue) => data[(pos + 1)..]);
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