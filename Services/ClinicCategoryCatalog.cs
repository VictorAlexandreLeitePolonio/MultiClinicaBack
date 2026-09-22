using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services;

public sealed record ClinicCategorySeed(
    string Name,
    string Slug,
    ClinicCategoryKind Kind,
    string? ParentSlug,
    string? ClinicalProfileKey,
    ClinicalModelStatus ClinicalModelStatus);

public static class ClinicCategoryCatalog
{
    // ponytail: lock process-local; the unique slug index remains the cross-instance guard.
    private static readonly SemaphoreSlim SeedLock = new(1, 1);

    public static IReadOnlyList<ClinicCategorySeed> Entries { get; } = BuildEntries();

    private static readonly IReadOnlyDictionary<string, string[]> Aliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["fonoaudiologia"] = ["fono", "fono-audio"],
        };

    public static async Task SeedAsync(AppDbContext db)
    {
        await SeedLock.WaitAsync();
        try
        {
            var categories = await db.ClinicCategories.ToListAsync();

            foreach (var entry in Entries)
            {
                var category = FindExisting(categories, entry);
                if (category is null)
                {
                    category = new ClinicCategory { Slug = entry.Slug };
                    db.ClinicCategories.Add(category);
                    categories.Add(category);
                }

                if (!category.IsActive || category.IsDeleted)
                    continue;

                category.Name = entry.Name;
                category.Kind = entry.Kind;
                category.ParentCategoryId = null;
                category.ClinicalProfileKey = entry.ClinicalProfileKey;
                category.ClinicalModelStatus = entry.ClinicalModelStatus;
            }

            await db.SaveChangesAsync();

            foreach (var entry in Entries)
            {
                var category = FindExisting(categories, entry);
                if (category is null || !category.IsActive || category.IsDeleted)
                    continue;

                category.ParentCategoryId = entry.ParentSlug is null
                    ? null
                    : categories.FirstOrDefault(parent =>
                        parent.Slug.Equals(entry.ParentSlug, StringComparison.OrdinalIgnoreCase)
                        && parent.IsActive && !parent.IsDeleted)?.Id;
            }

            await db.SaveChangesAsync();
        }
        finally
        {
            SeedLock.Release();
        }
    }

    private static ClinicCategory? FindExisting(
        IReadOnlyCollection<ClinicCategory> categories,
        ClinicCategorySeed entry)
    {
        var category = categories.FirstOrDefault(item =>
            item.Slug.Equals(entry.Slug, StringComparison.OrdinalIgnoreCase));
        if (category is not null)
            return category;

        return Aliases.TryGetValue(entry.Slug, out var aliases)
            ? categories.FirstOrDefault(item => aliases.Contains(item.Slug, StringComparer.OrdinalIgnoreCase))
            : null;
    }

    private static IReadOnlyList<ClinicCategorySeed> BuildEntries()
    {
        var areas = new[]
        {
            "Fisioterapia",
            "Fonoaudiologia",
            "Terapia Ocupacional",
            "Psicologia",
            "Nutrição",
            "Odontologia",
            "Medicina",
            "Enfermagem",
        }.Select(name => new ClinicCategorySeed(
            name,
            Slugify(name),
            ClinicCategoryKind.ProfessionalArea,
            null,
            name is "Fisioterapia" or "Fonoaudiologia" ? Slugify(name) : null,
            name is "Fisioterapia" or "Fonoaudiologia"
                ? ClinicalModelStatus.PendingReview
                : ClinicalModelStatus.NotConfigured));

        var specialties = new[]
        {
            "Acupuntura",
            "Alergia e imunologia",
            "Anestesiologia",
            "Angiologia",
            "Cardiologia",
            "Cirurgia cardiovascular",
            "Cirurgia da mão",
            "Cirurgia de cabeça e pescoço",
            "Cirurgia do aparelho digestivo",
            "Cirurgia geral",
            "Cirurgia oncológica",
            "Cirurgia pediátrica",
            "Cirurgia plástica",
            "Cirurgia torácica",
            "Cirurgia vascular",
            "Clínica médica",
            "Coloproctologia",
            "Dermatologia",
            "Endocrinologia e metabologia",
            "Endoscopia",
            "Gastroenterologia",
            "Genética médica",
            "Geriatria",
            "Ginecologia e obstetrícia",
            "Hematologia e hemoterapia",
            "Homeopatia",
            "Infectologia",
            "Mastologia",
            "Medicina de emergência",
            "Medicina de família e comunidade",
            "Medicina do trabalho",
            "Medicina do tráfego",
            "Medicina esportiva",
            "Medicina física e reabilitação",
            "Medicina intensiva",
            "Medicina legal e perícia médica",
            "Medicina nuclear",
            "Medicina preventiva e social",
            "Nefrologia",
            "Neurocirurgia",
            "Neurologia",
            "Nutrologia",
            "Oftalmologia",
            "Oncologia clínica",
            "Ortopedia e traumatologia",
            "Otorrinolaringologia",
            "Patologia",
            "Patologia clínica/medicina laboratorial",
            "Pediatria",
            "Pneumologia",
            "Psiquiatria",
            "Radiologia e diagnóstico por imagem",
            "Radioterapia",
            "Reumatologia",
            "Urologia",
        }.Select(name => new ClinicCategorySeed(
            name,
            name == "Ortopedia e traumatologia" ? "ortopedia" : Slugify(name),
            ClinicCategoryKind.MedicalSpecialty,
            "medicina",
            null,
            ClinicalModelStatus.NotConfigured));

        var legacy = new[]
        {
            "Clínica Geral",
            "Pilates",
            "Estética",
            "Outros",
        }.Select(name => new ClinicCategorySeed(
            name,
            Slugify(name),
            ClinicCategoryKind.Legacy,
            null,
            null,
            ClinicalModelStatus.NotConfigured));

        return areas.Concat(specialties).Concat(legacy).ToArray();
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var separatorPending = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                if (separatorPending && builder.Length > 0)
                    builder.Append('-');
                builder.Append(char.ToLowerInvariant(character));
                separatorPending = false;
            }
            else
            {
                separatorPending = builder.Length > 0;
            }
        }

        return builder.ToString();
    }
}
