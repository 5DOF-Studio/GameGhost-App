using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Utilities;

namespace WitnessDesktop.Tests.Models;

public class MediaContentTests
{
    [Fact]
    public void Image_WithFilePath_SetsTypeAndPath()
    {
        var media = new MediaContent
        {
            Type = MediaContentType.Image,
            FilePath = "/tmp/screenshot.png"
        };

        media.Type.Should().Be(MediaContentType.Image);
        media.FilePath.Should().Be("/tmp/screenshot.png");
        media.Url.Should().BeNull();
        media.ImageBytes.Should().BeNull();
        media.StartTime.Should().Be(0);
        media.Duration.Should().Be(0);
    }

    [Fact]
    public void Image_WithBytes_SetsTypeAndBytes()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF };
        var media = new MediaContent
        {
            Type = MediaContentType.Image,
            ImageBytes = bytes
        };

        media.Type.Should().Be(MediaContentType.Image);
        media.ImageBytes.Should().BeSameAs(bytes);
    }

    [Fact]
    public void Image_WithUrl_SetsTypeAndUrl()
    {
        var media = new MediaContent
        {
            Type = MediaContentType.Image,
            Url = "https://example.com/image.png"
        };

        media.Type.Should().Be(MediaContentType.Image);
        media.Url.Should().Be("https://example.com/image.png");
    }

    [Fact]
    public void Video_WithFilePathAndSeek_SetsAllProperties()
    {
        var media = new MediaContent
        {
            Type = MediaContentType.Video,
            FilePath = "/tmp/replay-001.mp4",
            StartTime = 15.5,
            Duration = 30.0,
            Title = "THAT FLANK"
        };

        media.Type.Should().Be(MediaContentType.Video);
        media.FilePath.Should().Be("/tmp/replay-001.mp4");
        media.StartTime.Should().Be(15.5);
        media.Duration.Should().Be(30.0);
        media.Title.Should().Be("THAT FLANK");
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var media = new MediaContent();

        media.Type.Should().Be(MediaContentType.Image); // enum default = 0
        media.FilePath.Should().BeNull();
        media.Url.Should().BeNull();
        media.ImageBytes.Should().BeNull();
        media.StartTime.Should().Be(0);
        media.Duration.Should().Be(0);
        media.Title.Should().BeNull();
    }
}

public class TimelineEvent_MediaPropertyTests
{
    [Fact]
    public void Media_DefaultsToNull()
    {
        var evt = new TimelineEvent
        {
            Type = EventOutputType.SageAdvice,
            Summary = "Test"
        };

        evt.Media.Should().BeNull();
    }

    [Fact]
    public void Media_CanBeSetToImageContent()
    {
        var media = new MediaContent
        {
            Type = MediaContentType.Image,
            FilePath = "/tmp/screenshot.png"
        };

        var evt = new TimelineEvent
        {
            Type = EventOutputType.ImageCard,
            Summary = "Screenshot",
            Media = media
        };

        evt.Media.Should().BeSameAs(media);
        evt.Media!.Type.Should().Be(MediaContentType.Image);
    }

    [Fact]
    public void Media_CanBeSetToVideoContent()
    {
        var media = new MediaContent
        {
            Type = MediaContentType.Video,
            FilePath = "/tmp/replay.mp4",
            StartTime = 10.0,
            Duration = 30.0
        };

        var evt = new TimelineEvent
        {
            Type = EventOutputType.VideoCard,
            Summary = "Replay",
            Media = media
        };

        evt.Media.Should().BeSameAs(media);
        evt.Media!.Duration.Should().Be(30.0);
    }
}

public class EventIconMap_MediaCardTests
{
    [Fact]
    public void ImageCard_HasIcon()
    {
        EventIconMap.GetIcon(EventOutputType.ImageCard).Should().Be("video_reel.png");
    }

    [Fact]
    public void VideoCard_HasIcon()
    {
        EventIconMap.GetIcon(EventOutputType.VideoCard).Should().Be("video_reel.png");
    }

    [Fact]
    public void ImageCard_HasCapsuleColor()
    {
        EventIconMap.GetCapsuleColorHex(EventOutputType.ImageCard).Should().NotBe("#30808080",
            "ImageCard should have a specific color, not the default fallback");
    }

    [Fact]
    public void VideoCard_HasCapsuleColor()
    {
        EventIconMap.GetCapsuleColorHex(EventOutputType.VideoCard).Should().NotBe("#30808080",
            "VideoCard should have a specific color, not the default fallback");
    }

    [Fact]
    public void ImageCard_HasCapsuleStroke()
    {
        EventIconMap.GetCapsuleStrokeHex(EventOutputType.ImageCard).Should().NotBe("#50808080",
            "ImageCard should have a specific stroke, not the default fallback");
    }

    [Fact]
    public void VideoCard_HasCapsuleStroke()
    {
        EventIconMap.GetCapsuleStrokeHex(EventOutputType.VideoCard).Should().NotBe("#50808080",
            "VideoCard should have a specific stroke, not the default fallback");
    }
}

public class TimelineEventTemplateSelector_MediaCardTests
{
    [Fact]
    public void ImageCard_SelectsImageCardTemplate()
    {
        var selector = new TimelineEventTemplateSelector
        {
            ImageCardTemplate = new DataTemplate(),
            DefaultTemplate = new DataTemplate()
        };

        var evt = new TimelineEvent { Type = EventOutputType.ImageCard, Summary = "Screenshot" };
        var result = selector.SelectTemplate(evt);

        result.Should().BeSameAs(selector.ImageCardTemplate);
    }

    [Fact]
    public void VideoCard_SelectsVideoCardTemplate()
    {
        var selector = new TimelineEventTemplateSelector
        {
            VideoCardTemplate = new DataTemplate(),
            DefaultTemplate = new DataTemplate()
        };

        var evt = new TimelineEvent { Type = EventOutputType.VideoCard, Summary = "Replay" };
        var result = selector.SelectTemplate(evt);

        result.Should().BeSameAs(selector.VideoCardTemplate);
    }

    [Fact]
    public void ImageCard_FallsBackToDefault_WhenNoImageTemplate()
    {
        var selector = new TimelineEventTemplateSelector
        {
            DefaultTemplate = new DataTemplate()
        };

        var evt = new TimelineEvent { Type = EventOutputType.ImageCard, Summary = "Screenshot" };
        var result = selector.SelectTemplate(evt);

        result.Should().BeSameAs(selector.DefaultTemplate);
    }

    [Fact]
    public void VideoCard_FallsBackToDefault_WhenNoVideoTemplate()
    {
        var selector = new TimelineEventTemplateSelector
        {
            DefaultTemplate = new DataTemplate()
        };

        var evt = new TimelineEvent { Type = EventOutputType.VideoCard, Summary = "Replay" };
        var result = selector.SelectTemplate(evt);

        result.Should().BeSameAs(selector.DefaultTemplate);
    }
}
