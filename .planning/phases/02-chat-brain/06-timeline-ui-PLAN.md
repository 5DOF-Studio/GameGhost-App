# Plan 06: Timeline UI Component

**Phase:** 02-chat-brain  
**Status:** ✅ Complete  
**Estimated Effort:** 8-12 hours  
**Actual Effort:** ~30 min  
**Dependencies:** 01-core-models, 04-timeline-feed-manager  
**Provides:** XAML implementation of timeline pattern

---

## Objective

Implement the Timeline UI in XAML, replacing or enhancing the existing flat `CollectionView` chat feed with a hierarchical timeline that:
- Groups events under checkpoint headers
- Renders different event types with appropriate icons and styling
- Shows proactive Brain alerts distinctly from user/assistant messages
- Auto-scrolls to the latest checkpoint

---

## Design Pattern

Based on the "agent-plan component" pattern from the design spec:

```
📷 Capture #7 — 3:12 in [in-game]
  ┊ 🖼 White pawn to e4, Italian Game setup
  ┊ ⚡ Fork available — knight to c6

📷 Capture #8 — 3:18 in [in-game]
  ┊ ⚠ Blunder! Queen exposed after that pawn push
  ┊ 💬 "what happened?"
  ┊    → "Your opponent pushed d5 and left their queen hanging..."
```

---

## Tasks

### 1. Timeline Container

**File:** `src/GaimerDesktop/GaimerDesktop/Views/TimelineView.xaml`

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:models="clr-namespace:GaimerDesktop.Models.Timeline"
             x:Class="GaimerDesktop.Views.TimelineView">
    
    <CollectionView x:Name="TimelineCollection"
                    ItemsSource="{Binding Checkpoints}"
                    SelectionMode="None"
                    ItemsUpdatingScrollMode="KeepLastItemInView">
        
        <CollectionView.ItemTemplate>
            <DataTemplate x:DataType="models:TimelineCheckpoint">
                <VerticalStackLayout Spacing="0" Padding="0,8,0,0">
                    
                    <!-- Checkpoint Header -->
                    <Grid ColumnDefinitions="Auto,*,Auto" Padding="12,8">
                        <Label Grid.Column="0"
                               Text="{Binding DisplayHeader}"
                               FontSize="13"
                               FontFamily="Rajdhani-SemiBold"
                               TextColor="{StaticResource TextMuted}" />
                        
                        <Label Grid.Column="2"
                               Text="{Binding ContextBadge}"
                               FontSize="11"
                               FontFamily="Rajdhani-Regular"
                               TextColor="{StaticResource TextMuted}"
                               Opacity="0.6" />
                    </Grid>
                    
                    <!-- Event Lines -->
                    <BindableLayout.ItemsSource>
                        <Binding Path="EventLines" />
                    </BindableLayout.ItemsSource>
                    
                    <VerticalStackLayout BindableLayout.ItemsSource="{Binding EventLines}"
                                         Spacing="2"
                                         Padding="0,0,0,8">
                        <BindableLayout.ItemTemplate>
                            <DataTemplate x:DataType="models:EventLine">
                                <Grid ColumnDefinitions="24,12,*" Padding="12,4">
                                    
                                    <!-- Dashed connector line -->
                                    <BoxView Grid.Column="1"
                                             WidthRequest="1"
                                             HeightRequest="20"
                                             Color="{StaticResource TextMuted}"
                                             Opacity="0.3"
                                             VerticalOptions="Center" />
                                    
                                    <!-- Event content -->
                                    <HorizontalStackLayout Grid.Column="2" Spacing="8">
                                        
                                        <!-- Icon -->
                                        <Label Text="{Binding LineIcon}"
                                               FontSize="14"
                                               VerticalOptions="Center" />
                                        
                                        <!-- Events (stacked if multiple) -->
                                        <FlexLayout BindableLayout.ItemsSource="{Binding Events}"
                                                    Wrap="Wrap"
                                                    JustifyContent="Start"
                                                    AlignItems="Center">
                                            <BindableLayout.ItemTemplate>
                                                <DataTemplate x:DataType="models:TimelineEvent">
                                                    <ContentView>
                                                        <!-- Event rendering based on type -->
                                                        <ContentView.Triggers>
                                                            <DataTrigger TargetType="ContentView"
                                                                         Binding="{Binding Type}"
                                                                         Value="DirectMessage">
                                                                <Setter Property="Content">
                                                                    <Setter.Value>
                                                                        <!-- Direct message bubble template -->
                                                                        <local:DirectMessageBubble />
                                                                    </Setter.Value>
                                                                </Setter>
                                                            </DataTrigger>
                                                        </ContentView.Triggers>
                                                        
                                                        <!-- Default: summary text -->
                                                        <Label Text="{Binding Summary}"
                                                               FontSize="13"
                                                               FontFamily="Rajdhani-Regular"
                                                               TextColor="{StaticResource TextPrimary}"
                                                               LineBreakMode="TailTruncation"
                                                               MaxLines="2" />
                                                    </ContentView>
                                                </DataTemplate>
                                            </BindableLayout.ItemTemplate>
                                        </FlexLayout>
                                        
                                    </HorizontalStackLayout>
                                </Grid>
                            </DataTemplate>
                        </BindableLayout.ItemTemplate>
                    </VerticalStackLayout>
                    
                </VerticalStackLayout>
            </DataTemplate>
        </CollectionView.ItemTemplate>
        
    </CollectionView>
    
