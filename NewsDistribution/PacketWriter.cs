namespace NewsDistribution.Shared;


/// <summary>
///     Packet writer implementation.
/// </summary>
public class PacketWriter : BinaryWriter
{
    /// <summary>
    ///     Initializes a new packet writer instance.
    /// </summary>
    public PacketWriter()
        : base(new MemoryStream())
    {
    }


    /// <summary>
    ///     Resets the internal buffer and starts a new packet.
    /// </summary>
    /// <param name="packetType">Packet type.</param>
    public void Start(PacketType packetType)
    {
        BaseStream.Position = 0;
        BaseStream.SetLength(0);

        Write((byte)packetType);
        Seek(sizeof(int), SeekOrigin.Current);
    }


    /// <summary>
    ///     Returns the packet data and resets the internal buffer.
    /// </summary>
    /// <returns></returns>
    public byte[] End()
    {
        var size = BaseStream.Length;
        var dataSize = size - sizeof(PacketType) - sizeof(int);

        BaseStream.Position = sizeof(PacketType);

        Write((int)dataSize);

        var buffer = new byte[size];
        BaseStream.Position = 0;
        BaseStream.Read(buffer, 0, (int)size);

        return buffer;
    }
}
