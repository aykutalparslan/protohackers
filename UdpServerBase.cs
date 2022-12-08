using System.Net;
using System.Net.Sockets;
using System.Text;

namespace protohackers;

public abstract class UdpServerBase
{
    private UdpClient? _client;
    private UdpClient? _sender;
    public async Task Start(int port)
    {
        var endPoint = new IPEndPoint(IPAddress.Any, port);
        _client = new UdpClient(endPoint);
        _sender = new UdpClient(port);
        while (true)
        {
            var result = await _client.ReceiveAsync();
            await ProcessDatagram(result.Buffer, result.RemoteEndPoint);
        }
    }
    protected abstract Task ProcessDatagram(byte[] data, IPEndPoint endPoint);

    protected async ValueTask<int> SendAsync(ReadOnlyMemory<byte> datagram, IPEndPoint endPoint)
    {
        Console.WriteLine($"---");
        Console.WriteLine($"{Encoding.UTF8.GetString(datagram.Span)}");
        Console.WriteLine($"Sending datagram to {endPoint.Address} - {endPoint.Port}");
        if (_sender != null) return await _sender.SendAsync(datagram, endPoint);
        return 0;
    }
}