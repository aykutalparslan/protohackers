using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using protohackers.Transport;

namespace protohackers;

public abstract class TcpServerBase
{
    public async Task Start(int port)
    {
        var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        listenSocket.Listen(128);
        
        await AcceptConnections(listenSocket);
    }
    private async Task AcceptConnections(Socket listenSocket)
    {
        var transportScheduler = new IOQueue();
        var applicationScheduler = PipeScheduler.ThreadPool;
        var senderPool = new SenderPool();
        var memoryPool = new PinnedBlockMemoryPool();
        while (listenSocket.IsBound)
        {
            var socket = await listenSocket.AcceptAsync();
            var connection = new Connection(socket, senderPool,
                transportScheduler, applicationScheduler, memoryPool);
            connection.Start();
            _ = ProcessConnection(connection);
        }
    }

    protected abstract Task ProcessConnection(Connection connection);
}