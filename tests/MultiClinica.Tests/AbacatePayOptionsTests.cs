using Microsoft.Extensions.Configuration;
using MultiClinica.API.Services.Billing;
using Xunit;

namespace MultiClinica.Tests;

public class AbacatePayOptionsTests
{
    // Mesmo formato que as variáveis de ambiente AbacatePay__Products__<chave> produzem.
    private static AbacatePayOptions Bind(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build()
            .GetSection("AbacatePay").Get<AbacatePayOptions>()!;

    [Fact]
    public void Produtos_vindos_da_configuracao_sao_encontrados()
    {
        var options = Bind(new()
        {
            ["AbacatePay:Products:Pro_Monthly"] = "prod_pro_m",
            ["AbacatePay:Products:extra-professional"] = "prod_extra",
            ["AbacatePay:Products:pack_pro-conversations-200"] = "prod_pack",
        });

        Assert.Equal("prod_pro_m", options.ProductFor("Pro_Monthly"));
        Assert.Equal("prod_extra", options.ProductFor("extra-professional"));
        Assert.Equal("prod_pack", options.ProductFor("pack:pro-conversations-200"));
        Assert.Null(options.ProductFor("Premium_Annual"));
    }
}
