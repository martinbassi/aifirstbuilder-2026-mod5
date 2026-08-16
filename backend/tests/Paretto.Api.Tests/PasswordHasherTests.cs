using Paretto.Infrastructure.Security;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 4 (Servicios de seguridad) — <see cref="IPasswordHasher"/>.
/// </summary>
public class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new PasswordHasher();

    [Fact]
    public void Hash_of_the_same_password_twice_produces_different_hashes_due_to_random_salt()
    {
        const string password = "Sup3rSecret!";

        var firstHash = _hasher.Hash(password);
        var secondHash = _hasher.Hash(password);

        Assert.NotEqual(firstHash, secondHash);
    }

    [Fact]
    public void Verify_accepts_the_correct_hash_and_rejects_an_incorrect_one()
    {
        const string password = "Sup3rSecret!";
        var hash = _hasher.Hash(password);

        Assert.True(_hasher.Verify(password, hash));
        Assert.False(_hasher.Verify("WrongPassword1", hash));
    }
}
