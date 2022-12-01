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

public class SmokeTest : TcpServerBase
{
    protected override async Task ProcessConnection(Connection connection)
    {
        connection.Start();
        while (true)
        {
            var result = await connection.Input.ReadAsync();
            var buff = result.Buffer;
            if (buff.IsSingleSegment)
            {
                await connection.Output.WriteAsync(buff.First);
            }
            else
            {
                foreach (var mem in buff)
                {
                    await connection.Output.WriteAsync(mem);
                }
            }
            connection.Input.AdvanceTo(buff.End);
            if (result.IsCompleted || result.IsCanceled)
            {
                break;
            }
        }

        await connection.Output.CompleteAsync();
    }
}