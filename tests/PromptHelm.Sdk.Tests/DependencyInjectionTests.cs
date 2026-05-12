using System;
using Microsoft.Extensions.DependencyInjection;
using PromptHelm.Sdk.DependencyInjection;

namespace PromptHelm.Sdk.Tests;

public class DependencyInjectionTests
{
    private const string ValidKey = "phk_0123456789abcdef0123456789abcdef";

    [Fact]
    public void AddPromptHelm_RegistersInterface()
    {
        var services = new ServiceCollection();
        services.AddPromptHelm(opts =>
        {
            opts.ApiKey = ValidKey;
            opts.UserAgent = "test/1";
        });

        ServiceProvider sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IPromptHelmClient>();

        Assert.NotNull(client);
        Assert.IsType<PromptHelmClient>(client);
    }

    [Fact]
    public void AddPromptHelm_InvalidConfig_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        services.AddPromptHelm(opts => opts.ApiKey = "bad-key");
        ServiceProvider sp = services.BuildServiceProvider();

        Assert.Throws<ArgumentException>(() => sp.GetRequiredService<IPromptHelmClient>());
    }

    [Fact]
    public void AddPromptHelm_ConfigureNull_Throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddPromptHelm(null!));
    }

    [Fact]
    public void AddPromptHelm_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddPromptHelm(_ => { }));
    }
}
