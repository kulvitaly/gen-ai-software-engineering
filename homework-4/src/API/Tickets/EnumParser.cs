using Domain.Tickets;

namespace API.Tickets;

internal static class EnumParser
{
    public static bool TryParseCategory(string? value, out TicketCategory category)
    {
        return TryParse(value, CategoryNames, out category);
    }

    public static bool TryParsePriority(string? value, out TicketPriority priority)
    {
        return TryParse(value, PriorityNames, out priority);
    }

    public static bool TryParseStatus(string? value, out TicketStatus status)
    {
        return TryParse(value, StatusNames, out status);
    }

    public static string ToApiValue(this TicketCategory category)
    {
        return CategoryNames[category];
    }

    public static string ToApiValue(this TicketPriority priority)
    {
        return PriorityNames[priority];
    }

    public static string ToApiValue(this TicketStatus status)
    {
        return StatusNames[status];
    }

    public static string ToApiValue(this TicketSource source)
    {
        return SourceNames[source];
    }

    public static string ToApiValue(this DeviceType deviceType)
    {
        return DeviceTypeNames[deviceType];
    }

    public static TicketCategory? ToCategory(this string? value)
    {
        return TryParseCategory(value, out var category) ? category : null;
    }

    public static TicketPriority? ToPriority(this string? value)
    {
        return TryParsePriority(value, out var priority) ? priority : null;
    }

    public static TicketStatus? ToStatus(this string? value)
    {
        return TryParseStatus(value, out var status) ? status : null;
    }

    public static TicketSource? ToSource(this string? value)
    {
        return TryParse(value, SourceNames, out TicketSource source) ? source : null;
    }

    public static DeviceType? ToDeviceType(this string? value)
    {
        return TryParse(value, DeviceTypeNames, out DeviceType deviceType) ? deviceType : null;
    }

    private static bool TryParse<TEnum>(string? value, IReadOnlyDictionary<TEnum, string> names, out TEnum parsed)
        where TEnum : struct, Enum
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var pair in names)
        {
            if (string.Equals(pair.Value, value.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(pair.Key.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                parsed = pair.Key;
                return true;
            }
        }

        return false;
    }

    private static readonly IReadOnlyDictionary<TicketCategory, string> CategoryNames = new Dictionary<TicketCategory, string>
    {
        [TicketCategory.AccountAccess] = "account_access",
        [TicketCategory.TechnicalIssue] = "technical_issue",
        [TicketCategory.BillingQuestion] = "billing_question",
        [TicketCategory.FeatureRequest] = "feature_request",
        [TicketCategory.BugReport] = "bug_report",
        [TicketCategory.Other] = "other"
    };

    private static readonly IReadOnlyDictionary<TicketPriority, string> PriorityNames = new Dictionary<TicketPriority, string>
    {
        [TicketPriority.Urgent] = "urgent",
        [TicketPriority.High] = "high",
        [TicketPriority.Medium] = "medium",
        [TicketPriority.Low] = "low"
    };

    private static readonly IReadOnlyDictionary<TicketStatus, string> StatusNames = new Dictionary<TicketStatus, string>
    {
        [TicketStatus.New] = "new",
        [TicketStatus.InProgress] = "in_progress",
        [TicketStatus.WaitingCustomer] = "waiting_customer",
        [TicketStatus.Resolved] = "resolved",
        [TicketStatus.Closed] = "closed"
    };

    private static readonly IReadOnlyDictionary<TicketSource, string> SourceNames = new Dictionary<TicketSource, string>
    {
        [TicketSource.WebForm] = "web_form",
        [TicketSource.Email] = "email",
        [TicketSource.Api] = "api",
        [TicketSource.Chat] = "chat",
        [TicketSource.Phone] = "phone"
    };

    private static readonly IReadOnlyDictionary<DeviceType, string> DeviceTypeNames = new Dictionary<DeviceType, string>
    {
        [DeviceType.Desktop] = "desktop",
        [DeviceType.Mobile] = "mobile",
        [DeviceType.Tablet] = "tablet"
    };
}
