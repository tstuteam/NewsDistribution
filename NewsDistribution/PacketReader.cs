using System.Text;

namespace NewsDistribution.Shared;


/// <summary>
///     Packet reader implementation.
/// </summary>
public class PacketReader : BinaryReader
{
    /// <summary>
    ///     Initializes a new packet reader instance.
    /// </summary>
    /// <param name="stream">Stream to read from.</param>
    public PacketReader(Stream stream)
        : base(stream)
    {
    }


    /// <summary>
    ///     Reads a string with the specified maximum length.
    ///     Throws <see cref="InvalidDataException"/> if the string is greater than <paramref name="maxLength"/>.
    /// </summary>
    /// <param name="maxLength">Maximum string length.</param>
    /// <returns>The string.</returns>
    /// <exception cref="InvalidDataException">The string is longer than <paramref name="maxLength"/></exception>
    public string ReadString(int maxLength)
    {
        var length = Read7BitEncodedInt();

        if (length > maxLength)
            throw new InvalidDataException("Received string is too large.");

        if (length > 1024)
        {
            byte[] buffer = new byte[length];
            Read(buffer);

            return Encoding.UTF8.GetString(buffer);
        }
        else
        {
            Span<byte> buffer = stackalloc byte[length];
            Read(buffer);

            return Encoding.UTF8.GetString(buffer);
        }
    }
}
