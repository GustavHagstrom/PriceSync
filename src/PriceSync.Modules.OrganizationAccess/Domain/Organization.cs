using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.OrganizationAccess.Domain;

internal sealed class Organization
{
    private const int MaximumDisplayNameLength = 200;

    private Organization(
        OrganizationId id,
        string displayName,
        LegalOrganizationIdentity legalIdentity,
        DateTimeOffset createdAt)
    {
        Id = id;
        DisplayName = displayName;
        LegalIdentity = legalIdentity;
        CreatedAt = createdAt;
        Status = OrganizationStatus.Active;
    }

    private Organization()
    {
        DisplayName = null!;
        LegalIdentity = null!;
    }

    public OrganizationId Id { get; private set; }

    public string DisplayName { get; private set; }

    public LegalOrganizationIdentity LegalIdentity { get; private set; }

    public OrganizationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public static Organization Create(
        OrganizationId id,
        string displayName,
        LegalOrganizationIdentity legalIdentity,
        TimeProvider timeProvider)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(legalIdentity);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new Organization(id, NormalizeDisplayName(displayName), legalIdentity, timeProvider.GetUtcNow());
    }

    public void ChangeDisplayName(string displayName)
    {
        DisplayName = NormalizeDisplayName(displayName);
    }

    public void Archive(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status == OrganizationStatus.Archived)
        {
            throw new InvalidOperationException("Archived Organization cannot be archived again.");
        }

        Status = OrganizationStatus.Archived;
        ArchivedAt = timeProvider.GetUtcNow();
    }

    private static string NormalizeDisplayName(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);

        var normalizedDisplayName = displayName.Trim();
        if (normalizedDisplayName.Length == 0)
        {
            throw new ArgumentException("Organization display name is required.", nameof(displayName));
        }

        if (normalizedDisplayName.Length > MaximumDisplayNameLength)
        {
            throw new ArgumentException("Organization display name cannot exceed 200 characters.", nameof(displayName));
        }

        return normalizedDisplayName;
    }
}
