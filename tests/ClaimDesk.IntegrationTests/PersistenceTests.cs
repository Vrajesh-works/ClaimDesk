using ClaimDesk.Application.Claims;
using ClaimDesk.Application.Sla;
using ClaimDesk.Domain.Claims;
using ClaimDesk.Domain.Users;
using ClaimDesk.Infrastructure.Persistence;
using ClaimDesk.Infrastructure.Seeding;
using ClaimDesk.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimDesk.IntegrationTests;

public class PersistenceTests(ClaimDeskApiFactory factory) : IClassFixture<ClaimDeskApiFactory>
{
    [Fact]
    public async Task Audit_events_cannot_be_modified_or_deleted()
    {
        var id = await TestClaims.ArrangeAsync(factory, ClaimStatus.InReview);

        await factory.WithDbAsync<bool>(async (db, _) =>
        {
            var row = await db.ClaimEvents.FirstAsync(e => e.ClaimId == id);
            db.Entry(row).Property(e => e.Note).CurrentValue = "rewritten history";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            return true;
        });

        await factory.WithDbAsync<bool>(async (db, _) =>
        {
            db.ClaimEvents.Remove(await db.ClaimEvents.FirstAsync(e => e.ClaimId == id));
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            return true;
        });
    }

    [Fact]
    public async Task Concurrent_edits_to_the_same_claim_are_detected()
    {
        var id = await TestClaims.ArrangeAsync(factory, ClaimStatus.InReview);
        var adjuster = new Actor(DemoUsers.Adjuster.Id, Roles.Adjuster);

        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();
        var serviceA = scopeA.ServiceProvider.GetRequiredService<ClaimService>();
        var serviceB = scopeB.ServiceProvider.GetRequiredService<ClaimService>();
        var claimA = await serviceA.GetAsync(id, CancellationToken.None);
        var claimB = await serviceB.GetAsync(id, CancellationToken.None);

        await serviceA.TransitionAsync(claimA, ClaimAction.Approve, adjuster, new TransitionRequest(), CancellationToken.None);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            serviceB.TransitionAsync(claimB, ClaimAction.Deny, adjuster, new TransitionRequest { Note = "late" }, CancellationToken.None));
    }

    [Fact]
    public async Task Sla_scan_raises_reminders_and_stuck_alerts_once_per_window()
    {
        // Isolated factory: this test moves the clock days forward.
        await using var isolated = new ClaimDeskApiFactory();
        var submitted = await TestClaims.ArrangeAsync(isolated, ClaimStatus.Submitted);
        var inReview = await TestClaims.ArrangeAsync(isolated, ClaimStatus.InReview);
        var paid = await TestClaims.ArrangeAsync(isolated, ClaimStatus.Paid);

        async Task<SlaScanResult> Scan() => await isolated.WithDbAsync((_, services) =>
            services.GetRequiredService<SlaScanner>().ScanAsync(CancellationToken.None));

        Assert.Equal(new SlaScanResult(0, 0), await Scan());

        isolated.Time.Advance(TimeSpan.FromHours(25));
        Assert.Equal(new SlaScanResult(1, 0), await Scan());
        Assert.Equal(new SlaScanResult(0, 0), await Scan());

        isolated.Time.Advance(TimeSpan.FromDays(5));
        var result = await Scan();
        Assert.Equal(1, result.Reminders);
        Assert.Equal(2, result.StuckAlerts);

        await isolated.WithDbAsync<bool>(async (db, _) =>
        {
            var events = await db.ClaimEvents.Where(e => e.Type == ClaimEventType.SlaReminder || e.Type == ClaimEventType.StuckAlert).ToListAsync();
            Assert.Equal(2, events.Count(e => e.ClaimId == submitted && e.Type == ClaimEventType.SlaReminder));
            Assert.Single(events, e => e.ClaimId == submitted && e.Type == ClaimEventType.StuckAlert);
            Assert.Single(events, e => e.ClaimId == inReview);
            Assert.DoesNotContain(events, e => e.ClaimId == paid);
            Assert.Equal(4, await db.Notifications.CountAsync());
            return true;
        });
    }
}
