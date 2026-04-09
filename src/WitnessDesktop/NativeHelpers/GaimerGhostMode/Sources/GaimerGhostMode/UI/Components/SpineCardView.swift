//
//  SpineCardView.swift
//  GhostFab-AppKit
//
//  Spine card container between TopCase and BottomCase.
//  Renders: SpineBrush gradient + HighlightBrush overlay + SpineRadialHighlight overlay
//  with a header row (icon + title label) and content area.
//  Source of truth: DesignReference/GhostFabCodex/UI/Views/CodexRendererView.xaml
//

import AppKit
import AVFoundation
import QuartzCore

/// Spine card view that sits between TopCase and BottomCase in the twin-case layout.
/// Contains gradient layers for the metallic spine appearance, a header with icon and title,
/// and placeholders for content text and image (populated by later plans).
/// Flipped NSView so child layout uses y=0 at top (matches parent GhostContentView).
private class FlippedView: NSView {
    override var isFlipped: Bool { true }
}

public class SpineCardView: NSView {
    private let dragThreshold: CGFloat = 6
    private var isTrackingInteraction = false
    private var isDragging = false
    private var initialScreenPoint = CGPoint.zero
    private var initialPanelOrigin = CGPoint.zero

    // MARK: - Gradient Layers

    private var spineGradient: CAGradientLayer!
    private var highlightGradient: CAGradientLayer!
    private var radialHighlight: CAGradientLayer!

    // MARK: - Subviews (exposed for later plans)

    /// Vertical stack container for header and future content rows.
    public private(set) var contentStack: NSView!

    /// Full-bounds image view for spine card background images.
    public private(set) var contentImage: NSImageView!

    /// Header icon (22x22pt, alpha 0.85).
    public private(set) var eventIcon: NSImageView!

    /// Header title label (13pt bold, white@60% alpha, kern 1.0).
    public private(set) var titleLabel: NSTextField!

        /// Message body label (16pt bold, white, word wrapping, dynamic height).
    public private(set) var messageLabel: NSTextField!

    // MARK: - Video Playback

    private var player: AVPlayer?
    private var playerLayer: AVPlayerLayer?
    private var progressBar: CALayer?
    private var replayIcon: NSImageView?
    private var endTimeObserver: Any?
    private var videoAutoDismissTimer: DispatchSourceTimer?
    private var videoStartTime: TimeInterval = 0
    private var videoDuration: TimeInterval = 0

    // MARK: - Private Header Container

    private var headerView: NSView!

    // MARK: - Initialization

    override public init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        wantsLayer = true
        setupLayers()
        setupSubviews()

        // Initial state: collapsed and invisible
        alphaValue = 0
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    // MARK: - Layer Setup

    private func setupLayers() {
        guard let layer = self.layer else { return }
        layer.masksToBounds = true
        layer.cornerRadius = GhostFabTokens.SpineCornerRadius  // 12pt

        // 1. SpineBrush background gradient
        spineGradient = GhostFabTokens.makeSpineGradient()
        layer.addSublayer(spineGradient)

        // 2. HighlightBrush overlay at 0.85 opacity
        highlightGradient = GhostFabTokens.makeHighlightGradient()
        highlightGradient.opacity = 0.85
        layer.addSublayer(highlightGradient)

        // 3. SpineRadialHighlight overlay for metallic sheen
        radialHighlight = GhostFabTokens.makeSpineRadialHighlightGradient()
        layer.addSublayer(radialHighlight)
    }

    // MARK: - Subview Setup

