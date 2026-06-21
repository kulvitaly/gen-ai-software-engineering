using System.ComponentModel.DataAnnotations;

namespace API.Tickets;

internal sealed class TicketCategoryAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is null || value is string text && EnumParser.TryParseCategory(text, out _);
    }
}

internal sealed class TicketPriorityAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is null || value is string text && EnumParser.TryParsePriority(text, out _);
    }
}

internal sealed class TicketStatusAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is null || value is string text && EnumParser.TryParseStatus(text, out _);
    }
}

internal sealed class TicketSourceAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is null || value is string text && text.ToSource().HasValue;
    }
}

internal sealed class DeviceTypeAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is null || value is string text && text.ToDeviceType().HasValue;
    }
}
