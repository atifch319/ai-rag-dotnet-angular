using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyAi.Application.Configuration;

namespace MyAi.Application.Tests;

public sealed class RagOptionsTests
{
    [Fact]
    public void Bind_ReadsConfiguredMinimumSimilarity()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rag:MinimumSimilarity"] = "0.25"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOptions<RagOptions>()
            .Bind(configuration.GetSection(RagOptions.SectionName))
            .Validate(
                options => options.MinimumSimilarity is >= 0d and <= 1d,
                "Rag:MinimumSimilarity must be between 0 and 1.")
            .ValidateOnStart();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RagOptions>>().Value;

        Assert.Equal(0.25, options.MinimumSimilarity);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void ValidateOnStart_RejectsOutOfRangeMinimumSimilarity(double invalid)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rag:MinimumSimilarity"] = invalid.ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOptions<RagOptions>()
            .Bind(configuration.GetSection(RagOptions.SectionName))
            .Validate(
                options => options.MinimumSimilarity is >= 0d and <= 1d,
                "Rag:MinimumSimilarity must be between 0 and 1.")
            .ValidateOnStart();

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<RagOptions>>().Value);
        Assert.Contains("MinimumSimilarity", exception.Message);
    }
}
