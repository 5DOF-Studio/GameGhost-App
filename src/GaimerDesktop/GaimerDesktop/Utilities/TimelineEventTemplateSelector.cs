using GaimerDesktop.Models;
using GaimerDesktop.Models.Timeline;

namespace GaimerDesktop.Utilities;

public class TimelineEventTemplateSelector : DataTemplateSelector
{
    public DataTemplate? DirectMessageTemplate { get; set; }
    public DataTemplate? ProactiveAlertTemplate { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }
    
    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
    {
        if (item is not TimelineEvent evt)
            return DefaultTemplate;
        
        return evt.Type switch
        {
            EventOutputType.DirectMessage => DirectMessageTemplate ?? DefaultTemplate,
            EventOutputType.Danger when evt.Role == MessageRole.Proactive => ProactiveAlertTemplate ?? DefaultTemplate,
            EventOutputType.Opportunity when evt.Role == MessageRole.Proactive => ProactiveAlertTemplate ?? DefaultTemplate,
            _ => DefaultTemplate
        };
    }
}
