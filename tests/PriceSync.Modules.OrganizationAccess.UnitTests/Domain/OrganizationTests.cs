using FluentAssertions;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using Xunit;

namespace PriceSync.Modules.OrganizationAccess.UnitTests.Domain;

public sealed class OrganizationTests
{
    [Fact]
    public void Create_establishes_active_organization_with_identity_and_creation_timestamp()
    {
        var id = new OrganizationId(Guid.Parse("01994aca-a1c3-7d23-9803-2d3e8fb705a1"));
        var legalIdentity = LegalOrganizationIdentity.Create("SE", "5560160680");
        var createdAt = new DateTimeOffset(2026, 9, 10, 12, 30, 0, TimeSpan.Zero);
        var timeProvider = new FrozenTimeProvider(createdAt);

        var organization = Organization.Create(id, "  Acme AB  ", legalIdentity, timeProvider);

        organization.Id.Should().Be(id);
        organization.DisplayName.Should().Be("Acme AB");
        organization.LegalIdentity.Should().Be(legalIdentity);
        organization.Status.Should().Be(OrganizationStatus.Active);
        organization.CreatedAt.Should().Be(createdAt);
        organization.ArchivedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_rejects_missing_display_name(string? displayName)
    {
        var create = () => Organization.Create(NewOrganizationId(), displayName!, NewLegalIdentity(), NewFrozenTimeProvider());

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_accepts_display_names_up_to_two_hundred_characters_after_trimming()
    {
        var displayName = $" {new string('A', 200)} ";

        var organization = Organization.Create(NewOrganizationId(), displayName, NewLegalIdentity(), NewFrozenTimeProvider());

        organization.DisplayName.Should().Be(new string('A', 200));
    }

    [Fact]
    public void Create_rejects_display_names_longer_than_two_hundred_characters_after_trimming()
    {
        var create = () => Organization.Create(NewOrganizationId(), new string('A', 201), NewLegalIdentity(), NewFrozenTimeProvider());

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_null_time_provider()
    {
        var create = () => Organization.Create(NewOrganizationId(), "Acme AB", NewLegalIdentity(), null!);

        create.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_keeps_display_name_as_presentation_data_not_identity_data()
    {
        var first = Organization.Create(NewOrganizationId(), "Acme AB", NewLegalIdentity(), NewFrozenTimeProvider());
        var second = Organization.Create(NewOrganizationId(), "Different Trading Name", NewLegalIdentity(), NewFrozenTimeProvider());

        first.LegalIdentity.Should().Be(second.LegalIdentity);
        first.DisplayName.Should().NotBe(second.DisplayName);
    }

    [Fact]
    public void ChangeDisplayName_updates_presentation_name_without_changing_legal_identity()
    {
        var organization = NewOrganization();
        var legalIdentity = organization.LegalIdentity;

        organization.ChangeDisplayName("  Acme Trading AB  ");

        organization.DisplayName.Should().Be("Acme Trading AB");
        organization.LegalIdentity.Should().Be(legalIdentity);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ChangeDisplayName_rejects_missing_display_name(string? displayName)
    {
        var organization = NewOrganization();

        var change = () => organization.ChangeDisplayName(displayName!);

        change.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangeDisplayName_rejects_display_names_longer_than_two_hundred_characters_after_trimming()
    {
        var organization = NewOrganization();

        var change = () => organization.ChangeDisplayName(new string('A', 201));

        change.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Archive_moves_active_organization_to_archived_with_current_utc_timestamp()
    {
        var organization = NewOrganization();
        var archivedAt = new DateTimeOffset(2026, 9, 10, 14, 45, 0, TimeSpan.Zero);
        var timeProvider = new FrozenTimeProvider(archivedAt);

        organization.Archive(timeProvider);

        organization.Status.Should().Be(OrganizationStatus.Archived);
        organization.ArchivedAt.Should().Be(archivedAt);
    }

    [Fact]
    public void Archive_rejects_null_time_provider()
    {
        var organization = NewOrganization();

        var archive = () => organization.Archive(null!);

        archive.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Archive_is_terminal_for_organization()
    {
        var organization = NewOrganization();
        organization.Archive(new FrozenTimeProvider(new DateTimeOffset(2026, 9, 10, 14, 45, 0, TimeSpan.Zero)));

        var archiveAgain = () => organization.Archive(new FrozenTimeProvider(new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero)));

        archiveAgain.Should().Throw<InvalidOperationException>();
        organization.Status.Should().Be(OrganizationStatus.Archived);
        organization.ArchivedAt.Should().Be(new DateTimeOffset(2026, 9, 10, 14, 45, 0, TimeSpan.Zero));
    }

    private static OrganizationId NewOrganizationId() => new(Guid.NewGuid());

    private static LegalOrganizationIdentity NewLegalIdentity() => LegalOrganizationIdentity.Create("SE", "5560160680");

    private static DateTimeOffset UtcNow() => new(2026, 9, 10, 12, 30, 0, TimeSpan.Zero);

    private static FrozenTimeProvider NewFrozenTimeProvider() => new(UtcNow());

    private static Organization NewOrganization() => Organization.Create(NewOrganizationId(), "Acme AB", NewLegalIdentity(), NewFrozenTimeProvider());

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
