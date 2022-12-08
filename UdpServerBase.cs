using System.Net;
using System.Net.Sockets;

namespace protohackers;

public abstract class UdpServerBase
{
    private UdpClient? _client;
    public async Task Start(int port)
    {
        var endPoint = new IPEndPoint(IPAddress.Any, port);
        _client = new UdpClient(endPoint);
        while (true)
        {
            var result = await _client.ReceiveAsync();
            await ProcessDatagram(result.Buffer, result.RemoteEndPoint);
        }
    }
    protected abstract Task ProcessDatagram(byte[] data, IPEndPoint endPoint);

    protected async ValueTask<int> SendAsync(ReadOnlyMemory<byte> datagram, IPEndPoint endPoint)
    {
        if (_client != null) return await _client.SendAsync(datagram, endPoint);
        return 0;
    }
}