    private func setupSubviews() {
        // --- Content Stack (flipped vertical container — y=0 at top) ---
        contentStack = FlippedView()
        contentStack.wantsLayer = true
        contentStack.isHidden = true

        // --- Header View (horizontal row) ---
        headerView = NSView()
        headerView.wantsLayer = true

        // --- Event Icon (22x22pt) ---
        eventIcon = NSImageView()
        eventIcon.wantsLayer = true
        eventIcon.imageScaling = .scaleProportionallyUpOrDown
        eventIcon.alphaValue = 0.85

        // --- Title Label (13pt bold, white@60% alpha, kern 1.0) ---
        titleLabel = NSTextField(labelWithString: "")
        titleLabel.isBezeled = false
        titleLabel.drawsBackground = false
        titleLabel.isEditable = false
        titleLabel.isSelectable = false
        titleLabel.font = NSFont.boldSystemFont(ofSize: 13)
        titleLabel.textColor = NSColor(srgbRed: 1.0, green: 1.0, blue: 1.0, alpha: 0.6)  // #99FFFFFF
        // Character spacing (kern) 1.0
        if let font = titleLabel.font {
            let attributes: [NSAttributedString.Key: Any] = [
                .kern: 1.0,
                .font: font,
                .foregroundColor: titleLabel.textColor ?? NSColor.white
            ]
            titleLabel.attributedStringValue = NSAttributedString(string: "", attributes: attributes)
        }

        // --- Message Label (16pt bold, white, word wrapping) ---
        messageLabel = NSTextField(wrappingLabelWithString: "")
        messageLabel.isBezeled = false
        messageLabel.drawsBackground = false
        messageLabel.isEditable = false
        messageLabel.isSelectable = false
        messageLabel.font = NSFont.boldSystemFont(ofSize: 16)
        messageLabel.textColor = NSColor.white
        messageLabel.lineBreakMode = .byWordWrapping
        messageLabel.maximumNumberOfLines = 0
        messageLabel.cell?.wraps = true
        messageLabel.cell?.isScrollable = false

        // --- Content Image (full bounds, aspect fill via layer) ---
        contentImage = NSImageView()
        contentImage.wantsLayer = true
        contentImage.imageScaling = .scaleNone  // Let layer contentsGravity handle fill
        contentImage.layer?.contentsGravity = .resizeAspectFill
        contentImage.layer?.masksToBounds = true
        contentImage.isHidden = true

        // --- Build view hierarchy ---
        headerView.addSubview(eventIcon)
        headerView.addSubview(titleLabel)
        contentStack.addSubview(headerView)
        contentStack.addSubview(messageLabel)

        addSubview(contentStack)
        addSubview(contentImage)
    }

    // MARK: - Layout

    override public func layout() {
        super.layout()

        // Gradient sublayers fill bounds
        spineGradient.frame = bounds
        highlightGradient.frame = bounds
        radialHighlight.frame = bounds

        // Content stack fills bounds
        contentStack.frame = NSRect(x: 0, y: 0, width: bounds.width, height: bounds.height)

        // Header view: left margin 160pt (clears FAB ghost circle), top margin 20pt
        let headerX: CGFloat = 160
        let headerY: CGFloat = 20
        let headerW: CGFloat = bounds.width - headerX - 20  // 20pt right margin
        let headerH: CGFloat = 22  // icon height
        headerView.frame = NSRect(x: headerX, y: headerY, width: headerW, height: headerH)

        // Icon: 22x22 at origin of header
        eventIcon.frame = NSRect(x: 0, y: 0, width: 22, height: 22)

        // Title label: after icon + 8pt spacing
        let labelX: CGFloat = 30  // 22 icon + 8 spacing
        titleLabel.frame = NSRect(x: labelX, y: 0, width: headerView.bounds.width - labelX, height: 22)

        // Message label: below header + 32pt spacer
        // y = 20 (header top margin) + 22 (header height) + 32 (spacer) = 74pt from top
        let messageTop: CGFloat = 20 + 22 + 32  // 74pt
        let messageMargin: CGFloat = 20
        let messageWidth = bounds.width - (messageMargin * 2)
        let messageHeight = max(0, bounds.height - messageTop - messageMargin)
        messageLabel.frame = NSRect(x: messageMargin, y: messageTop, width: messageWidth, height: messageHeight)

        // Content image fills bounds
        contentImage.frame = NSRect(x: 0, y: 0, width: bounds.width, height: bounds.height)

        // Video layers (if active)
        layoutVideoLayers()
    }

