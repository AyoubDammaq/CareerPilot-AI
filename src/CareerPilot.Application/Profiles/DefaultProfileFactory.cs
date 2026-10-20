using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Profiles;

/// <summary>
/// Initial profile, containing ONLY facts the owner stated when commissioning the project.
/// Nothing is inferred: unknown values stay empty, skill levels are unrated, and every fact
/// starts unverified until the owner confirms it in the UI. All of it is editable.
/// </summary>
public static class DefaultProfileFactory
{
    public static UserProfile Create()
    {
        var profile = new UserProfile
        {
            Headline = "Software Engineer (.NET, backend and AI applications)",
            Country = "Tunisia",
        };

        profile.WorkExperiences.Add(new WorkExperience
        {
            Position = 0,
            Company = "Sagemcom Tunisie",
            Title = "IT Development Engineer",
            Location = "Tunisia",
            // The owner said "since October 2025"; the exact day is unknown and must be confirmed.
            StartDate = new DateOnly(2025, 10, 1),
            IsVerified = false,
        });

        profile.Education.Add(new Education
        {
            Position = 0,
            Institution = "ENIT",
            Degree = "Engineering degree",
            Field = "Computer Science",
        });

        var skills = new (string Name, string Category)[]
        {
            ("C#", "Backend"), (".NET 9", "Backend"), ("ASP.NET Core", "Backend"), ("ASP.NET MVC", "Backend"),
            ("REST APIs", "Backend"), ("Clean Architecture", "Backend"), ("Microservices", "Backend"),
            ("SQL Server", "Databases"), ("Oracle", "Databases"), ("PostgreSQL", "Databases"),
            ("Software testing", "Backend"),
            ("Angular", "Frontend"), ("React", "Frontend"), ("TypeScript", "Frontend"),
            ("Docker", "DevOps"), ("Kubernetes", "DevOps"), ("Prometheus", "DevOps"), ("Grafana", "DevOps"),
            ("Python", "AI and LLM"), ("Machine learning (fundamentals)", "AI and LLM"),
            ("Deep learning (fundamentals)", "AI and LLM"), ("Semantic Kernel", "AI and LLM"),
            ("Ollama", "AI and LLM"), ("LLM API integrations", "AI and LLM"), ("Function calling", "AI and LLM"),
        };
        for (var i = 0; i < skills.Length; i++)
        {
            profile.Skills.Add(new Skill
            {
                Position = i,
                Name = skills[i].Name,
                Category = skills[i].Category,
                Level = null,
                IsVerified = false,
            });
        }

        profile.Projects.Add(new Project
        {
            Position = 0,
            Name = "Enterprise AI Assistant",
            Description = "Enterprise AI assistant built around .NET, Semantic Kernel, LLM integrations and function calling.",
            Technologies = [".NET", "Semantic Kernel", "LLM integrations", "Function calling"],
            Results = [],
            IsVerified = false,
        });

        profile.Languages.Add(new ProfileLanguage { Position = 0, Name = "French", Level = "B2" });
        profile.Languages.Add(new ProfileLanguage { Position = 1, Name = "English", Level = "B2" });
        profile.Languages.Add(new ProfileLanguage { Position = 2, Name = "German", Level = "Beginner" });

        profile.Preferences = new JobPreference
        {
            // "Golf countries" in the brief is read as the Gulf states (GCC); the owner can edit the list.
            TargetCountries =
            [
                "Tunisia", "France", "Belgium",
                "Saudi Arabia", "United Arab Emirates", "Qatar", "Kuwait", "Bahrain", "Oman",
            ],
            IncludeInternationalRemote = true,
            WorkModes = [nameof(WorkMode.Remote), nameof(WorkMode.Hybrid), nameof(WorkMode.OnSite)],
            EmploymentTypes = [nameof(EmploymentType.Permanent)],
            TargetTitles =
            [
                ".NET / C# Software Engineer",
                "Backend Software Engineer",
                "Full Stack Developer (.NET + Angular/React)",
                "Software Engineer (AI/LLM applications)",
                "AI Application Engineer",
                "Backend Engineer (AI integrations, agents, RAG, enterprise AI)",
                "Software Engineer (microservices and enterprise systems)",
            ],
            WorkingLanguages = ["French", "English"],
            IncludeSeniorRoles = false,
            ExperienceLevel = "Early career",
            SalaryCurrency = "EUR",
        };

        return profile;
    }
}