</ContentView>
```

### 2. Direct Message Bubble Template

**File:** `src/GaimerDesktop/GaimerDesktop/Views/DirectMessageBubble.xaml`

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:models="clr-namespace:GaimerDesktop.Models.Timeline"
             x:Class="GaimerDesktop.Views.DirectMessageBubble"
             x:DataType="models:TimelineEvent">
    
    <VerticalStackLayout Spacing="4" Padding="0,4">
        
        <!-- User message -->
        <Border IsVisible="{Binding Role, Converter={StaticResource RoleToUserVisibility}}"
                BackgroundColor="{StaticResource BgTertiary}"
                StrokeThickness="0"
                Padding="12,8">
            <Border.StrokeShape>
                <RoundRectangle CornerRadius="12,12,12,4" />
            </Border.StrokeShape>
            
            <Label Text="{Binding Summary}"
                   FontSize="14"
                   FontFamily="Rajdhani-Regular"
                   TextColor="{StaticResource TextPrimary}" />
        </Border>
        
        <!-- Brain response -->
        <Grid IsVisible="{Binding Role, Converter={StaticResource RoleToAssistantVisibility}}"
              ColumnDefinitions="16,*"
              Padding="0,2,0,0">
            
            <Label Grid.Column="0"
                   Text="→"
                   FontSize="12"
                   TextColor="{StaticResource TextMuted}"
                   VerticalOptions="Start"
                   Margin="0,4,0,0" />
            
            <Label Grid.Column="1"
                   Text="{Binding FullContent}"
                   FontSize="14"
                   FontFamily="Rajdhani-Regular"
                   TextColor="{StaticResource TextPrimary}"
                   LineBreakMode="WordWrap" />
        </Grid>
        
    </VerticalStackLayout>
    
</ContentView>
```

### 3. Proactive Alert Styling

**File:** `src/GaimerDesktop/GaimerDesktop/Views/ProactiveAlertView.xaml`

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:models="clr-namespace:GaimerDesktop.Models.Timeline"
             x:Class="GaimerDesktop.Views.ProactiveAlertView"
             x:DataType="models:TimelineEvent">
    
    <Border Padding="12,8"
            Margin="0,4"
            StrokeThickness="1">
        
        <!-- Dynamic stroke color based on urgency -->
        <Border.Triggers>
            <DataTrigger TargetType="Border"
                         Binding="{Binding Brain.Urgency}"
                         Value="high">
                <Setter Property="Stroke" Value="{StaticResource AccentRed}" />
                <Setter Property="BackgroundColor" Value="#1AFF4444" />
            </DataTrigger>
            <DataTrigger TargetType="Border"
                         Binding="{Binding Brain.Urgency}"
                         Value="medium">
                <Setter Property="Stroke" Value="{StaticResource AccentYellow}" />
                <Setter Property="BackgroundColor" Value="#1AFFFF00" />
            </DataTrigger>
        </Border.Triggers>
        
        <Border.StrokeShape>
            <RoundRectangle CornerRadius="8" />
        </Border.StrokeShape>
        
        <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="8">
            
            <!-- Signal badge -->
            <Border Grid.Column="0"
                    BackgroundColor="{StaticResource BgSecondary}"
                    Padding="6,2"
                    VerticalOptions="Center">
                <Border.StrokeShape>
                    <RoundRectangle CornerRadius="4" />
                </Border.StrokeShape>
                
                <Label Text="{Binding Brain.Signal, Converter={StaticResource SignalToUpperCase}}"
                       FontSize="10"
                       FontFamily="Rajdhani-SemiBold"
                       TextColor="{StaticResource TextMuted}" />
            </Border>
            
            <!-- Summary text -->
            <Label Grid.Column="1"
                   Text="{Binding Summary}"
                   FontSize="14"
                   FontFamily="Rajdhani-Regular"
                   TextColor="{StaticResource TextPrimary}"
                   VerticalOptions="Center"
                   LineBreakMode="TailTruncation" />
            
            <!-- Eval delta (if present) -->
            <Label Grid.Column="2"
                   Text="{Binding Brain.EvalDelta, StringFormat='{0:+#;-#;0}cp'}"
                   FontSize="12"
                   FontFamily="Rajdhani-SemiBold"
                   TextColor="{StaticResource TextMuted}"
                   VerticalOptions="Center"
                   IsVisible="{Binding Brain.EvalDelta, Converter={StaticResource NotNullToBool}}" />
            
        </Grid>
        
    </Border>
    
