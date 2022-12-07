using System.Numerics;

namespace protohackers;

public class MillerRabin
{
    /// <summary>
    /// Implements the Miller-Rabin primality test algorithm from
    /// https://en.wikipedia.org/wiki/Miller-Rabin_primality_test
    /// </summary>
    /// <param name="n"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    public static bool IsPrime(long n)
    {
        if (n % 2 == 0 || n < 2)
        {
            return n == 2;
        }
        long d = n - 1;
        int s = 0;
        while(d % 2 == 0)
        {
            d /= 2;
            s++;
        }

        var rounds = GetRounds(n);
        foreach (var a in rounds)
        {
            long x = ModPow(a, d, n);

            if (x == 1 || x == n - 1)
            {
                continue;
            }
            for (int i2 = 0; i2 < s; i2++)
            {
                x = ((x * x) % n);
                if (x == n - 1)
                {
                    break;
                }
            }
            if (x != n - 1)
            {
                return false;
            }
        }
        return true;
    }
    
    /// <summary>
    /// Implements the Miller-Rabin primality test algorithm from
    /// https://en.wikipedia.org/wiki/Miller-Rabin_primality_test
    /// </summary>
    /// <param name="n"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    public static bool IsPrime(BigInteger n)
    {
        if (n % 2 == 0 || n < 2)
        {
            return n == 2;
        }
        BigInteger d = n - 1;
        int s = 0;
        while(d % 2 == 0)
        {
            d /= 2;
            s++;
        }

        var rounds = GetRounds(n);
        foreach (var a in rounds)
        {
            BigInteger x = BigInteger.ModPow(a, d, n);

            if (x == 1 || x == n - 1)
            {
                continue;
            }
            for (int i2 = 0; i2 < s; i2++)
            {
                x = ((x * x) % n);
                if (x == n - 1)
                {
                    break;
                }
            }
            if (x != n - 1)
            {
                return false;
            }
        }
        return true;
    }

    private static long[] GetRounds(long n) => n switch
    {
        < 2047 => new long[] { 2 },
        < 1373653 => new long[] { 2, 3 },
        < 9080191 => new long[] { 31, 73 },
        < 25326001 => new long[] { 2, 3, 5 },
        < 3215031751 => new long[] { 2, 3, 5, 7 },
        < 4759123141 => new long[] { 2, 7, 61 },
        < 1122004669633 => new long[] { 2, 13, 23, 1662803 },
        < 2152302898747 => new long[] { 2, 3, 5, 7, 11 },
        < 3474749660383 => new long[] { 2, 3, 5, 7, 11, 13 },
        < 341550071728321 => new long[] { 2, 3, 5, 7, 11, 13, 17 },
        < 3825123056546413051 => new long[] { 2, 3, 5, 7, 11, 13, 17, 19 },
        _ => new long[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 }
    };

    private static BigInteger[] GetRounds(BigInteger n)
    {
        if (n <  BigInteger.Parse("318665857834031151167461"))
        {
            return new BigInteger[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 };
        }
        if (n <  BigInteger.Parse("3317044064679887385961981"))
        {
            return new BigInteger[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41 };
        }

        var rounds = new BigInteger[20];
        for (int i = 0; i < 20; i++)
        {
            rounds[i] = RandomInteger.Next(2, n - 2);
        }
        return rounds;
    }

    // https://stackoverflow.com/a/5434148/2015348
    // https://gist.github.com/bbarry/1068d17b49b0ff98bca5194d275896ed
    private static long ModPow(long value, long exponent, long modulus)
    {
        long result = 1;
        while (exponent > 0)
        {
            if ((exponent & 1) == 1) result = result * value % modulus;
            value = value * value % modulus;
            exponent >>= 1;
        }

        return (uint)result;
    }
}