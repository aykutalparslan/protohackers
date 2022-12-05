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