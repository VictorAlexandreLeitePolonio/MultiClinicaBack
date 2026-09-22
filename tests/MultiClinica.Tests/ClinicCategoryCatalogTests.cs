using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services;
using Xunit;

namespace MultiClinica.Tests;

public class ClinicCategoryCatalogTests
{
    [Fact]
    public async Task SeedAsync_IsIdempotentAndKeepsTheCompleteCatalog()
    {
        await using var app = new MultiClinicaFactory();

        await app.SeedAsync(ClinicCategoryCatalog.SeedAsync);
        await app.SeedAsync(ClinicCategoryCatalog.SeedAsync);

        await app.SeedAsync(async db =>
        {
            var categories = await db.ClinicCategories
                .Where(category => !category.IsDeleted)
                .ToListAsync();

            Assert.Equal(67, categories.Count);
            Assert.Equal(67, categories.Select(category => category.Slug).Distinct().Count());

            var orthopedics = Assert.Single(categories, category => category.Slug == "ortopedia");
            Assert.Equal("Ortopedia e traumatologia", orthopedics.Name);
            Assert.Equal(ClinicCategoryKind.MedicalSpecialty, orthopedics.Kind);
            Assert.Contains(categories, category => category.Slug == "fonoaudiologia");
            Assert.Contains(categories, category => category.Slug == "medicina");
        });
    }
}
