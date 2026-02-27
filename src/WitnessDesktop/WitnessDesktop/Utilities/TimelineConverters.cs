using System.Globalization;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;

namespace WitnessDesktop.Utilities;

public class RoleToUserVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is MessageRole role && role == MessageRole.User;
    
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class RoleToAssistantVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is MessageRole role && (role == MessageRole.Assistant || role == MessageRole.Proactive);
    
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class SignalToUpperCaseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString()?.ToUpperInvariant() ?? "";
    
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null;
    
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class EventTypeToTemplateConverter : IValueConverter
{
    public DataTemplate? DirectMessageTemplate { get; set; }
    public DataTemplate? ProactiveAlertTemplate { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimelineEvent evt)
        {
            return evt.Type switch
            {
                EventOutputType.DirectMessage => DirectMessageTemplate,
                EventOutputType.Danger or EventOutputType.Opportunity when evt.Role == MessageRole.Proactive
                    => ProactiveAlertTemplate,
                _ => DefaultTemplate
            };
        }
        return DefaultTemplate;
    }
    
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