    // MARK: - Title Update

    /// Updates the title label text preserving kern spacing.
    public func setTitle(_ text: String) {
        let attributes: [NSAttributedString.Key: Any] = [
            .kern: 1.0,
            .font: titleLabel.font ?? NSFont.boldSystemFont(ofSize: 13),
            .foregroundColor: titleLabel.textColor ?? NSColor.white
        ]
        titleLabel.attributedStringValue = NSAttributedString(string: text, attributes: attributes)
    }

    // MARK: - Content API

    /// Sets the message text content.
    public func setMessage(_ text: String) {
        messageLabel.stringValue = text
    }

    /// Sets the header icon and title.
    public func setHeader(icon: NSImage?, title: String?) {
        eventIcon.image = icon
        eventIcon.alphaValue = 0.85
        setTitle(title ?? "")
    }

    // MARK: - Content Height Measurement

    /// Calculates the required content height for the current message text.
    /// Returns the total height needed including header, spacer, message, and margins.
    /// Minimum height is 120pt (matches MAUI Math.Max(contentHeight, 120)).
    public func calculateContentHeight() -> CGFloat {
        let spineWidth = GhostFabTokens.CaseWidth - (GhostFabTokens.SpineInset * 2)  // 630 - 40 = 590
        let bodyWidth = spineWidth - 40  // 20pt margin each side = 550

        // Measure text height using sizeThatFits
        let measuredSize = messageLabel.sizeThatFits(NSSize(width: bodyWidth, height: CGFloat.greatestFiniteMagnitude))

        // Total: topMargin(20) + header(22) + spacer(32) + textHeight + bottomMargin(20)
        let contentHeight = 20 + 22 + 32 + measuredSize.height + 20

        return max(contentHeight, 120)  // Minimum 120pt
    }

    // MARK: - Video Cleanup

    /// Tears down AVPlayer, removes playerLayer, hides replay icon, cancels timers.
    /// Called on dismiss, on new card (any type), and before showing a new video.
    public func cleanupVideo() {
        // Cancel video auto-dismiss timer
        videoAutoDismissTimer?.cancel()
        videoAutoDismissTimer = nil

        // Remove boundary time observer
        if let observer = endTimeObserver, let player = player {
            player.removeTimeObserver(observer)
        }
        endTimeObserver = nil

        // Stop playback and release player
        player?.pause()
        playerLayer?.removeFromSuperlayer()
        playerLayer = nil
        player = nil

        // Remove progress bar
        progressBar?.removeAllAnimations()
        progressBar?.removeFromSuperlayer()
        progressBar = nil

        // Hide replay icon
        replayIcon?.removeFromSuperview()
        replayIcon = nil

        videoStartTime = 0
        videoDuration = 0
    }

    // MARK: - Video Playback

