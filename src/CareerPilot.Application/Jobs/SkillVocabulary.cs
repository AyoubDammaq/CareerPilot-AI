using System.Text.RegularExpressions;

namespace CareerPilot.Application.Jobs;

/// <summary>
/// Finds known technologies mentioned in a job text. The vocabulary is reference data and is merged
/// with the owner's own profile skills so anything they list is recognised too.
/// </summary>
public static class SkillVocabulary
{
    public static readonly string[] Default =
    [
        "C#", ".NET", "ASP.NET", "ASP.NET Core", "Entity Framework", "Blazor", "LINQ", "WPF",
        "Java", "Spring Boot", "Kotlin", "Scala", "Golang", "Rust", "Python", "Django", "Flask", "FastAPI",
        "Ruby", "Rails", "PHP", "Laravel", "Symfony", "Node.js", "NestJS", "TypeScript", "JavaScript",
        "Angular", "React", "Vue", "Next.js", "HTML", "CSS", "GraphQL", "gRPC", "REST", "Microservices",
        "SQL", "SQL Server", "PostgreSQL", "MySQL", "Oracle", "MongoDB", "Redis", "Elasticsearch", "Kafka",
        "RabbitMQ", "Docker", "Kubernetes", "Helm", "Terraform", "Ansible", "AWS", "Azure", "GCP",
        "CI/CD", "Jenkins", "GitHub Actions", "GitLab CI", "Prometheus", "Grafana", "OpenTelemetry",
        "Linux", "Git", "Agile", "Scrum", "TDD", "Clean Architecture", "DDD",
        "Machine Learning", "Deep Learning", "PyTorch", "TensorFlow", "scikit-learn", "NLP", "LLM",
        "RAG", "Semantic Kernel", "LangChain", "OpenAI", "Ollama", "Hugging Face", "Vector database",
        "Function calling", "MLOps", "Data engineering", "Spark", "Airflow", "dbt",
    ];

    public static List<string> Extract(string text, IEnumerable<string>? extra = null)
    {
        var found = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return found;

        foreach (var term in Default.Concat(extra ?? []).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // Boundaries: not preceded/followed by a letter, digit or '#', '+' continuing a longer token.
            var pattern = $@"(?<![\p{{L}}\p{{N}}#+.]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}#+])";
            // Short names (REST, Git, React, Redis...) are matched case-sensitively so ordinary words
            // such as "rest" or "react" in prose do not count as skills.
            var caseSensitive = term.Length < 6;
            var options = RegexOptions.CultureInvariant | (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
            if (Regex.IsMatch(text, pattern, options))
                found.Add(term);
        }
        return found;
    }
}