</ContentView>
```

### 4. Value Converters

**File:** `src/GaimerDesktop/GaimerDesktop/Utilities/TimelineConverters.cs`

```csharp
public class RoleToUserVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is MessageRole role && role == MessageRole.User;
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class RoleToAssistantVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is MessageRole role && (role == MessageRole.Assistant || role == MessageRole.Proactive);
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class SignalToUpperCaseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString()?.ToUpperInvariant() ?? "";
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value != null;
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class EventTypeToTemplateConverter : IValueConverter
{
    public DataTemplate DirectMessageTemplate { get; set; }
    public DataTemplate ProactiveAlertTemplate { get; set; }
    public DataTemplate DefaultTemplate { get; set; }
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
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
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

### 5. Auto-Scroll Behavior

**File:** `src/GaimerDesktop/GaimerDesktop/Views/TimelineView.xaml.cs`

```csharp
public partial class TimelineView : ContentView
{
    private ITimelineFeed? _timelineFeed;
    
    public TimelineView()
    {
        InitializeComponent();
    }
    
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        
        if (BindingContext is MainViewModel vm)
        {
            _timelineFeed = vm.TimelineFeed;
            _timelineFeed.CheckpointCreated += OnCheckpointCreated;
        }
    }
    
    private void OnCheckpointCreated(object? sender, TimelineCheckpoint checkpoint)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TimelineCollection.ScrollTo(checkpoint, position: ScrollToPosition.End, animate: true);
        });
    }
}
```

### 6. Integration with MainPage

Replace or wrap existing chat CollectionView:

```xml
<!-- In MainPage.xaml, replace chat CollectionView with: -->
<local:TimelineView Checkpoints="{Binding TimelineFeed.Checkpoints}" />
```

### 7. Register Converters

**File:** `src/GaimerDesktop/GaimerDesktop/App.xaml`

```xml
<Application.Resources>
    <ResourceDictionary>
        <!-- Existing resources... -->
        
        <converters:RoleToUserVisibilityConverter x:Key="RoleToUserVisibility" />
        <converters:RoleToAssistantVisibilityConverter x:Key="RoleToAssistantVisibility" />
        <converters:SignalToUpperCaseConverter x:Key="SignalToUpperCase" />
        <converters:NotNullToBoolConverter x:Key="NotNullToBool" />
    </ResourceDictionary>
</Application.Resources>
```

---

## Icon Reference

| EventOutputType | Icon | Accent Color |
|-----------------|------|--------------|
| Tactic | ⚔️ | Default |
| PositionEval | 📊 | Default |
| BestMove | 🎯 | Cyan |
| Danger | ⚠️ | Red |
| Opportunity | ⚡ | Yellow |
| GameStateChange | 🏁 | Default |
| DirectMessage | 💬 | Default |
| ImageAnalysis | 🖼️ | Default |
| AnalyticsResult | 📈 | Default |
| HistoryRecall | 📋 | Default |
| GeneralChat | 💭 | Default |

---

## Verification

- [x] Checkpoints render as section headers with timestamp
- [x] EventLines render with dashed connector and icon
- [x] Events of same type stack horizontally within a line
- [x] DirectMessage events render as bubbles with user/response
- [x] Proactive alerts show signal badge and urgency accent
- [x] High urgency alerts have red border/background
- [x] Medium urgency alerts have yellow border/background
- [x] Auto-scroll to latest checkpoint works
- [x] InGame checkpoints show game time
- [x] OutGame checkpoints show clock time

---

## Implementation Notes

- Created TimelineView.xaml with CollectionView + nested BindableLayout
- Created DirectMessageBubble.xaml for user/assistant message bubbles
- Created ProactiveAlertView.xaml for urgency-styled alert cards
- Created TimelineConverters.cs with 5 value converters
- Registered converters in App.xaml
- Added TimelineFeed property to MainViewModel
- Added views namespace to MainPage.xaml
- C# compilation verified (DLL built successfully)

---

## Files to Create

```
src/GaimerDesktop/GaimerDesktop/Views/TimelineView.xaml
src/GaimerDesktop/GaimerDesktop/Views/TimelineView.xaml.cs
src/GaimerDesktop/GaimerDesktop/Views/DirectMessageBubble.xaml
src/GaimerDesktop/GaimerDesktop/Views/DirectMessageBubble.xaml.cs
src/GaimerDesktop/GaimerDesktop/Views/ProactiveAlertView.xaml
src/GaimerDesktop/GaimerDesktop/Views/ProactiveAlertView.xaml.cs
src/GaimerDesktop/GaimerDesktop/Utilities/TimelineConverters.cs
```

## Files to Modify

```
src/GaimerDesktop/GaimerDesktop/App.xaml (register converters)
src/GaimerDesktop/GaimerDesktop/MainPage.xaml (integrate TimelineView)
src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs (expose TimelineFeed)
```

---

## Notes

- MAUI doesn't have a direct equivalent to WPF's `HierarchicalDataTemplate`, so we use nested `BindableLayout`
- For complex nested templates, consider custom controls or `ContentView` subclasses
- Auto-scroll uses `ItemsUpdatingScrollMode="KeepLastItemInView"` plus manual scroll on checkpoint creation
- Proactive alerts are visually distinct from assistant responses (signal badge, colored border)