    /// Animates the spine card open to display a video clip.
    /// Creates an AVPlayer, seeks to startTime, sets forwardPlaybackEndTime,
    /// auto-plays muted, shows a 2px cyan progress bar, and on finish shows
    /// a replay icon with 4-second auto-dismiss.
    /// - Parameters:
    ///   - url: Local file URL of the video segment
    ///   - startTime: Seek offset in seconds within the file
    ///   - duration: Clip length in seconds
    ///   - height: Target height for the spine card
    ///   - completion: Called after height animation completes
    public func showSpineVideo(url: URL, startTime: TimeInterval, duration: TimeInterval,
                               height: CGFloat, completion: (() -> Void)? = nil) {
        // Clean up any existing video first
        cleanupVideo()

        videoStartTime = startTime
        videoDuration = duration

        // Hide text and image content
        contentStack.isHidden = true
        contentImage.isHidden = true

        // --- AVPlayer setup ---
        let asset = AVURLAsset(url: url)
        let item = AVPlayerItem(asset: asset)
        let seekTime = CMTime(seconds: startTime, preferredTimescale: 600)
        let endTime = CMTime(seconds: startTime + duration, preferredTimescale: 600)
        item.forwardPlaybackEndTime = endTime

        let avPlayer = AVPlayer(playerItem: item)
        avPlayer.isMuted = true
        self.player = avPlayer

        // --- AVPlayerLayer setup ---
        let avLayer = AVPlayerLayer(player: avPlayer)
        avLayer.videoGravity = .resizeAspectFill
        avLayer.masksToBounds = true
        avLayer.cornerRadius = GhostFabTokens.SpineCornerRadius  // 12pt match parent

        // Insert above gradient layers (spineGradient, highlightGradient, radialHighlight = indices 0,1,2)
        // but below any future corner mask. Using sublayer count places it on top of existing gradients.
        guard let rootLayer = self.layer else { return }
        let insertIndex = UInt32(min(rootLayer.sublayers?.count ?? 3, 3))
        rootLayer.insertSublayer(avLayer, at: insertIndex)
        self.playerLayer = avLayer

        // --- Progress bar setup ---
        let bar = CALayer()
        bar.backgroundColor = GhostFabTokens.CyanAccent.cgColor
        bar.frame = CGRect(x: 0, y: 0, width: 0, height: 2)  // starts at 0 width, positioned in layout
        bar.cornerRadius = 1
        rootLayer.addSublayer(bar)
        self.progressBar = bar

        // --- Replay icon setup (hidden initially) ---
        let replay = NSImageView()
        replay.wantsLayer = true
        replay.imageScaling = .scaleProportionallyUpOrDown
        replay.alphaValue = 0
        // SF Symbol "arrow.counterclockwise.circle.fill" or fallback to drawn circle
        if let sfImage = NSImage(systemSymbolName: "arrow.counterclockwise.circle.fill",
                                 accessibilityDescription: "Replay") {
            let config = NSImage.SymbolConfiguration(pointSize: 36, weight: .medium)
            replay.image = sfImage.withSymbolConfiguration(config)
            replay.contentTintColor = .white
        }
        addSubview(replay)
        self.replayIcon = replay

        // --- Animate spine open ---
        let cubicOut = CAMediaTimingFunction(controlPoints: 0.215, 0.61, 0.355, 1.0)

        NSAnimationContext.runAnimationGroup({ context in
            context.duration = 0.3
            context.timingFunction = cubicOut
            self.animator().alphaValue = 1.0
        })

        NSAnimationContext.runAnimationGroup({ context in
            context.duration = 0.35
            context.timingFunction = cubicOut
            self.animator().frame = NSRect(
                x: self.frame.origin.x,
                y: self.frame.origin.y,
                width: self.frame.width,
                height: height
            )
        }, completionHandler: { [weak self] in
            guard let self = self else { return }
            completion?()

            // Layout player layer and progress bar to match new bounds
            self.layoutVideoLayers()

            // Seek and play
            avPlayer.seek(to: seekTime, toleranceBefore: .zero, toleranceAfter: .zero) { [weak self] _ in
                guard let self = self else { return }
                avPlayer.play()
                self.startProgressBarAnimation()
            }

            // Register boundary time observer for playback end
            self.endTimeObserver = avPlayer.addBoundaryTimeObserver(
                forTimes: [NSValue(time: endTime)],
                queue: .main
            ) { [weak self] in
                self?.onVideoPlaybackEnded()
            }
        })
    }

    /// Lays out the playerLayer and progressBar to fill current bounds.
    /// Called after spine height animation completes and on layout().
    private func layoutVideoLayers() {
        playerLayer?.frame = bounds
        // Progress bar: 2px at bottom, full width container
        if let bar = progressBar {
            bar.frame = CGRect(x: 0, y: bounds.height - 2, width: 0, height: 2)
        }
        // Replay icon: 48x48 centered
        if let replay = replayIcon {
            let iconSize: CGFloat = 48
            replay.frame = NSRect(
                x: (bounds.width - iconSize) / 2,
                y: (bounds.height - iconSize) / 2,
                width: iconSize,
                height: iconSize
            )
        }
    }

