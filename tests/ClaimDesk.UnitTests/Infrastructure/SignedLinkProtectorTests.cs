using ClaimDesk.Infrastructure.Storage;
using Microsoft.Extensions.Time.Testing;

namespace ClaimDesk.UnitTests.Infrastructure;

public class SignedLinkProtectorTests
{
    private const string Key = "unit-test-signing-key-0123456789abcdef";
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Round_trips_payload_before_expiry()
    {
        var protector = new SignedLinkProtector(Key, _time);
        var token = protector.Protect("claim/file.pdf", "file.pdf", "application/pdf", TimeSpan.FromMinutes(5));

        _time.Advance(TimeSpan.FromMinutes(4));
        var payload = protector.Unprotect(token);

        Assert.NotNull(payload);
        Assert.Equal("claim/file.pdf", payload.Key);
        Assert.Equal("application/pdf", payload.ContentType);
    }

    [Fact]
    public void Rejects_expired_token()
    {
        var protector = new SignedLinkProtector(Key, _time);
        var token = protector.Protect("k", "f.pdf", "application/pdf", TimeSpan.FromMinutes(5));

        _time.Advance(TimeSpan.FromMinutes(6));

        Assert.Null(protector.Unprotect(token));
    }

    [Fact]
    public void Rejects_token_signed_with_another_key()
    {
        var token = new SignedLinkProtector(Key, _time).Protect("k", "f.pdf", "application/pdf", TimeSpan.FromMinutes(5));
        var other = new SignedLinkProtector("another-signing-key-0123456789abcdef", _time);

        Assert.Null(other.Unprotect(token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")]
    public void Rejects_malformed_tokens(string? token)
    {
        Assert.Null(new SignedLinkProtector(Key, _time).Unprotect(token));
    }

    [Fact]
    public void Rejects_short_signing_key()
    {
        Assert.Throws<ArgumentException>(() => new SignedLinkProtector("short", _time));
    }
}
