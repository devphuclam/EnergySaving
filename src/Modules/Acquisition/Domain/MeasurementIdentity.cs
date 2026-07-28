using System.Security.Cryptography;
using System.Text;
using IUMP.Modules.Acquisition.Contracts;

namespace IUMP.Modules.Acquisition.Domain;

public sealed class MeasurementIdentity : IMeasurementIdentityFactory
{
    public static readonly Guid NamespaceId = Guid.Parse("02e993bb-c767-5ff6-963f-530e1dfdff6b");

    public Guid Create(Guid sourceId, Guid runId, Guid pointId, Guid mappingId, long sourceSequence,
        int algorithmVersion)
    {
        if (sourceId == Guid.Empty || runId == Guid.Empty || pointId == Guid.Empty || mappingId == Guid.Empty)
            throw new ArgumentException("Identity UUIDs are required.");
        if (sourceSequence < 0) throw new ArgumentOutOfRangeException(nameof(sourceSequence));
        if (algorithmVersion <= 0) throw new ArgumentOutOfRangeException(nameof(algorithmVersion));

        var name = $"IUMP:SIMULATOR:V1|{sourceId:D}|{runId:D}|{pointId:D}|{mappingId:D}|{sourceSequence}|{algorithmVersion}";
        var namespaceBytes = ToNetworkBytes(NamespaceId);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, namespaceBytes.Length);
        var hash = SHA1.HashData(input);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return FromNetworkBytes(hash.AsSpan(0, 16));
    }

    public string CreateCanonical(Guid sourceId, Guid runId, Guid pointId, Guid mappingId,
        long sourceSequence, int algorithmVersion) =>
        Create(sourceId, runId, pointId, mappingId, sourceSequence, algorithmVersion)
            .ToString("D").ToLowerInvariant();

    private static byte[] ToNetworkBytes(Guid value)
    {
        var bytes = value.ToByteArray();
        SwapGuidByteOrder(bytes);
        return bytes;
    }

    private static Guid FromNetworkBytes(ReadOnlySpan<byte> value)
    {
        var bytes = value.ToArray();
        SwapGuidByteOrder(bytes);
        return new Guid(bytes);
    }

    private static void SwapGuidByteOrder(byte[] bytes)
    {
        (bytes[0], bytes[3]) = (bytes[3], bytes[0]);
        (bytes[1], bytes[2]) = (bytes[2], bytes[1]);
        (bytes[4], bytes[5]) = (bytes[5], bytes[4]);
        (bytes[6], bytes[7]) = (bytes[7], bytes[6]);
    }
}
