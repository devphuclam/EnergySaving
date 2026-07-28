using IUMP.Modules.Acquisition.Domain;

namespace IUMP.Tests.Unit.Acquisition;

public static class MeasurementIdentityTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        CheckCount = 0;
        var failures = new List<string>();
        var factory = new MeasurementIdentity();
        var source = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var run = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var point = Guid.Parse("11111111-2222-4333-8444-555555555555");
        var mapping = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
        var zero = factory.Create(source, run, point, mapping, 0, 1);
        var one = factory.Create(source, run, point, mapping, 1, 1);
        var fortyTwo = factory.Create(source, run, point, mapping, 42, 1);
        TestCount++;
        Check(zero.ToString("D") == "e118cea2-3d28-5dd4-9726-b3d7d4425ea4",
            "sequence 0 matches the literal UUIDv5 fixture", failures);
        TestCount++;
        Check(one.ToString("D") == "bf5a3f14-0774-5b13-88b1-fa782872b01c",
            "sequence 1 matches the literal UUIDv5 fixture", failures);
        TestCount++;
        Check(fortyTwo.ToString("D") == "442c323f-dddb-516b-96ff-88dab38133ce",
            "sequence 42 matches the literal UUIDv5 fixture", failures);
        TestCount++;
        Check(factory.Create(source, run, point, mapping, 0, 1) == zero,
            "same tuple and retry produce the same identity", failures);
        TestCount++;
        Check(factory.Create(source, Guid.NewGuid(), point, mapping, 0, 1) != zero,
            "different Run changes identity", failures);
        TestCount++;
        Check(factory.Create(source, run, Guid.NewGuid(), mapping, 0, 1) != zero,
            "different Point changes identity", failures);
        TestCount++;
        Check(factory.Create(source, run, point, Guid.NewGuid(), 0, 1) != zero,
            "different Mapping changes identity", failures);
        TestCount++;
        Check(one != zero && fortyTwo != zero,
            "different source sequence changes identity", failures);
        TestCount++;
        Check(factory.Create(source, run, point, mapping, 0, 2) != zero,
            "different algorithm version changes identity", failures);
        TestCount++;
        Check(factory.CreateCanonical(source, run, point, mapping, 0, 1) ==
              "e118cea2-3d28-5dd4-9726-b3d7d4425ea4",
            "canonical output is lowercase and dashed", failures);
        var network = zero.ToString("N");
        TestCount++;
        Check(network[12] == '5', "RFC 4122 version bits identify UUIDv5", failures);
        TestCount++;
        Check(network[16] is '8' or '9' or 'a' or 'b',
            "RFC 4122 variant bits are set", failures);
        return failures;
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        CheckCount++;
        if (!condition) failures.Add($"T109: {message}.");
    }
}
