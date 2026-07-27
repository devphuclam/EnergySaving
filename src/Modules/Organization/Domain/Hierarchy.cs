namespace IUMP.Modules.Organization.Domain;

public readonly record struct SiteId(Guid Value)
{
    public static SiteId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public enum SiteStatus { Draft, Active, Inactive }

public sealed class Site
{
    public SiteId Id { get; }
    public string Code { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string Timezone { get; private set; }
    public SiteStatus Status { get; private set; }
    public long Version { get; private set; }

    public Site(SiteId id, string code, string name, string? description, string timezone, SiteStatus status, long version)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Site ID is required.", nameof(id));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        Id = id;
        Code = NormalizeCode(code);
        Name = RequireText(name, 200);
        Description = description?.Trim();
        Timezone = RequireText(timezone, 100);
        Status = status;
        Version = version;
    }

    public bool TryActivate()
    {
        if (Status != SiteStatus.Draft) return false;
        Status = SiteStatus.Active;
        Version++;
        return true;
    }

    public bool TryInactivate()
    {
        if (Status != SiteStatus.Active) return false;
        Status = SiteStatus.Inactive;
        Version++;
        return true;
    }

    public bool TryReactivate()
    {
        if (Status != SiteStatus.Inactive) return false;
        Status = SiteStatus.Active;
        Version++;
        return true;
    }

    public bool IsActive => Status == SiteStatus.Active;

    public bool TryUpdate(string name, string? description, string timezone)
    {
        var normalizedName = RequireText(name, 200);
        var normalizedTimezone = RequireText(timezone, 100);
        var normalizedDescription = description?.Trim();
        if (Name == normalizedName && Description == normalizedDescription && Timezone == normalizedTimezone)
            return false;
        Name = normalizedName;
        Description = normalizedDescription;
        Timezone = normalizedTimezone;
        Version++;
        return true;
    }

    public static string NormalizeCode(string value) => RequireText(value, 50).ToUpperInvariant();
    public static string RequireText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.");
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw new ArgumentOutOfRangeException(nameof(value));
        return trimmed;
    }
}

