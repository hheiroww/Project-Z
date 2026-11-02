import AppKit
import ScreenCaptureKit
import CoreMedia
import CoreVideo

// Screen pixels remain in bounded, in-memory buffers. No capture files are created.
private let captureError = strdup("Screen capture is unavailable. Grant Screen Recording permission and retry desktop blur.")!
private final class DisplayStream: NSObject, SCStreamOutput {
    let display: SCDisplay
    let bounds: CGRect
    private let gate = NSLock()
    private var latest: CVPixelBuffer?
    var stream: SCStream?
    init(_ display: SCDisplay) {
        self.display = display
        self.bounds = CGDisplayBounds(display.displayID)
    }
    func stream(_ stream: SCStream, didOutputSampleBuffer sampleBuffer: CMSampleBuffer, of type: SCStreamOutputType) {
        guard type == .screen, sampleBuffer.isValid, let pixels = sampleBuffer.imageBuffer else { return }
        if let attachments = CMSampleBufferGetSampleAttachmentsArray(sampleBuffer, createIfNecessary: false) as? [[SCStreamFrameInfo: Any]],
           let rawStatus = attachments.first?[.status] as? Int,
           SCFrameStatus(rawValue: rawStatus) != .complete { return }
        gate.lock(); latest = pixels; gate.unlock()
    }
    func snapshot() -> CVPixelBuffer? {
        gate.lock(); defer { gate.unlock() }; return latest
    }
}
private final class CaptureState {
    let window: NSWindow
    let gate = NSLock()
    var content: SCShareableContent?
    var streams: [CGDirectDisplayID: DisplayStream] = [:]
    var starting: Set<CGDirectDisplayID> = []
    var closed = false
    var failed = false
    init(_ window: NSWindow) {
        self.window = window
        Task {
            do {
                let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: false)
                self.gate.lock(); self.content = content; self.gate.unlock()
            } catch {
                self.gate.lock(); self.failed = true; self.gate.unlock()
            }
        }
    }
    func start(_ display: SCDisplay, content: SCShareableContent) {
        Task {
            let output = DisplayStream(display)
            do {
                let excluded = content.applications.filter { $0.processID == ProcessInfo.processInfo.processIdentifier }
                guard !excluded.isEmpty else { throw NSError(domain: "ProjectZDesktopCapture", code: 1) }
                // Excluding this application's windows also prevents feedback between its blur hosts.
                let filter = SCContentFilter(display: display, excludingApplications: excluded, exceptingWindows: [])
                let configuration = SCStreamConfiguration()
                configuration.width = max(1, display.width / 2)
                configuration.height = max(1, display.height / 2)
                configuration.minimumFrameInterval = CMTime(value: 1, timescale: 30)
                configuration.queueDepth = 2
                configuration.showsCursor = false
                configuration.capturesAudio = false
                configuration.pixelFormat = kCVPixelFormatType_32BGRA
                configuration.colorSpaceName = CGColorSpace.sRGB
                let stream = SCStream(filter: filter, configuration: configuration, delegate: nil)
                output.stream = stream
                try stream.addStreamOutput(output, type: .screen, sampleHandlerQueue: DispatchQueue(label: "ProjectZ.desktop.capture"))
                try await stream.startCapture()
                self.gate.lock()
                let closed = self.closed
                if !closed { self.streams[display.displayID] = output }
                self.starting.remove(display.displayID)
                self.gate.unlock()
                if closed { try? await stream.stopCapture() }
            } catch {
                self.gate.lock(); self.failed = true; self.starting.remove(display.displayID); self.gate.unlock()
            }
        }
    }
    func stop() {
        gate.lock(); closed = true
        let previous = Array(streams.values); streams.removeAll()
        gate.unlock()
        Task { for value in previous { try? await value.stream?.stopCapture(); value.stream = nil } }
    }
}
@_cdecl("pz_capture_error")
public func captureErrorText() -> UnsafePointer<CChar> { UnsafePointer(captureError) }
@_cdecl("pz_capture_request_permission")
public func requestPermission() -> Int32 { CGRequestScreenCaptureAccess() ? 1 : 0 }
@_cdecl("pz_capture_create")
public func createCapture(_ pointer: UnsafeMutableRawPointer?) -> UnsafeMutableRawPointer? {
    guard let pointer = pointer, CGPreflightScreenCaptureAccess() else { return nil }
    let window = Unmanaged<NSWindow>.fromOpaque(pointer).takeUnretainedValue()
    return Unmanaged.passRetained(CaptureState(window)).toOpaque()
}
@_cdecl("pz_capture_bounds")
public func captureBounds(_ pointer: UnsafeMutableRawPointer?, _ x: UnsafeMutablePointer<Int32>, _ y: UnsafeMutablePointer<Int32>,
                          _ width: UnsafeMutablePointer<Int32>, _ height: UnsafeMutablePointer<Int32>, _ scale: UnsafeMutablePointer<Float>) -> Int32 {
    guard let pointer = pointer else { return 0 }
    let state = Unmanaged<CaptureState>.fromOpaque(pointer).takeUnretainedValue()
    guard state.window.isVisible, !state.window.isMiniaturized, let content = state.window.contentView else { return 0 }
    let client = state.window.convertToScreen(content.convert(content.bounds, to: nil))
    let top = CGDisplayBounds(CGMainDisplayID()).height
    x.pointee = Int32(client.minX.rounded())
    y.pointee = Int32((top - client.maxY).rounded())
    width.pointee = Int32(client.width.rounded()); height.pointee = Int32(client.height.rounded()); scale.pointee = 1
    return width.pointee > 0 && height.pointee > 0 ? 1 : 0
}
@_cdecl("pz_capture_read")
public func readCapture(_ pointer: UnsafeMutableRawPointer?, _ x: Int32, _ y: Int32, _ width: Int32, _ height: Int32,
                        _ downsample: Int32, _ rgba: UnsafeMutablePointer<UInt8>?, _ capacity: Int32) -> Int32 {
    guard let pointer = pointer, let rgba = rgba, width > 0, height > 0, width <= 16384, height <= 16384,
          downsample > 0, downsample <= 8 else { return -1 }
    let state = Unmanaged<CaptureState>.fromOpaque(pointer).takeUnretainedValue()
    let area = CGRect(x: Int(x), y: Int(y), width: Int(width), height: Int(height))
    state.gate.lock()
    if state.closed || state.failed { state.gate.unlock(); return -1 }
    guard let content = state.content else { state.gate.unlock(); return 0 }
    let wanted = Array(content.displays.filter { CGDisplayBounds($0.displayID).intersects(area) }.prefix(8))
    let missing = wanted.filter { state.streams[$0.displayID] == nil && !state.starting.contains($0.displayID) }
    for display in missing { state.starting.insert(display.displayID) }
    let values = wanted.compactMap { state.streams[$0.displayID] }
    let old = state.streams.filter { pair in !wanted.contains(where: { $0.displayID == pair.key }) }
    for pair in old { state.streams.removeValue(forKey: pair.key) }
    state.gate.unlock()
    for pair in old { Task { try? await pair.value.stream?.stopCapture(); pair.value.stream = nil } }
    for display in missing { state.start(display, content: content) }
    if values.count != wanted.count || wanted.isEmpty { return 0 }
    let snapshots = values.compactMap { value -> (CGRect, CVPixelBuffer)? in
        guard let pixels = value.snapshot() else { return nil }; return (value.bounds, pixels)
    }
    if snapshots.count != values.count { return 0 }
    let ow = (Int(width) + Int(downsample) - 1) / Int(downsample), oh = (Int(height) + Int(downsample) - 1) / Int(downsample)
    guard ow * oh * 4 <= Int(capacity) else { return -1 }
    for (_, pixels) in snapshots { CVPixelBufferLockBaseAddress(pixels, .readOnly) }
    defer { for (_, pixels) in snapshots { CVPixelBufferUnlockBaseAddress(pixels, .readOnly) } }
    for dy in 0..<oh { for dx in 0..<ow {
        let sx = Double(x) + Double(dx * Int(downsample)), sy = Double(y) + Double(dy * Int(downsample))
        let best = snapshots.min { a, b in
            func distance(_ r: CGRect) -> Double {
                let cx = max(r.minX, min(r.maxX - 1, sx)), cy = max(r.minY, min(r.maxY - 1, sy))
                return (sx - cx) * (sx - cx) + (sy - cy) * (sy - cy)
            }
            return distance(a.0) < distance(b.0)
        }!
        let bw = CVPixelBufferGetWidth(best.1), bh = CVPixelBufferGetHeight(best.1)
        let px = max(0, min(bw - 1, Int((sx - best.0.minX) * Double(bw) / best.0.width)))
        let py = max(0, min(bh - 1, Int((sy - best.0.minY) * Double(bh) / best.0.height)))
        guard let base = CVPixelBufferGetBaseAddress(best.1) else { return -1 }
        let source = base.assumingMemoryBound(to: UInt8.self).advanced(by: py * CVPixelBufferGetBytesPerRow(best.1) + px * 4)
        let destination = rgba.advanced(by: (dy * ow + dx) * 4)
        destination[0] = source[2]; destination[1] = source[1]; destination[2] = source[0]; destination[3] = 255
    } }
    return 1
}
@_cdecl("pz_capture_destroy")
public func destroyCapture(_ pointer: UnsafeMutableRawPointer?) {
    guard let pointer = pointer else { return }
    Unmanaged<CaptureState>.fromOpaque(pointer).takeRetainedValue().stop()
}
