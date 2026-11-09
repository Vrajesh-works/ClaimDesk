using ClaimDesk.Domain.Claims;
using ClaimDesk.Domain.Users;

namespace ClaimDesk.UnitTests.Domain;

public class ClaimWorkflowTests
{
    [Fact]
    public void Each_action_and_source_state_pair_is_unique()
    {
        var pairs = ClaimWorkflow.Transitions.Select(t => (t.Action, t.From)).ToList();
        Assert.Equal(pairs.Count, pairs.Distinct().Count());
    }

    [Fact]
    public void Terminal_states_have_no_outgoing_transitions()
    {
        Assert.DoesNotContain(ClaimWorkflow.Transitions, t => t.From.IsTerminal());
    }

    [Fact]
    public void Every_non_initial_state_is_reachable()
    {
        var targets = ClaimWorkflow.Transitions.Select(t => t.To).ToHashSet();
        foreach (var status in Enum.GetValues<ClaimStatus>().Where(s => s != ClaimStatus.Submitted))
        {
            Assert.Contains(status, targets);
        }
    }

    [Fact]
    public void Available_actions_respect_role_and_ownership()
    {
        var owner = Guid.NewGuid();
        var claim = TestData.NewClaim(owner);

        Assert.Equal([ClaimAction.StartReview], ClaimWorkflow.AvailableActions(claim, new Actor(Guid.NewGuid(), Roles.Adjuster)));
        Assert.Empty(ClaimWorkflow.AvailableActions(claim, new Actor(owner, Roles.Claimant)));

        claim.Apply(ClaimAction.StartReview, new Actor(Guid.NewGuid(), Roles.Adjuster), null, TestData.Now);
        claim.Apply(ClaimAction.RequestInfo, new Actor(Guid.NewGuid(), Roles.Adjuster), "need more", TestData.Now);

        Assert.Equal([ClaimAction.ProvideInfo], ClaimWorkflow.AvailableActions(claim, new Actor(owner, Roles.Claimant)));
        Assert.Empty(ClaimWorkflow.AvailableActions(claim, new Actor(Guid.NewGuid(), Roles.Claimant)));
    }
}
