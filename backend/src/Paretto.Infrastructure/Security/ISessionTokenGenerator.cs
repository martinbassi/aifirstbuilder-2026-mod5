namespace Paretto.Infrastructure.Security;

public interface ISessionTokenGenerator
{
    (string RawToken, string TokenHash) Generate();
}
