// AudioDirection.cs
using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

public class AudioDirection : IDisposable
{
    private WasapiLoopbackCapture? capture;
    private readonly int bytesPerSample;
    private readonly int channels;
    private readonly WaveFormat format;

    // smoothing
    private float smoothedValue = 0f;
    private readonly float smoothingAlpha = 0.4f; // 0..1 (higher = less smoothing)

    public event Action<float> OnDirection = delegate { }; // -1 (left) .. 0 .. +1 (right)

    public AudioDirection()
    {
        capture = new WasapiLoopbackCapture();
        format = capture.WaveFormat;
        channels = format.Channels; // expect >=2
        bytesPerSample = format.BitsPerSample / 8;
        capture.DataAvailable += Capture_DataAvailable;
    }

    public void Start() => capture!.StartRecording();
    public void Stop() => capture!.StopRecording();

    private void Capture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        // We assume 16-bit or 32-bit float; handle common formats.
        int bytes = e.BytesRecorded;
        if (channels < 2) return;

        // We'll compute RMS for L and R across buffer
        double sumL = 0, sumR = 0;
        int samples = 0;

        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            // 32-bit float stereo interleaved: L,R,L,R...
            int floats = bytes / 4;
            for (int i = 0; i + 1 < floats; i += channels)
            {
                float l = BitConverter.ToSingle(e.Buffer, (i * 4));
                float r = BitConverter.ToSingle(e.Buffer, ((i + 1) * 4));
                sumL += l * l;
                sumR += r * r;
                samples++;
            }
        }
        else if (format.BitsPerSample == 16)
        {
            // 16-bit PCM
            int samples16 = bytes / 2;
            for (int i = 0; i + 1 < samples16; i += channels)
            {
                short ls = BitConverter.ToInt16(e.Buffer, (i * 2));
                short rs = BitConverter.ToInt16(e.Buffer, ((i + 1) * 2));
                float l = ls / 32768f;
                float r = rs / 32768f;
                sumL += l * l;
                sumR += r * r;
                samples++;
            }
        }
        else
        {
            // Unsupported format (you can add more)
            return;
        }

        if (samples == 0) return;
        double rmsL = Math.Sqrt(sumL / samples);
        double rmsR = Math.Sqrt(sumR / samples);

        // direction scalar: (R - L) / (R + L + eps)
        double eps = 1e-6;
        double dir = (rmsR - rmsL) / (rmsR + rmsL + eps);
        float dirF = (float)Math.Max(-1.0, Math.Min(1.0, dir));

        // smoothing
        smoothedValue = smoothedValue * (1 - smoothingAlpha) + dirF * smoothingAlpha;
        OnDirection?.Invoke(smoothedValue);
    }

    public void Dispose()
    {
        capture?.Dispose();
        capture = null;
    }
}
