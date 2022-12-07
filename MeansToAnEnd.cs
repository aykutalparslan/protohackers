using System.Buffers;
using System.Buffers.Binary;
using protohackers.Transport;

namespace protohackers;

public class MeansToAnEnd : TcpServerBase
{
    protected override async Task ProcessConnection(Connection connection)
    {
        SortedSet<TimestampedPrice> prices = new SortedSet<TimestampedPrice>(new ByTimestamp());
        var responseBuffer = new byte[4];
        bool completed = false;
        while (!completed)
        {
            var result = await connection.Input.ReadAsync();
            var buffer = result.Buffer;

            do
            {
                if (buffer.Length >= 9)
                {
                    Console.WriteLine("Processing request...");
                    try
                    {
                        var response = ProcessRequest(buffer.Slice(0, 9), prices);
                        if (response != null)
                        {
                            Console.WriteLine("Sending response...");
                            BinaryPrimitives.WriteInt32BigEndian(responseBuffer, response.Value);
                            await connection.Output.WriteAsync(responseBuffer);
                        }
                    }
                    catch (InvalidOperationException e)
                    {
                        completed = true;
                        break;
                    }
                    
                    buffer = buffer.Slice(buffer.GetPosition(9, buffer.Start));
                }
            } while (buffer.Length >= 9);
            connection.Input.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCanceled ||
                result.IsCompleted)
            {
                break;
            }
        }

        await connection.Output.CompleteAsync();
    }

    private int? ProcessRequest(ReadOnlySequence<byte> request, SortedSet<TimestampedPrice> prices)
    {
        SequenceReader<byte> reader = new SequenceReader<byte>(request);
        reader.TryRead(out var q);
        Span<byte> numBytes = stackalloc byte[4];
        if (q == 'I')
        {
            reader.TryCopyTo(numBytes);
            int timestamp = BinaryPrimitives.ReadInt32BigEndian(numBytes);
            reader.Advance(4);
            reader.TryCopyTo(numBytes);
            int price = BinaryPrimitives.ReadInt32BigEndian(numBytes);
            prices.Add(new TimestampedPrice(timestamp, price));
            Console.WriteLine($"Adding price {timestamp} - {price}");
        }
        else if (q == 'Q')
        {
            reader.TryCopyTo(numBytes);
            int low = BinaryPrimitives.ReadInt32BigEndian(numBytes);
            reader.Advance(4);
            reader.TryCopyTo(numBytes);
            int high = BinaryPrimitives.ReadInt32BigEndian(numBytes);
            Console.WriteLine($"Querying prices {low} - {high}");
            var filtered = prices.GetViewBetween(
                new TimestampedPrice(low, 0),
                new TimestampedPrice(high, 0));
            if (filtered == null)
            {
                return 0;
            }
            int sum = 0;
            foreach (var p in filtered)
            {
                sum += p.Price;
            }

            int median = sum / filtered.Count;
            Console.WriteLine($"Median is {median}");
            return median;
        }
        else
        {
            throw new InvalidOperationException();
        }

        return null;
    }
    private readonly record struct TimestampedPrice(int Timestamp, int Price);
    private class ByTimestamp : IComparer<TimestampedPrice>
    {
        public int Compare(TimestampedPrice x, TimestampedPrice y)
        {
            return x.Timestamp.CompareTo(y.Timestamp);
        }
    }
}