    /// Starts a CABasicAnimation on the progress bar width from 0 to full width over the clip duration.
    private func startProgressBarAnimation() {
        guard let bar = progressBar else { return }
        let fullWidth = bounds.width

        // Set the final frame so the model layer matches the end state
        bar.frame = CGRect(x: 0, y: bounds.height - 2, width: fullWidth, height: 2)

        let anim = CABasicAnimation(keyPath: "bounds.size.width")
        anim.fromValue = 0
        anim.toValue = fullWidth
        anim.duration = videoDuration
        anim.timingFunction = CAMediaTimingFunction(name: .linear)
        anim.isRemovedOnCompletion = false
        anim.fillMode = .forwards
        bar.add(anim, forKey: "progressAnim")
    }

    /// Called when AVPlayer reaches the forwardPlaybackEndTime boundary.
    /// Freezes on last frame, shows replay icon, starts 4s auto-dismiss timer.
    private func onVideoPlaybackEnded() {
        // Player pauses automatically at forwardPlaybackEndTime -- freeze on last frame
        // Show replay icon with fade-in
        NSAnimationContext.runAnimationGroup({ context in
            context.duration = 0.25
            self.replayIcon?.animator().alphaValue = 0.85
        })

        // Start 4-second auto-dismiss timer
        let timer = DispatchSource.makeTimerSource(queue: .main)
        timer.schedule(deadline: .now() + 4.0)
        timer.setEventHandler { [weak self] in
            self?.cleanupVideo()
            // Notify parent to run hideSpine via the same path as card dismiss
            if let parent = self?.superview as? GhostContentView {
                parent.dismissCard()
            }
        }
        timer.resume()
        videoAutoDismissTimer = timer
    }

    /// Handles tap on the replay icon: re-seeks and replays the clip.
    /// Called from mouseUp when replay icon is visible.
    private func replayVideo() {
        guard let player = player else { return }

        // Cancel auto-dismiss timer
        videoAutoDismissTimer?.cancel()
        videoAutoDismissTimer = nil

        // Hide replay icon
        NSAnimationContext.runAnimationGroup({ context in
            context.duration = 0.15
            self.replayIcon?.animator().alphaValue = 0
        })

        // Re-seek and play
        let seekTime = CMTime(seconds: videoStartTime, preferredTimescale: 600)
        player.seek(to: seekTime, toleranceBefore: .zero, toleranceAfter: .zero) { [weak self] _ in
            player.play()
            self?.startProgressBarAnimation()
        }
    }

    // MARK: - Show/Hide Animation

    /// Animates the spine card open to display text content.
    /// Height: 350ms CubicOut, Opacity: 300ms CubicOut.
    /// - Parameters:
    ///   - height: Target height (calculated from content or fixed)
    ///   - completion: Called after height animation completes
    public func showSpine(height: CGFloat, completion: (() -> Void)? = nil) {
        cleanupVideo()

        let cubicOut = CAMediaTimingFunction(controlPoints: 0.215, 0.61, 0.355, 1.0)

        // Make text content visible before animation
        contentStack.isHidden = false
        contentImage.isHidden = true

        // Fade in: 300ms CubicOut
        NSAnimationContext.runAnimationGroup({ context in
            context.duration = 0.3
            context.timingFunction = cubicOut
            self.animator().alphaValue = 1.0
        })

        // Height animation: 350ms CubicOut
        NSAnimationContext.runAnimationGroup({ context in
            context.duration = 0.35
            context.timingFunction = cubicOut
            self.animator().frame = NSRect(
                x: self.frame.origin.x,
                y: self.frame.origin.y,
                width: self.frame.width,
                height: height
            )
        }, completionHandler: {
            completion?()
        })
    }

