///
/// GhostModeHarness — Standalone macOS app for iterating on GhostFab UI.
///
/// Run:  cd src/WitnessDesktop/NativeHelpers/GaimerGhostMode && swift run GhostModeHarness
///
/// Opens the real GhostPanel (NSPanel) alongside a control window with buttons
/// to exercise every visual state without running the full MAUI app.
///

import AppKit
import GaimerGhostMode

// MARK: - Global ref for @convention(c) callbacks

private weak var _harness: HarnessDelegate?

// MARK: - App Delegate

final class HarnessDelegate: NSObject, NSApplicationDelegate {

    private var controlWindow: NSWindow!
    private let sdk = GhostFabPanelSDK()

    private var logView: NSTextView!
    private var stateLabel: NSTextField!

    private let timeFmt: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "HH:mm:ss.SSS"
        return f
    }()

    // MARK: - Launch

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)

        sdk.createPanel()
        sdk.showPanel()
        log("Panel created and shown")

        if let icon = GhostFabResources.image(named: "gaimer-app-icon") {
            sdk.setAgentImage(icon)
            log("Agent image loaded")
        } else {
            log("WARNING: gaimer-app-icon not found")
        }

        if let screen = NSScreen.main {
            let size = GhostFabMetrics.collapsedPanelSize
            let origin = CGPoint(
                x: (screen.frame.width - size.width) / 2,
                y: (screen.frame.height - size.height) / 2
            )
            sdk.setPosition(origin)
        }

        wireCallbacks()

        let existing = sdk.stateController.onStateChange
        sdk.stateController.onStateChange = { [weak self] state in
            existing?(state)
            self?.updateStateSummary(state)
        }

        buildControlWindow()
        updateStateSummary(sdk.stateController.state)
        NSApp.activate(ignoringOtherApps: true)
    }

    // MARK: - Callbacks

    private func wireCallbacks() {
        _harness = self

        sdk.setFabTapCallback {
            _harness?.log("CALLBACK  fab_tap")
        }
        sdk.setCardDismissCallback {
            _harness?.log("CALLBACK  card_dismiss")
        }
        sdk.setGearTapCallback {
            _harness?.log("CALLBACK  gear_tap")
        }
        sdk.setAudioToggleCallback { index, isOn in
            let names = ["VoiceChat", "VoiceCmd", "GameAudio", "AudioIn"]
            let name = index >= 0 && index < 4 ? names[Int(index)] : "?\(index)"
            _harness?.log("CALLBACK  audio_toggle[\(name)] -> \(isOn)")
        }
        log("All callbacks wired")
    }

    // MARK: - Log

    private func log(_ msg: String) {
        let ts = timeFmt.string(from: Date())
        let line = "[\(ts)] \(msg)\n"
        print(line, terminator: "")

        guard let logView = logView else { return }
        DispatchQueue.main.async {
            logView.textStorage?.append(NSAttributedString(
                string: line,
                attributes: [
                    .foregroundColor: NSColor(srgbRed: 0.6, green: 0.9, blue: 0.6, alpha: 1),
                    .font: NSFont.monospacedSystemFont(ofSize: 10, weight: .regular),
                ]
            ))
            logView.scrollToEndOfDocument(nil)
        }
    }

    private func updateStateSummary(_ state: GhostFabPanelState) {
        guard let stateLabel = stateLabel else { return }
        let card: String
        switch state.cardContent {
        case .none: card = "none"
        case .text(let t, _, _, _, let a): card = "text(\(t ?? "-"), alert=\(a))"
        case .image(let t, _, _, let a): card = "image(\(t ?? "-"), alert=\(a))"
        }
        let audio = [
            state.audioState.voiceChat ? "VC" : nil,
            state.audioState.voiceCommand ? "VCmd" : nil,
            state.audioState.gameAudio ? "GA" : nil,
            state.audioState.audioIn ? "AI" : nil,
        ].compactMap { $0 }.joined(separator: "+")

        let text = """
        FAB: \(state.isFabActive ? "active" : "-") \(state.isFabConnected ? "connected" : "-")  \
        |  Audio: \(audio.isEmpty ? "off" : audio)  |  VAD: \(Int(state.vadLevel * 100))%  \
        |  Card: \(card)
        """
        DispatchQueue.main.async { stateLabel.stringValue = text }
    }

    // MARK: - Control Window

    private func buildControlWindow() {
        let w: CGFloat = 400
        let h: CGFloat = 740

        controlWindow = NSWindow(
            contentRect: NSRect(x: 60, y: 100, width: w, height: h),
            styleMask: [.titled, .closable, .miniaturizable],
            backing: .buffered,
            defer: false
        )
        controlWindow.title = "GhostFab Harness"
        controlWindow.isReleasedWhenClosed = false

        let cv = NSView(frame: NSRect(x: 0, y: 0, width: w, height: h))
        cv.wantsLayer = true
        cv.layer?.backgroundColor = NSColor(srgbRed: 0.12, green: 0.12, blue: 0.16, alpha: 1).cgColor
        controlWindow.contentView = cv

        var y: CGFloat = h - 40

        func label(_ text: String) {
            let l = NSTextField(labelWithString: text)
            l.font = .boldSystemFont(ofSize: 11)
            l.textColor = NSColor(srgbRed: 0.7, green: 0.7, blue: 0.8, alpha: 1)
            l.frame = NSRect(x: 16, y: y, width: w - 32, height: 16)
            cv.addSubview(l)
            y -= 22
        }

        func button(_ title: String, action: Selector) {
            let b = NSButton(title: title, target: self, action: action)
            b.bezelStyle = .rounded
            b.frame = NSRect(x: 16, y: y, width: w - 32, height: 26)
            cv.addSubview(b)
            y -= 30
        }

        // Card Content
        label("CARD CONTENT")
        button("Show Text Card", action: #selector(showTextCard))
        button("Show Long Text Card", action: #selector(showLongTextCard))
        button("Show Alert Card (no auto-dismiss)", action: #selector(showAlertCard))
        button("Show Image Card", action: #selector(showImageCard))
        button("Dismiss Card", action: #selector(dismissCard))
        y -= 6

        // FAB State
        label("FAB STATE")
        button("Toggle FAB Active", action: #selector(toggleFabActive))
        button("Toggle FAB Connected", action: #selector(toggleFabConnected))
        y -= 6

        // Audio
        label("AUDIO TOGGLES")
        button("Cycle Audio States", action: #selector(cycleAudioStates))
        button("Set VAD 0%", action: #selector(setVad0))
        button("Set VAD 50%", action: #selector(setVad50))
        button("Set VAD 100%", action: #selector(setVad100))
        y -= 6

        // Demo
        label("DEMO")
        button("Run Scripted Demo (10s)", action: #selector(runScriptedDemo))
        y -= 10

        // State summary
        label("CURRENT STATE")
        stateLabel = NSTextField(labelWithString: "...")
        stateLabel.font = .monospacedSystemFont(ofSize: 9, weight: .regular)
        stateLabel.textColor = NSColor(srgbRed: 0.9, green: 0.85, blue: 0.5, alpha: 1)
        stateLabel.lineBreakMode = .byWordWrapping
        stateLabel.maximumNumberOfLines = 3
        stateLabel.frame = NSRect(x: 16, y: y - 30, width: w - 32, height: 36)
        cv.addSubview(stateLabel)
        y -= 44

        // Event log
        label("EVENT LOG")
        let logH = max(y - 16, 120)
        let sv = NSScrollView(frame: NSRect(x: 16, y: 16, width: w - 32, height: logH))
        sv.hasVerticalScroller = true
        sv.autohidesScrollers = false
        sv.borderType = .bezelBorder

        logView = NSTextView(frame: sv.contentView.bounds)
        logView.isEditable = false
        logView.isSelectable = true
        logView.backgroundColor = NSColor(srgbRed: 0.06, green: 0.06, blue: 0.1, alpha: 1)
        logView.textContainerInset = NSSize(width: 4, height: 4)
        logView.autoresizingMask = [.width, .height]
        sv.documentView = logView
        cv.addSubview(sv)

        controlWindow.makeKeyAndOrderFront(nil)
    }

    // MARK: - Card Actions

    @objc private func showTextCard() {
        log("ACTION  showTextCard")
        sdk.showTextCard(title: "SAGE ADVICE",
                         message: "Consider developing your knight to f3 before committing the bishop.")
    }

    @objc private func showLongTextCard() {
        log("ACTION  showLongTextCard")
        sdk.showTextCard(
            title: "ANALYSIS",
            message: "Your opponent has a strong pawn structure on the queenside. I recommend focusing on the kingside where you have more space. Consider Nf3-g5 to pressure the f7 square, followed by Qh5 if the knight is not challenged. Watch for the back rank — your king needs luft."
        )
    }

    @objc private func showAlertCard() {
        log("ACTION  showAlertCard")
        sdk.showTextCard(
            title: "DANGER",
            message: "Back rank mate threat detected! Move your rook or create an escape square immediately.",
            isAlert: true
        )
    }

    @objc private func showImageCard() {
        log("ACTION  showImageCard")
        // Load test image from harness resources, fall back to ghost asset
        let img = loadHarnessImage("test-image")
            ?? GhostFabResources.image(named: "gaimer-app-icon")
        if let img = img {
            sdk.showImageCard(title: "BOARD SNAPSHOT", image: img, fixedHeight: 200)
        }
    }

    private func loadHarnessImage(_ name: String) -> NSImage? {
        guard let url = Bundle.module.url(forResource: name, withExtension: "png", subdirectory: "Resources") else {
            return nil
        }
        return NSImage(contentsOf: url)
    }

    @objc private func dismissCard() {
        log("ACTION  dismissCard")
        sdk.dismissCard()
    }

    // MARK: - FAB Actions

    private var fabActive = false
    private var fabConnected = false

    @objc private func toggleFabActive() {
        fabActive.toggle()
        log("ACTION  setFabActive(\(fabActive))")
        sdk.setFabActive(fabActive)
    }

    @objc private func toggleFabConnected() {
        fabConnected.toggle()
        log("ACTION  setFabConnected(\(fabConnected))")
        sdk.setFabConnected(fabConnected)
    }

    // MARK: - Audio Actions

    private var audioStep = 0

    @objc private func cycleAudioStates() {
        audioStep = (audioStep + 1) % 5
        let states: [GhostFabAudioState] = [
            GhostFabAudioState(),
            GhostFabAudioState(voiceChat: true),
            GhostFabAudioState(voiceChat: true, voiceCommand: true),
            GhostFabAudioState(voiceChat: true, voiceCommand: true, gameAudio: true),
            GhostFabAudioState(voiceChat: true, voiceCommand: true, gameAudio: true, audioIn: true),
        ]
        log("ACTION  cycleAudioStates -> step \(audioStep)")
        sdk.setAudioState(states[audioStep])
    }

    @objc private func setVad0() { log("ACTION  VAD 0%"); sdk.setVadLevel(0) }
    @objc private func setVad50() { log("ACTION  VAD 50%"); sdk.setVadLevel(0.5) }
    @objc private func setVad100() { log("ACTION  VAD 100%"); sdk.setVadLevel(1.0) }

    // MARK: - Scripted Demo

    @objc private func runScriptedDemo() {
        log("ACTION  scripted demo start")
        let image = GhostFabResources.image(named: "gaimer-app-icon")

        let steps: [(TimeInterval, GhostFabPanelState)] = [
            (0.0, GhostFabPanelState()),
            (0.5, GhostFabPanelState(
                agentImage: image, isFabActive: false, isFabConnected: true,
                audioState: GhostFabAudioState(voiceChat: true), vadLevel: 0.2
            )),
            (1.5, GhostFabPanelState(
                agentImage: image, isFabActive: true, isFabConnected: true,
                audioState: GhostFabAudioState(voiceChat: true), vadLevel: 0.45,
                cardContent: .text(title: "SAGE ADVICE",
                                   message: "Improve the bishop before committing the queen. You have initiative here.")
            )),
            (4.0, GhostFabPanelState(
                agentImage: image, isFabActive: true, isFabConnected: true,
                audioState: GhostFabAudioState(voiceChat: true, gameAudio: true), vadLevel: 0.8,
                cardContent: .text(title: "DANGER",
                                   message: "Back rank mate threat! Create an escape square now.",
                                   isAlert: true)
            )),
            (7.0, GhostFabPanelState(
                agentImage: image, isFabActive: true, isFabConnected: true,
                audioState: GhostFabAudioState(voiceChat: true, voiceCommand: true, gameAudio: true, audioIn: true),
                vadLevel: 0.9,
                cardContent: .image(title: "BOARD SNAPSHOT", image: image ?? NSImage(), fixedHeight: 200)
            )),
            (10.0, GhostFabPanelState()),
        ]

        for (delay, state) in steps {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) { [weak self] in
                self?.sdk.replaceState(with: state)
                self?.log("DEMO  t+\(delay)s applied")
            }
        }
    }
}

// MARK: - Launch

let app = NSApplication.shared
let delegate = HarnessDelegate()
app.delegate = delegate
app.run()