public readonly record struct AreaId(Guid Value)
{
    public static AreaId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public enum AreaStatus { Draft, Active, Inactive }

public sealed class Area
{
    public AreaId Id { get; }
    public SiteId SiteId { get; }
    public string Code { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public AreaStatus Status { get; private set; }
    public long Version { get; private set; }

    public Area(AreaId id, SiteId siteId, string code, string name, string? description, AreaStatus status, long version)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Area ID is required.", nameof(id));
        if (siteId.Value == Guid.Empty) throw new ArgumentException("Site ID is required.", nameof(siteId));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        Id = id;
        SiteId = siteId;
        Code = Site.NormalizeCode(code);
        Name = Site.RequireText(name, 200);
        Description = description?.Trim();
        Status = status;
        Version = version;
    }

    public bool TryActivate()
    {
        if (Status != AreaStatus.Draft) return false;
        Status = AreaStatus.Active;
        Version++;
        return true;
    }

    public bool TryInactivate()
    {
        if (Status != AreaStatus.Active) return false;
        Status = AreaStatus.Inactive;
        Version++;
        return true;
    }

    public bool TryReactivate()
    {
        if (Status != AreaStatus.Inactive) return false;
        Status = AreaStatus.Active;
        Version++;
        return true;
    }

    public bool IsActive => Status == AreaStatus.Active;

    public bool TryUpdate(string name, string? description)
    {
        var normalizedName = Site.RequireText(name, 200);
        var normalizedDescription = description?.Trim();
        if (Name == normalizedName && Description == normalizedDescription) return false;
        Name = normalizedName;
        Description = normalizedDescription;
        Version++;
        return true;
    }
}

public readonly record struct AssetId(Guid Value)
{
    public static AssetId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public enum AssetStatus { Draft, Active, Inactive, Decommissioned }

public sealed class Asset
{
    public AssetId Id { get; }
    public SiteId SiteId { get; }
    public AreaId AreaId { get; }
    public string Code { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public AssetStatus Status { get; private set; }
    public long Version { get; private set; }

    public Asset(AssetId id, SiteId siteId, AreaId areaId, string code, string name, string? description, AssetStatus status, long version)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Asset ID is required.", nameof(id));
        if (siteId.Value == Guid.Empty) throw new ArgumentException("Site ID is required.", nameof(siteId));
        if (areaId.Value == Guid.Empty) throw new ArgumentException("Area ID is required.", nameof(areaId));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        Id = id;
        SiteId = siteId;
        AreaId = areaId;
        Code = Site.NormalizeCode(code);
        Name = Site.RequireText(name, 200);
        Description = description?.Trim();
        Status = status;
        Version = version;
    }

    public bool TryActivate()
    {
        if (Status != AssetStatus.Draft) return false;
        Status = AssetStatus.Active;
        Version++;
        return true;
    }

    public bool TryInactivate()
    {
        if (Status != AssetStatus.Active) return false;
        Status = AssetStatus.Inactive;
        Version++;
        return true;
    }

    public bool TryReactivate()
    {
        if (Status != AssetStatus.Inactive) return false;
        Status = AssetStatus.Active;
        Version++;
        return true;
    }

    public bool TryDecommission()
    {
        if (Status == AssetStatus.Decommissioned) return false;
        if (Status != AssetStatus.Active && Status != AssetStatus.Inactive) return false;
        Status = AssetStatus.Decommissioned;
        Version++;
        return true;
    }

    public bool IsActive => Status == AssetStatus.Active;
    public bool IsDecommissioned => Status == AssetStatus.Decommissioned;

    public bool TryUpdate(string name, string? description)
    {
        var normalizedName = Site.RequireText(name, 200);
        var normalizedDescription = description?.Trim();
        if (Name == normalizedName && Description == normalizedDescription) return false;
        Name = normalizedName;
        Description = normalizedDescription;
        Version++;
        return true;
    }
}

public readonly record struct PointId(Guid Value)
{
    public static PointId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public enum PointStatus { Draft, Active, Inactive, Decommissioned }

public sealed class MeasurementPoint
{
    public PointId Id { get; }
    public SiteId SiteId { get; }
    public AreaId AreaId { get; }
    public AssetId AssetId { get; }
    public string Code { get; }
    public string? Description { get; private set; }
    public string MetricId { get; private set; }
    public string UnitId { get; private set; }
    public string DataOwnerUserId { get; private set; }
    public int ExpectedIntervalSeconds { get; private set; }
    public int NoDataAfterSeconds { get; private set; }
    public PointStatus Status { get; private set; }
    public long Version { get; private set; }

    public MeasurementPoint(PointId id, SiteId siteId, AreaId areaId, AssetId assetId,
        string code, string? description, string metricId, string unitId, string dataOwnerUserId,
        int expectedIntervalSeconds, int noDataAfterSeconds, PointStatus status, long version)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Point ID is required.", nameof(id));
        if (siteId.Value == Guid.Empty) throw new ArgumentException("Site ID is required.", nameof(siteId));
        if (areaId.Value == Guid.Empty) throw new ArgumentException("Area ID is required.", nameof(areaId));
        if (assetId.Value == Guid.Empty) throw new ArgumentException("Asset ID is required.", nameof(assetId));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        if (expectedIntervalSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(expectedIntervalSeconds));
        if (noDataAfterSeconds <= expectedIntervalSeconds) throw new ArgumentOutOfRangeException(nameof(noDataAfterSeconds));
        Id = id;
        SiteId = siteId;
        AreaId = areaId;
        AssetId = assetId;
        Code = Site.NormalizeCode(code);
        Description = description?.Trim();
        MetricId = Site.RequireText(metricId, 200);
        UnitId = Site.RequireText(unitId, 200);
        DataOwnerUserId = Site.RequireText(dataOwnerUserId, 200);
        ExpectedIntervalSeconds = expectedIntervalSeconds;
        NoDataAfterSeconds = noDataAfterSeconds;
        Status = status;
        Version = version;
    }

    public bool TryActivate()
    {
        if (Status != PointStatus.Draft) return false;
        Status = PointStatus.Active;
        Version++;
        return true;
    }

    public bool TryInactivate()
    {
        if (Status != PointStatus.Active) return false;
        Status = PointStatus.Inactive;
        Version++;
        return true;
    }

    public bool TryReactivate()
    {
        if (Status != PointStatus.Inactive) return false;
        Status = PointStatus.Active;
        Version++;
        return true;
    }

    public bool TryDecommission()
    {
        if (Status == PointStatus.Decommissioned) return false;
        if (Status != PointStatus.Active && Status != PointStatus.Inactive) return false;
        Status = PointStatus.Decommissioned;
        Version++;
        return true;
    }

    public bool IsActive => Status == PointStatus.Active;
    public bool IsDecommissioned => Status == PointStatus.Decommissioned;

    public bool TryUpdateConfiguration(string? description, string metricId, string unitId, string dataOwnerUserId,
        int expectedIntervalSeconds, int noDataAfterSeconds)
    {
        var normalizedMetric = Site.RequireText(metricId, 200);
        var normalizedUnit = Site.RequireText(unitId, 200);
        var normalizedOwner = Site.RequireText(dataOwnerUserId, 200);
        if (expectedIntervalSeconds <= 0 || noDataAfterSeconds <= expectedIntervalSeconds)
            throw new ArgumentOutOfRangeException(nameof(expectedIntervalSeconds));
        var normalizedDescription = description?.Trim();
        if (Description == normalizedDescription && MetricId == normalizedMetric && UnitId == normalizedUnit &&
            DataOwnerUserId == normalizedOwner && ExpectedIntervalSeconds == expectedIntervalSeconds &&
            NoDataAfterSeconds == noDataAfterSeconds) return false;
        Description = normalizedDescription;
        MetricId = normalizedMetric;
        UnitId = normalizedUnit;
        DataOwnerUserId = normalizedOwner;
        ExpectedIntervalSeconds = expectedIntervalSeconds;
        NoDataAfterSeconds = noDataAfterSeconds;
        Version++;
        return true;
    }
}

public sealed record PointLifecycleEntry(
    string HistoryId,
    string PointId,
    long PointVersion,
    PointStatus OldStatus,
    PointStatus NewStatus,
    string ActorId,
    string? ActorUsername,
    string? Reason,
    DateTime OccurredAt,
    string? CorrelationId,
    string? CausationId);

public sealed record PointActivationPrerequisites(
    bool ParentAssetActive,
    bool MetricActive,
    bool UnitActive,
    bool UnitCompatible,
    bool IntervalValid,
    bool DataOwnerActive,
    bool HasExactlyOneActiveMapping);
