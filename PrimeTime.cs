using System.Buffers;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using protohackers.Transport;

namespace protohackers;

public class PrimeTime : TcpServerBase
{
    private byte[] ResponseTrue => "{\"method\":\"isPrime\",\"prime\":true}\n"u8.ToArray();
    private byte[] ResponseFalse => "{\"method\":\"isPrime\",\"prime\":false}\n"u8.ToArray();
    private byte[] ResponseMalformed => "{\"method\":\"isPrime\"}\n"u8.ToArray();
    protected override async Task ProcessConnection(Connection connection)
    {
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
                    var response = ProcessRequest(buffer.Slice(0, position.Value));
                    if (response == ResponseMalformed)
                    {
                        completed = true;
                        break;
                    }
                    await connection.Output.WriteAsync(response);
                    buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                }
            } while (position != null);
        
            connection.Input.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCanceled || 
                result.IsCompleted)
            {
                break;
            }
        }
        
        await connection.Output.CompleteAsync();
    }

    private byte[] ProcessRequest(ReadOnlySequence<byte> line)
    {
        BigInteger n = 0;
        bool isValidRequest = line.IsSingleSegment ? 
            TryParseRequest(line.FirstSpan, out n) : 
            TryParseRequest(line.ToArray(), out n);

        if (isValidRequest)
        {
            return MillerRabin.IsPrime(n) ? 
                ResponseTrue : 
                ResponseFalse;
        }
        return ResponseMalformed;
    }
    
    private bool TryParseRequest(ReadOnlySpan<byte> request, out BigInteger n)
    {
        try
        {
            var node = JsonNode.Parse(request);
            if (node == null)
            {
                n = default;
                return false;
            }

            var obj = node.AsObject();
            if (!obj.TryGetPropertyValue("method", out var method) ||
                method == null || method.ToString() != "isPrime")
            {
                n = default;
                return false;
            }

            if (!obj.TryGetPropertyValue("number", out var number) ||
                number == null)
            {
                n = default;
                return false;
            }

            var val = number.GetValue<JsonElement>();
            if (val.ValueKind != JsonValueKind.Number)
            {
                n = default;
                return false;
            }

            BigInteger.TryParse(number.ToString(), out n);
            return true;
        }
        catch
        {
            n = default;
            return false;
        }
    }
}