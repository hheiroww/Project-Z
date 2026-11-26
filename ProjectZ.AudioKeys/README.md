# ProjectZ.AudioKeys

Analyze PCM in fixed-size windows and turn frequency-band energy into normalized controls. Use the same band frequencies, threshold, gain, response mode, and attack/release settings when playing live or baking a timeline.

Create one `AudioKeyAnalyzer` per stream. Supply consecutive mono or stereo windows at the actual sample cadence and reuse the output spans. The caller owns PCM buffering and hop timing. Mono input uses an empty right channel. Stereo energy is averaged after measuring both channels, which preserves a signal when its channels are out of phase.

RMS and Peak provide continuous controls. Trigger fires on a threshold crossing. Onset responds to new energy relative to recent history and can rearm while music continues playing. A separate analyzer with a short window can drive rhythmic effects while larger windows resolve narrow bass bands.
