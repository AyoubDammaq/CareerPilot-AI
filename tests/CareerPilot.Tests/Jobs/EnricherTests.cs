using CareerPilot.Application.Jobs;
using CareerPilot.Domain.Jobs;

namespace CareerPilot.Tests.Jobs;

public class JobEnricherTests
{
    [Theory]
    [InlineData("Software Engineer (m/f/d)", "software engineer")]
    [InlineData("Développeur Sénior", "developpeur senior")]
    [InlineData("  Back-End   Engineer ", "back end engineer")]
    public void Normalize_strips_gender_tags_accents_and_punctuation(string input, string expected) =>
        Assert.Equal(expected, JobEnricher.Normalize(input));

    [Fact]
    public void The_same_vacancy_gets_the_same_key_regardless_of_gender_tag_and_case()
    {
        var place = CountryCatalog.Resolve("Paris, France", null);
        Assert.Equal(
            JobEnricher.CanonicalKey("Acme", "Backend Engineer (m/f/d)", place),
            JobEnricher.CanonicalKey("ACME", "backend engineer", place));
    }

    [Fact]
    public void Different_places_give_different_keys()
    {
        var a = JobEnricher.CanonicalKey("Acme", "Backend Engineer", CountryCatalog.Resolve("Paris, France", null));
        var b = JobEnricher.CanonicalKey("Acme", "Backend Engineer", CountryCatalog.Resolve("Brussels, Belgium", null));
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData("Senior Backend Engineer", SeniorityLevel.Senior)]
    [InlineData("Staff Software Engineer", SeniorityLevel.Senior)]
    [InlineData("Engineering Lead", SeniorityLevel.Lead)]
    [InlineData("Head of Engineering", SeniorityLevel.Lead)]
    [InlineData("Senior Engineering Manager", SeniorityLevel.Lead)]
    [InlineData("Junior Developer", SeniorityLevel.Junior)]
    [InlineData("Software Engineer", SeniorityLevel.Unspecified)]
    public void Seniority_is_detected_from_the_title(string title, SeniorityLevel expected) =>
        Assert.Equal(expected, JobEnricher.DetectSeniority(title, new DiscoveryOptions()));

    [Theory]
    [InlineData("We expect 3+ years of experience with C#.", 3)]
    [InlineData("5 years of experience in Java and 2 years experience with Go", 2)]
    [InlineData("Experience: 3-5 years in backend development", 3)]
    [InlineData("Founded 10 years ago, we are growing.", null)]
    [InlineData("", null)]
    public void Experience_years_are_a_hint_only_when_tied_to_experience(string text, int? expected) =>
        Assert.Equal(expected, JobEnricher.ExperienceYears(text));

    [Theory]
    [InlineData("Nous recherchons un développeur pour rejoindre notre équipe et travailler avec des experts dans une ambiance sympa.", "fr")]
    [InlineData("We are looking for an engineer to join our team and work with the best people in the industry.", "en")]
    [InlineData("", null)]
    public void Language_is_detected(string text, string? expected) =>
        Assert.Equal(expected, JobEnricher.DetectLanguage(text));

    [Fact]
    public void Sponsorship_and_relocation_mentions_are_detected_from_the_text()
    {
        var job = Helpers.Job("1", description: "We offer visa sponsorship and relocation support for new joiners.");
        var e = JobEnricher.Enrich(job, new DiscoveryOptions(), null);
        Assert.True(e.MentionsSponsorship);
        Assert.True(e.MentionsRelocation);

        var plain = JobEnricher.Enrich(Helpers.Job("2", description: "Build services."), new DiscoveryOptions(), null);
        Assert.False(plain.MentionsSponsorship);
        Assert.False(plain.MentionsRelocation);
    }
}

public class SkillVocabularyTests
{
    [Fact]
    public void Known_technologies_are_found()
    {
        var skills = SkillVocabulary.Extract("We use C#, .NET 9, ASP.NET Core and PostgreSQL on Kubernetes.");
        Assert.Contains("C#", skills);
        Assert.Contains(".NET", skills);
        Assert.Contains("ASP.NET", skills);
        Assert.Contains("PostgreSQL", skills);
        Assert.Contains("Kubernetes", skills);
    }

    [Fact]
    public void Ordinary_words_are_not_mistaken_for_technologies()
    {
        Assert.Empty(SkillVocabulary.Extract("Take the rest of the day to react and go home."));
    }

    [Fact]
    public void The_owners_own_skills_extend_the_vocabulary()
    {
        Assert.Contains("Foobar", SkillVocabulary.Extract("Experience with foobar is a plus.", ["Foobar"]));
    }
}