    /// Animates the spine card open to display an image.
    /// Height: 350ms CubicOut, Opacity: 300ms CubicOut.
    /// - Parameters:
    ///   - image: The image to display (AspectFill)
    ///   - height: Target height
    ///   - completion: Called after height animation completes
    public func showSpineImage(image: NSImage, height: CGFloat, completion: (() -> Void)? = nil) {
        cleanupVideo()

        let cubicOut = CAMediaTimingFunction(controlPoints: 0.215, 0.61, 0.355, 1.0)

        // Toggle content mode: image visible, text hidden
        contentStack.isHidden = true
        contentImage.isHidden = false
        contentImage.image = image

        // Ensure AspectFill via layer
        if let imgLayer = contentImage.layer {
            imgLayer.contentsGravity = .resizeAspectFill
            imgLayer.masksToBounds = true
        }

        // Fade in: 300ms CubicOut
        NSAnimationContext.runAnimationGroup({ context in
            context.duration = 0.3
            context.timingFunction = cubicOut
            self.animator().alphaValue = 1.0
        })

        // Height animation: 350ms CubicOut
        NSAnimationContext.runAnimationGroup({ context in
            context.duration = 0.35
            context.timingFunction = cubicOut
            self.animator().frame = NSRect(
                x: self.frame.origin.x,
                y: self.frame.origin.y,
                width: self.frame.width,
                height: height
            )
        }, completionHandler: {
            completion?()
        })
    }

    /// Animates the spine card closed (height -> 0, fade out).
    /// Height: 300ms CubicIn, Opacity: 200ms CubicIn.
    /// - Parameter completion: Called after height animation completes
    public func hideSpine(completion: (() -> Void)? = nil) {
        cleanupVideo()

        let cubicIn = CAMediaTimingFunction(controlPoints: 0.55, 0.055, 0.675, 0.19)

        // Fade out: 200ms CubicIn
        NSAnimationContext.runAnimationGroup({ context in
            context.duration = 0.2
            context.timingFunction = cubicIn
            self.animator().alphaValue = 0.0
        })

        // Height collapse: 300ms CubicIn
        NSAnimationContext.runAnimationGroup({ context in
            context.duration = 0.3
            context.timingFunction = cubicIn
            self.animator().frame = NSRect(
                x: self.frame.origin.x,
                y: self.frame.origin.y,
                width: self.frame.width,
                height: 0
            )
        }, completionHandler: {
            self.contentStack.isHidden = true
            self.contentImage.isHidden = true
            completion?()
        })
    }

    // MARK: - Frame Change Notification

    /// Notifies parent GhostContentView to re-layout when spine height changes
    /// during animation. The animator proxy drives frame changes through Core
    /// Animation, and each intermediate frame triggers this to keep BottomCase,
    /// separator, and panel height in sync.
    override public func setFrameSize(_ newSize: NSSize) {
        super.setFrameSize(newSize)
        if let parent = superview as? GhostContentView {
            parent.layoutContent()
        }
    }

    // MARK: - Drag Handling

    override public func mouseDown(with event: NSEvent) {
        isTrackingInteraction = true
        isDragging = false
        initialScreenPoint = NSEvent.mouseLocation
        initialPanelOrigin = window?.frame.origin ?? .zero
    }

    override public func mouseDragged(with event: NSEvent) {
        guard isTrackingInteraction,
              let panel = window as? GhostPanel else {
            super.mouseDragged(with: event)
            return
        }

        let currentPoint = NSEvent.mouseLocation
        let dx = currentPoint.x - initialScreenPoint.x
        let dy = currentPoint.y - initialScreenPoint.y
        if !isDragging && hypot(dx, dy) < dragThreshold {
            return
        }

        isDragging = true
        panel.repositionForDrag(
            proposedOrigin: CGPoint(
                x: initialPanelOrigin.x + dx,
                y: initialPanelOrigin.y + dy
            )
        )
    }

    override public func mouseUp(with event: NSEvent) {
        guard isTrackingInteraction else {
            super.mouseUp(with: event)
            return
        }

        defer {
            isTrackingInteraction = false
            isDragging = false
        }

        if isDragging {
            (window as? GhostPanel)?.persistCurrentPlacement()
        } else if let replay = replayIcon, replay.alphaValue > 0.5 {
            // Tap while replay icon is showing -> replay the video
            replayVideo()
        }
    }
}
