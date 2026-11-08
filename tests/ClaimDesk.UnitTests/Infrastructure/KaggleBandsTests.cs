using ClaimDesk.Domain.Claims;
using ClaimDesk.Infrastructure.Seeding;

namespace ClaimDesk.UnitTests.Infrastructure;

public class KaggleBandsTests
{
    [Theory]
    [InlineData("none", DaysPolicyAccident.None)]
    [InlineData("1 to 7", DaysPolicyAccident.OneToSeven)]
    [InlineData("8 to 15", DaysPolicyAccident.EightToFifteen)]
    [InlineData("15 to 30", DaysPolicyAccident.FifteenToThirty)]
    [InlineData("more than 30", DaysPolicyAccident.MoreThanThirty)]
    public void Maps_days_policy_accident(string csv, DaysPolicyAccident expected) =>
        Assert.Equal(expected, KaggleBands.DaysPolicyAccident(csv));

    [Theory]
    [InlineData("no change", AddressChange.NoChange)]
    [InlineData("under 6 months", AddressChange.UnderSixMonths)]
    [InlineData("1 year", AddressChange.OneYear)]
    [InlineData("2 to 3 years", AddressChange.TwoToThreeYears)]
    [InlineData("4 to 8 years", AddressChange.FourToEightYears)]
    public void Maps_address_change(string csv, AddressChange expected) =>
        Assert.Equal(expected, KaggleBands.AddressChange(csv));

    [Theory]
    [InlineData("none", PastClaims.None)]
    [InlineData("1", PastClaims.One)]
    [InlineData("2 to 4", PastClaims.TwoToFour)]
    [InlineData("more than 4", PastClaims.MoreThanFour)]
    public void Maps_past_claims(string csv, PastClaims expected) =>
        Assert.Equal(expected, KaggleBands.PastClaims(csv));

    [Fact]
    public void Maps_categoricals()
    {
        Assert.Equal(Fault.PolicyHolder, KaggleBands.Fault("Policy Holder"));
        Assert.Equal(Fault.ThirdParty, KaggleBands.Fault("Third Party"));
        Assert.Equal(BasePolicy.AllPerils, KaggleBands.BasePolicy("All Perils"));
        Assert.Equal(VehicleCategory.Sport, KaggleBands.Category("Sport"));
        Assert.Equal(AgentType.External, KaggleBands.AgentType("External"));
        Assert.True(KaggleBands.YesNo("Yes"));
        Assert.False(KaggleBands.YesNo("No"));
    }
}
