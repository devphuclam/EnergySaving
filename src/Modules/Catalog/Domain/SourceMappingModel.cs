namespace IUMP.Modules.Catalog.Domain;

public readonly record struct DataSourceId(Guid Value)
{
    public static DataSourceId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public enum SourceType { Simulator }
public enum SourceStatus { Draft, Active, Suspended, Decommissioned }

public sealed class DataSource
{
    public DataSourceId Id { get; }
    public string Code { get; }
    public string Name { get; private set; }
    public SourceType SourceType { get; }
    public SourceStatus Status { get; private set; }
    public long Version { get; private set; }
    public Guid? SiteId { get; }

    public DataSource(
        DataSourceId id,
        string code,
        string name,
        SourceType sourceType,
        SourceStatus status,
        long version,
        Guid? siteId = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Data source ID is required.", nameof(id));
        if (!Enum.IsDefined(sourceType)) throw new ArgumentOutOfRangeException(nameof(sourceType));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        Id = id;
        Code = Metric.NormalizeCode(code, nameof(code), 50);
        Name = Metric.RequireText(name, nameof(name), 200);
        SourceType = sourceType;
        Status = status;
        Version = version;
        SiteId = siteId;
    }

    public bool TryTransitionTo(SourceStatus target)
    {
        var allowed = (Status, target) switch
        {
            (SourceStatus.Draft, SourceStatus.Active) => true,
            (SourceStatus.Draft, SourceStatus.Decommissioned) => true,
            (SourceStatus.Active, SourceStatus.Suspended) => true,
            (SourceStatus.Active, SourceStatus.Decommissioned) => true,
            (SourceStatus.Suspended, SourceStatus.Active) => true,
            (SourceStatus.Suspended, SourceStatus.Decommissioned) => true,
            _ => false
        };
        if (!allowed) return false;
        Status = target;
        Version++;
        return true;
    }

    public bool IsDecommissioned => Status == SourceStatus.Decommissioned;

    public bool TryUpdate(string name)
    {
        if (IsDecommissioned) return false;
        var normalized = Metric.RequireText(name, nameof(name), 200);
        if (normalized == Name) return false;
        Name = normalized;
        Version++;
        return true;
    }
}

public readonly record struct MappingId(Guid Value)
{
    public static MappingId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public enum MappingStatus { Draft, Active, Inactive, Superseded }

public sealed class SourcePointMapping
{
    public MappingId Id { get; }
    public DataSourceId DataSourceId { get; }
    public string PointId { get; }
    public MappingStatus Status { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public long Version { get; private set; }

    public SourcePointMapping(MappingId id, DataSourceId dataSourceId, string pointId,
        MappingStatus status, DateTime effectiveFrom, DateTime? effectiveTo, long version)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Mapping ID is required.", nameof(id));
        if (dataSourceId.Value == Guid.Empty) throw new ArgumentException("Data source ID is required.", nameof(dataSourceId));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        Id = id;
        DataSourceId = dataSourceId;
        PointId = Metric.RequireText(pointId, nameof(pointId), 200);
        Status = status;
        EffectiveFrom = NormalizeUtc(effectiveFrom);
        EffectiveTo = effectiveTo.HasValue ? NormalizeUtc(effectiveTo.Value) : null;
        if (EffectiveTo.HasValue && EffectiveTo.Value <= EffectiveFrom)
            throw new ArgumentException("EffectiveTo must be greater than EffectiveFrom.", nameof(effectiveTo));
        Version = version;
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public bool TryActivate() { if (Status != MappingStatus.Draft) return false; Status = MappingStatus.Active; Version++; return true; }
    public bool TryInactivate() { if (Status != MappingStatus.Active) return false; Status = MappingStatus.Inactive; Version++; return true; }
    public bool TrySupersede() { if (Status is not (MappingStatus.Active or MappingStatus.Inactive)) return false; Status = MappingStatus.Superseded; Version++; return true; }
    public bool IsSuperseded => Status == MappingStatus.Superseded;
    public bool IsActive => Status == MappingStatus.Active;

    public bool TryUpdatePeriod(DateTime effectiveFrom, DateTime? effectiveTo)
    {
        if (Status is MappingStatus.Active or MappingStatus.Superseded) return false;
        var from = NormalizeUtc(effectiveFrom);
        DateTime? to = effectiveTo.HasValue ? NormalizeUtc(effectiveTo.Value) : null;
        if (to.HasValue && to.Value <= from)
            throw new ArgumentException("EffectiveTo must be greater than EffectiveFrom.", nameof(effectiveTo));
        if (from == EffectiveFrom && to == EffectiveTo) return false;
        EffectiveFrom = from;
        EffectiveTo = to;
        Version++;
        return true;
    }

    public bool OverlapsWith(SourcePointMapping other)
    {
        if (!IsActive || !other.IsActive || other.PointId != PointId || other.Id == Id) return false;
        var aEnd = EffectiveTo ?? DateTime.MaxValue;
        var bEnd = other.EffectiveTo ?? DateTime.MaxValue;
        return EffectiveFrom < bEnd && other.EffectiveFrom < aEnd;
    }
}
