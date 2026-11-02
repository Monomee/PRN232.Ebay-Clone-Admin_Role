using System.Data.Common;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EbayClone.Application.FunctionalTests;

using static Testing;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DbConnection _connection;
    private readonly string _connectionString;

    public CustomWebApplicationFactory(DbConnection connection, string connectionString)
    {
        _connection = connection;
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:MyCnn", _connectionString)
            .UseSetting("Jwt:Key", "functional-test-signing-key-32bytes!")
            .UseSetting("Security:EncryptionKey", Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray()))
            .UseSetting("Jwt:Issuer", "EbayCloneTest")
            .UseSetting("Jwt:Audience", "EbayCloneTest");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services
                .RemoveAll<IUser>()
                .AddTransient(provider =>
                {
                    var mock = new Mock<IUser>();
                    mock.SetupGet(x => x.Roles).Returns(GetRoles());
                    mock.SetupGet(x => x.Id).Returns(GetUserId());
                    return mock.Object;
                });
            services
                .RemoveAll<DbContextOptions<ApplicationDbContext>>()
                .AddDbContext<ApplicationDbContext>((sp, options) =>
                {
                    options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                    options.UseSqlServer(_connection);
                });
        });
    }
}
