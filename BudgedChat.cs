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
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using protohackers.Transport;

namespace protohackers;

public class BudgedChat : TcpServerBase
{
    private static byte[] WelcomeMessage => "Welcome to budgetchat! What shall I call you?\n"u8.ToArray();
    private ConcurrentDictionary<string, WeakReference<Connection>> _users = new();
    protected override async Task ProcessConnection(Connection connection)
    {
        string? username = null;
        await connection.Output.WriteAsync(WelcomeMessage);
        bool completed = false;
        while (!completed)
        {
            var result = await connection.Input.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;
            SequencePosition? position = null;
            do
            {
                position = buffer.PositionOf((byte)'\n');
                if (position != null)
                {
                    
                    if (username == null)
                    {
                        var message = buffer.Slice(0, position.Value);
                        username = Encoding.UTF8.GetString(message);
                        if (!Regex.IsMatch(username, "[^a-zA-Z0-9]"))
                        {
                            completed = true;
                            break;
                        }
                        _users.TryAdd(username, new WeakReference<Connection>(connection));
                        await connection.Output.WriteAsync(GenerateExistingUsersMessage(username));
                        await WalkConnections(username, GenerateUserJoinedMessage(username));
                    }
                    else
                    {
                        var message = buffer.Slice(0, 
                            buffer.GetPosition(1, position.Value));
                        await WalkConnections(username, 
                            GenerateMessage(username, message));
                    }
                    buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                }
            } while (position != null);
            
            if (result.IsCanceled || 
                result.IsCompleted || 
                completed)
            {
                break;
            }
            
            connection.Input.AdvanceTo(buffer.Start, buffer.End);
        }
        
        await connection.Output.CompleteAsync();
        if (username != null)
        {
            _users.TryRemove(username, out var reference);
            await WalkConnections(username, GenerateUserLeftMessage(username));
        }
    }

    private ReadOnlyMemory<byte> GenerateMessage(string username, ReadOnlySequence<byte> message)
    {
        StringBuilder sb = new StringBuilder("[");
        sb.Append(username);
        sb.Append("] ");
        sb.Append(Encoding.UTF8.GetString(message));
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private ReadOnlyMemory<byte> GenerateUserJoinedMessage(string username)
    {
        return Encoding.UTF8.GetBytes("* "+ username + " has entered the room\n");
    }
    
    private ReadOnlyMemory<byte> GenerateUserLeftMessage(string username)
    {
        return Encoding.UTF8.GetBytes("* "+ username + " has left the room\n");
    }

    private async Task WalkConnections(string username, ReadOnlyMemory<byte> message)
    {
        foreach (var(u, r) in _users)
        {
            if (u != username && r.TryGetTarget(out var c))
            {
                await c.Output.WriteAsync(message);
            }
        }
    }

    private ReadOnlyMemory<byte> GenerateExistingUsersMessage(string username)
    {
        StringBuilder sb = new StringBuilder("* The room contains: ");
        bool first = true;
        foreach (var(u, r) in _users)
        {
            if (u != username && r.TryGetTarget(out var c))
            {
                if (!first)
                {
                    first = false;
                    sb.Append(", ");
                }

                sb.Append(u);
            }
        }
        sb.Append('\n');

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}