using Domain.Tickets;

namespace Application.Tickets;

public interface ITicketClassifier
{
    TicketClassification Classify(Ticket ticket);
}

public sealed class TicketClassifier : ITicketClassifier
{
    private static readonly IReadOnlyList<KeywordRule<TicketCategory>> CategoryRules =
    [
        new(TicketCategory.AccountAccess, ["login", "password", "2fa", "two-factor", "can't access", "cannot access"]),
        new(TicketCategory.BillingQuestion, ["payment", "payments", "invoice", "invoices", "refund", "billing"]),
        new(TicketCategory.FeatureRequest, ["enhancement", "feature", "suggestion", "request"]),
        new(TicketCategory.BugReport, ["reproduce", "reproduction", "steps to reproduce", "defect"]),
        new(TicketCategory.TechnicalIssue, ["bug", "error", "errors", "crash", "crashes", "exception"])
    ];

    private static readonly IReadOnlyList<KeywordRule<TicketPriority>> PriorityRules =
    [
        new(TicketPriority.Urgent, ["can't access", "cannot access", "critical", "production down", "security"]),
        new(TicketPriority.High, ["important", "blocking", "asap"]),
        new(TicketPriority.Low, ["minor", "cosmetic", "suggestion"])
    ];

    public TicketClassification Classify(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var text = $"{ticket.Subject} {ticket.Description}";
        var categoryMatch = Match(CategoryRules, text);
        var priorityMatch = Match(PriorityRules, text);
        var category = categoryMatch.Value ?? TicketCategory.Other;
        var priority = priorityMatch.Value ?? TicketPriority.Medium;
        var keywords = categoryMatch.Keywords
            .Concat(priorityMatch.Keywords)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var confidence = CalculateConfidence(categoryMatch.Keywords.Count, priorityMatch.Keywords.Count);
        var reasoning = BuildReasoning(category, priority, keywords);

        return new TicketClassification(category, priority, confidence, reasoning, keywords);
    }

    private static MatchResult<T> Match<T>(IEnumerable<KeywordRule<T>> rules, string text)
        where T : struct, Enum
    {
        foreach (var rule in rules)
        {
            var keywords = rule.Keywords
                .Where(keyword => ContainsKeyword(text, keyword))
                .ToArray();

            if (keywords.Length > 0)
            {
                return new MatchResult<T>(rule.Value, keywords);
            }
        }

        return new MatchResult<T>(null, []);
    }

    private static bool ContainsKeyword(string text, string keyword)
    {
        return text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static double CalculateConfidence(int categoryMatches, int priorityMatches)
    {
        var matchedGroups = (categoryMatches > 0 ? 1 : 0) + (priorityMatches > 0 ? 1 : 0);
        var keywordBonus = Math.Min(categoryMatches + priorityMatches, 4) * 0.05;
        return Math.Clamp(0.35 + matchedGroups * 0.2 + keywordBonus, 0, 1);
    }

    private static string BuildReasoning(TicketCategory category, TicketPriority priority, IReadOnlyCollection<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return "No classification keywords matched; defaulted to other category and medium priority.";
        }

        return $"Matched {keywords.Count} keyword(s), assigning {category} category and {priority} priority.";
    }

    private sealed record KeywordRule<T>(T Value, IReadOnlyList<string> Keywords)
        where T : struct, Enum;

    private sealed record MatchResult<T>(T? Value, IReadOnlyList<string> Keywords)
        where T : struct, Enum;
}
