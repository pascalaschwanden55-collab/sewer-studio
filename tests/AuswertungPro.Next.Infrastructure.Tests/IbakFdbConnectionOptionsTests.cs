using AuswertungPro.Next.Infrastructure.Import.Ibak;
using FirebirdSql.Data.FirebirdClient;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class IbakFdbConnectionOptionsTests
{
    [Fact]
    public void LoadCredentials_uses_firebird_defaults_without_env()
    {
        using var scope = new IbakFdbEnvScope(user: null, password: null);

        var credentials = IbakFdbConnectionOptions.LoadCredentials();

        Assert.Equal("SYSDBA", credentials.User);
        Assert.Equal("masterkey", credentials.Password);
    }

    [Fact]
    public void LoadCredentials_trims_user_but_keeps_password_literal()
    {
        using var scope = new IbakFdbEnvScope("  custom_user  ", "  secret  ");

        var credentials = IbakFdbConnectionOptions.LoadCredentials();

        Assert.Equal("custom_user", credentials.User);
        Assert.Equal("  secret  ", credentials.Password);
    }

    [Fact]
    public void CreateEmbedded_preserves_existing_topology_connection_defaults()
    {
        using var scope = new IbakFdbEnvScope(user: null, password: null);

        var builder = IbakFdbConnectionOptions.CreateEmbedded(@"C:\tmp\Arizona.fdb");

        Assert.Equal(@"C:\tmp\Arizona.fdb", builder.Database);
        Assert.Equal("SYSDBA", builder.UserID);
        Assert.Equal("masterkey", builder.Password);
        Assert.Equal(FbServerType.Embedded, builder.ServerType);
        Assert.Equal("NONE", builder.Charset);
    }

    [Fact]
    public void CreatePhotoMap_preserves_existing_photo_connection_defaults()
    {
        using var scope = new IbakFdbEnvScope("operator", "pw");

        var builder = IbakFdbConnectionOptions.CreatePhotoMap(@"C:\tmp\Arizona.fdb", @"C:\tmp\fbclient.dll");

        Assert.Equal(@"C:\tmp\Arizona.fdb", builder.Database);
        Assert.Equal("operator", builder.UserID);
        Assert.Equal("pw", builder.Password);
        Assert.Equal("WIN1252", builder.Charset);
        Assert.Equal(3, builder.Dialect);
        Assert.False(builder.Pooling);
        Assert.Equal(@"C:\tmp\fbclient.dll", builder.ClientLibrary);
    }

    private sealed class IbakFdbEnvScope : IDisposable
    {
        private readonly string? _previousUser;
        private readonly string? _previousPassword;

        public IbakFdbEnvScope(string? user, string? password)
        {
            _previousUser = Environment.GetEnvironmentVariable(IbakFdbConnectionOptions.UserEnvVar);
            _previousPassword = Environment.GetEnvironmentVariable(IbakFdbConnectionOptions.PasswordEnvVar);
            Environment.SetEnvironmentVariable(IbakFdbConnectionOptions.UserEnvVar, user);
            Environment.SetEnvironmentVariable(IbakFdbConnectionOptions.PasswordEnvVar, password);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(IbakFdbConnectionOptions.UserEnvVar, _previousUser);
            Environment.SetEnvironmentVariable(IbakFdbConnectionOptions.PasswordEnvVar, _previousPassword);
        }
    }
}
