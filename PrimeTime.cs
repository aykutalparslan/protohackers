using System.Buffers;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using protohackers.Transport;

namespace protohackers;

public class PrimeTime : TcpServerBase
{
    private ReadOnlySpan<byte> ResponseTrue => """{"method":"isPrime","prime":true}"""u8;
    private ReadOnlySpan<byte> ResponseFalse => """{"method":"isPrime","prime":false}"""u8;
    private ReadOnlySpan<byte> ResponseMalformed => """{"method":"isPrime"}"""u8;
    protected override async Task ProcessConnection(Connection connection)
    {
        connection.Start();
        while (true)
        {
            var result = await connection.Input.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;
            SequencePosition? position = null;
            do
            {
                position = buffer.PositionOf((byte)'\n');
                if (position != null)
                {
                    Console.WriteLine("Processing line...");
                    connection.Output.Write(ProcessRequest(buffer.Slice(0, position.Value)));
                    await connection.Output.FlushAsync();
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

    private ReadOnlySpan<byte> ProcessRequest(ReadOnlySequence<byte> line)
    {
        BigInteger n = 0;
        bool isValidRequest = line.IsSingleSegment ? 
            TryParseRequest(line.FirstSpan, out n) : 
            TryParseRequest(line.ToArray(), out n);

        if (isValidRequest)
        {
            Console.WriteLine($"Request is valid with n: {n}");
            bool isPrime = MillerRabin.IsPrime(n);
            Console.WriteLine($"Primality: {isPrime}");
            return isPrime ? 
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