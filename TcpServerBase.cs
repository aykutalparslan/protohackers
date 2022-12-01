// 
// Project Ferrite is an Implementation of the Telegram Server API
// Copyright 2022 Aykut Alparslan KOC <aykutalparslan@msn.com>
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
// 

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
            _ = ProcessConnection(connection);
        }
    }

    protected abstract Task ProcessConnection(Connection connection);
}