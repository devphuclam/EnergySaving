using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Acquisition.Domain;

namespace IUMP.Tests.Unit.Acquisition;

public static class DeterministicGeneratorVectorTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 10;
        CheckCount = 19;
        var failures = new List<string>();
        var generator = new DeterministicGenerator();
        var pointId = Guid.Parse("11111111-2222-4333-8444-555555555555");
        var configurationId = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
        const string initialHex = "032ba308f46f1f8e4f8167f77e7b0514000000000000000000";
        const string firstHex = "ed99faae39338fb74f8167f77e7b0514013f80c23bc5fbfb3f";
        const string restartHex = "ed99faae39338fb74f8167f77e7b0514000000000000000000";

        var initial = generator.Initialize(42, pointId, configurationId, 7, 1);
        Check(initial.Length == 25, "serialized state is exactly 25 bytes", failures);
        Check(Convert.ToHexString(initial).ToLowerInvariant() == initialHex,
            "literal initial state matches the normative vector", failures);

        var constant = generator.Generate(initial, SimulatorScenario.Constant, 12.5, 12.5);
        Check(constant.Value == 12.5 && constant.DrawCount == 0,
            "Constant returns 12.5000 without a PRNG draw", failures);
        Check(Convert.ToHexString(constant.State).ToLowerInvariant() == initialHex,
            "Constant leaves the serialized state unchanged", failures);

        var first = generator.Generate(initial, SimulatorScenario.Normal, 10, 20);
        Check(first.Value == 11.6519 && first.DrawCount == 2,
            "Normal first output is literal 11.6519 with two draws", failures);
        Check(Convert.ToHexString(first.State).ToLowerInvariant() == firstHex,
            "Normal first state matches the literal cached-spare vector", failures);
        var decodedFirst = DeterministicGenerator.Deserialize(first.State);
        Check(decodedFirst.SpareValid, "Normal first state has a valid cached spare", failures);

        var restartedGenerator = new DeterministicGenerator();
        var restart = restartedGenerator.Generate(Convert.FromHexString(firstHex),
            SimulatorScenario.Normal, 10, 20);
        Check(restart.Value == 17.9149 && restart.DrawCount == 0,
            "Normal restart returns literal 17.9149 with zero new draws", failures);
        Check(Convert.ToHexString(restart.State).ToLowerInvariant() == restartHex,
            "Normal restart consumes the spare and writes canonical positive zero", failures);
        var decodedRestart = DeterministicGenerator.Deserialize(restart.State);
        Check(!decodedRestart.SpareValid &&
              BitConverter.DoubleToInt64Bits(decodedRestart.SpareValue) == 0,
            "invalid cached spare is canonical positive zero", failures);

        CheckThrows(() => DeterministicGenerator.Deserialize(new byte[24]),
            "malformed state length is rejected", failures);
        var invalidFlag = initial.ToArray();
        invalidFlag[16] = 2;
        CheckThrows(() => DeterministicGenerator.Deserialize(invalidFlag),
            "invalid spare flag is rejected", failures);
        var invalidIncrement = initial.ToArray();
        invalidIncrement[8] ^= 1;
        CheckThrows(() => DeterministicGenerator.Deserialize(invalidIncrement),
            "unknown increment is rejected", failures);
        CheckThrows(() => generator.Initialize(42, pointId, configurationId, 7, 2),
            "unknown algorithm version is rejected", failures);
        CheckThrows(() => generator.Initialize(
                "IUMP-DETERMINISTIC-V2", 42, pointId, configurationId, 7, 1),
            "unknown algorithm ID is rejected", failures);
        CheckThrows(() => generator.Generate(initial, SimulatorScenario.Normal, 20, 10),
            "invalid Normal bounds are rejected", failures);
        Check(DeterministicGenerator.Deserialize(initial).Increment ==
              0x14057B7EF767814FUL,
            "serialized increment uses the normative little-endian value", failures);

        var tiesToEvenState = DeterministicGenerator.Serialize(
            new DeterministicGeneratorState(
                0x0102030405060708UL, DeterministicGenerator.Increment, true, 0.00015));
        var tiesToEven = generator.Generate(
            tiesToEvenState, SimulatorScenario.Normal, 10, 20);
        Check(tiesToEven.Value == 15.0002 && tiesToEven.DrawCount == 0,
            "literal cached spare rounds the midpoint tie to the even fourth decimal", failures);

        var clampState = DeterministicGenerator.Serialize(
            new DeterministicGeneratorState(
                0x0102030405060708UL, DeterministicGenerator.Increment, true, -3.00001));
        var roundedThenClamped = generator.Generate(
            clampState, SimulatorScenario.Normal, 10.00004, 20.00004);
        Check(roundedThenClamped.Value == 10.00004 &&
              roundedThenClamped.DrawCount == 0,
            "literal below-bound spare is rounded first and then clamped", failures);
        return failures;
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        if (!condition) failures.Add($"T108: {message}.");
    }

    private static void CheckThrows(Action action, string message, List<string> failures)
    {
        try
        {
            action();
            failures.Add($"T108: {message}.");
        }
        catch (ArgumentException)
        {
        }
    }
}
