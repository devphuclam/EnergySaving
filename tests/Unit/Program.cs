using IUMP.BuildingBlocks.Correlation;

var failures = new List<string>();

var supplied = CorrelationId.Create("r0-correlation-123");
if (supplied.Value != "r0-correlation-123")
{
    failures.Add("A valid supplied correlation ID must be preserved.");
}

var blank = CorrelationId.Create("   ");
if (!Guid.TryParse(blank.Value, out _))
{
    failures.Add("A blank correlation ID must be replaced by a server-generated GUID.");
}

var unsafeValue = CorrelationId.Create("unsafe\r\nvalue");
if (!Guid.TryParse(unsafeValue.Value, out _))
{
    failures.Add("A correlation ID with control characters must be replaced.");
}

var tooLong = CorrelationId.Create(new string('a', 129));
if (!Guid.TryParse(tooLong.Value, out _))
{
    failures.Add("A correlation ID longer than 128 characters must be replaced.");
}

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine("PASS: correlation ID public interface");
return 0;
