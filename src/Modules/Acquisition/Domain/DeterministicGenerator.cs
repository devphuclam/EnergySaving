using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using IUMP.Modules.Acquisition.Contracts;

namespace IUMP.Modules.Acquisition.Domain;

public readonly record struct DeterministicGeneratorState(
    ulong State,
    ulong Increment,
    bool SpareValid,
    double SpareValue);

public sealed class DeterministicGenerator : ISimulatorValueGenerator
{
    public const ulong StateMultiplier = 0x5851F42D4C957F2DUL;
    public const ulong Increment = 0x14057B7EF767814FUL;
    public const ulong OutputMultiplier = 0xAEF17502108EF2D9UL;
    public const ulong FnvOffset = 0xCBF29CE484222325UL;
    public const ulong FnvPrime = 0x00000100000001B3UL;
    public const int SerializedStateLength = 25;
    private const string AlgorithmId = "IUMP-DETERMINISTIC-V1";
    private static readonly double Pi = BitConverter.Int64BitsToDouble(0x400921fb54442d18L);

    public byte[] Initialize(ulong seed, Guid pointId, Guid configurationId, long configurationVersion,
        int algorithmVersion) =>
        Initialize(AlgorithmId, seed, pointId, configurationId, configurationVersion, algorithmVersion);

    public byte[] Initialize(string algorithmId, ulong seed, Guid pointId, Guid configurationId,
        long configurationVersion, int algorithmVersion)
    {
        if (!string.Equals(algorithmId, AlgorithmId, StringComparison.Ordinal))
            throw new ArgumentException("Unsupported algorithm ID.", nameof(algorithmId));
        if (pointId == Guid.Empty) throw new ArgumentException("PointId is required.", nameof(pointId));
        if (configurationId == Guid.Empty) throw new ArgumentException("ConfigurationId is required.", nameof(configurationId));
        if (configurationVersion < 0) throw new ArgumentOutOfRangeException(nameof(configurationVersion));
        if (algorithmVersion != 1) throw new ArgumentOutOfRangeException(nameof(algorithmVersion));

        var material = string.Create(CultureInfo.InvariantCulture,
            $"{AlgorithmId}|seed={seed:x16}|point_id={pointId:D}|configuration_id={configurationId:D}|configuration_version={configurationVersion}|algorithm_version={algorithmVersion}");
        var hash = FnvOffset;
        foreach (var value in Encoding.UTF8.GetBytes(material))
        {
            hash = unchecked((hash ^ value) * FnvPrime);
        }

        ulong state = 0;
        state = Step(state);
        state = unchecked(state + hash);
        state = Step(state);
        return Serialize(new DeterministicGeneratorState(state, Increment, false, 0d));
    }

    public DeterministicGeneration Generate(byte[] state, SimulatorScenario scenario, double minimumValue,
        double maximumValue)
    {
        if (!double.IsFinite(minimumValue) || !double.IsFinite(maximumValue))
            throw new ArgumentException("Bounds must be finite.");
        if (scenario == SimulatorScenario.Constant && minimumValue != maximumValue)
            throw new ArgumentException("Constant bounds must match.");
        if (scenario == SimulatorScenario.Normal && minimumValue >= maximumValue)
            throw new ArgumentException("Normal minimum must be less than maximum.");

        var current = Deserialize(state);
        if (scenario == SimulatorScenario.Constant)
            return new DeterministicGeneration(minimumValue, Serialize(current), 0);
        if (scenario != SimulatorScenario.Normal)
            throw new ArgumentOutOfRangeException(nameof(scenario));

        double z;
        var draws = 0;
        if (current.SpareValid)
        {
            z = current.SpareValue;
            current = current with { SpareValid = false, SpareValue = 0d };
        }
        else
        {
            var first = Draw(current.State);
            var second = Draw(first.NextState);
            draws = 2;
            var u1 = (first.Value + 1.0d) / 4294967296.0d;
            var u2 = (second.Value + 1.0d) / 4294967296.0d;
            var radius = Math.Sqrt(-2.0d * Math.Log(u1));
            var angle = 2.0d * Pi * u2;
            var z0 = radius * Math.Cos(angle);
            var z1 = radius * Math.Sin(angle);
            z = z0;
            current = new DeterministicGeneratorState(second.NextState, current.Increment, true, z1);
        }

        var midpoint = (maximumValue + minimumValue) / 2.0d;
        var sigma = (maximumValue - minimumValue) / 6.0d;
        var raw = midpoint + z * sigma;
        var rounded = Math.Round(raw, 4, MidpointRounding.ToEven);
        var value = rounded < minimumValue ? minimumValue :
            rounded > maximumValue ? maximumValue : rounded;
        return new DeterministicGeneration(value, Serialize(current), draws);
    }

    public static byte[] Serialize(DeterministicGeneratorState state)
    {
        if (state.Increment != Increment) throw new ArgumentException("Unsupported increment.", nameof(state));
        if (!state.SpareValid && BitConverter.DoubleToInt64Bits(state.SpareValue) != 0L)
            throw new ArgumentException("Invalid spare must be canonical positive zero.", nameof(state));
        if (!double.IsFinite(state.SpareValue)) throw new ArgumentException("Spare must be finite.", nameof(state));

        var bytes = new byte[SerializedStateLength];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0, 8), state.State);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8, 8), state.Increment);
        bytes[16] = state.SpareValid ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(17, 8),
            BitConverter.DoubleToInt64Bits(state.SpareValue));
        return bytes;
    }

    public static DeterministicGeneratorState Deserialize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != SerializedStateLength)
            throw new ArgumentException("Serialized state must be exactly 25 bytes.", nameof(bytes));
        if (bytes[16] is not (0 or 1))
            throw new ArgumentException("Spare flag must be 0 or 1.", nameof(bytes));
        var increment = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(8, 8));
        if (increment != Increment)
            throw new ArgumentException("Unsupported increment.", nameof(bytes));
        var spare = BitConverter.Int64BitsToDouble(
            BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(17, 8)));
        if (!double.IsFinite(spare)) throw new ArgumentException("Spare must be finite.", nameof(bytes));
        if (bytes[16] == 0 && BitConverter.DoubleToInt64Bits(spare) != 0L)
            throw new ArgumentException("Invalid spare must be canonical positive zero.", nameof(bytes));
        return new DeterministicGeneratorState(
            BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(0, 8)),
            increment,
            bytes[16] == 1,
            spare);
    }

    private static ulong Step(ulong state) =>
        unchecked(state * StateMultiplier + Increment);

    private static (uint Value, ulong NextState) Draw(ulong state)
    {
        var next = Step(state);
        var shift = (uint)(state >> 59) + 5U;
        var word = unchecked(((state >> (int)shift) ^ state) * OutputMultiplier);
        var permuted = (word >> 43) ^ word;
        return (unchecked((uint)permuted), next);
    }
}
