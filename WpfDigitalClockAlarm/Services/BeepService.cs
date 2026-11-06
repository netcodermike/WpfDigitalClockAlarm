using System;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;

namespace WpfDigitalClockAlarm.Services
{
    public enum AlarmSoundType
    {
        ClassicDigital, // 4-pulse beep (classic alarm clock)
        HighAlert,      // High pitch continuous pulse
        FastChirp       // Fast dual-tone chirp
    }

    public class BeepService
    {
        private bool _isPlaying = false;
        private CancellationTokenSource? _cts;

        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// Generates an in-memory 16-bit 44.1kHz mono WAV buffer for a tone with given frequency, duration, and volume.
        /// Includes a short smooth envelope fade-in/fade-out to eliminate audio clicking.
        /// </summary>
        public static byte[] GenerateWaveform(int frequency, int durationMs, double volume = 0.8)
        {
            if (frequency <= 0 || frequency > 22050) throw new ArgumentOutOfRangeException(nameof(frequency));
            if (durationMs <= 0 || durationMs > 60000) throw new ArgumentOutOfRangeException(nameof(durationMs));
            if (!double.IsFinite(volume)) throw new ArgumentOutOfRangeException(nameof(volume));
            int sampleRate = 44100;
            short bitsPerSample = 16;
            short channels = 1;
            int totalSamples = (sampleRate * durationMs) / 1000;
            int byteRate = sampleRate * channels * (bitsPerSample / 8);
            short blockAlign = (short)(channels * (bitsPerSample / 8));
            int subChunk2Size = totalSamples * (bitsPerSample / 8);
            int chunkSize = 36 + subChunk2Size;

            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);

            // RIFF header
            writer.Write("RIFF"u8.ToArray());
            writer.Write(chunkSize);
            writer.Write("WAVE"u8.ToArray());

            // fmt sub-chunk
            writer.Write("fmt "u8.ToArray());
            writer.Write(16); // PCM subchunk size
            writer.Write((short)1); // PCM
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            // data sub-chunk
            writer.Write("data"u8.ToArray());
            writer.Write(subChunk2Size);

            double amplitude = 32760 * Math.Clamp(volume, 0.0, 1.0);
            double angleIncrement = (2.0 * Math.PI * frequency) / sampleRate;
            double currentAngle = 0;

            // 4ms envelope to prevent click artifacts
            int fadeSamples = Math.Min(sampleRate * 4 / 1000, totalSamples / 4);

            for (int i = 0; i < totalSamples; i++)
            {
                double envelope = 1.0;
                if (i < fadeSamples)
                {
                    envelope = (double)i / fadeSamples;
                }
                else if (i > totalSamples - fadeSamples)
                {
                    envelope = (double)(totalSamples - i) / fadeSamples;
                }

                // Square-ish / rich harmonic wave sounds like real digital alarm clocks!
                double fundamental = Math.Sin(currentAngle);
                double thirdHarmonic = 0.3 * Math.Sin(3 * currentAngle);
                double rawSample = (fundamental + thirdHarmonic) / 1.3;

                short sample = (short)(rawSample * amplitude * envelope);
                writer.Write(sample);

                currentAngle += angleIncrement;
                if (currentAngle > 2 * Math.PI)
                    currentAngle -= 2 * Math.PI;
            }

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Plays a single short test beep asynchronously.
        /// </summary>
        public async Task PlayTestBeepAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    byte[] waveData = GenerateWaveform(1000, 200, 0.85);
                    using var ms = new MemoryStream(waveData);
                    using var player = new SoundPlayer(ms);
                    player.PlaySync();
                }
                catch
                {
                    try { Console.Beep(1000, 200); }
                    catch { SystemSounds.Beep.Play(); }
                }
            });
        }

        /// <summary>
        /// Plays a pleasant ascending two-tone reminder chime (for task reminders).
        /// </summary>
        public virtual async Task PlayReminderChimeAsync()
        {
            await Task.Run(async () =>
            {
                try
                {
                    byte[] tone1 = GenerateWaveform(660, 100, 0.75); // E5
                    byte[] tone2 = GenerateWaveform(880, 160, 0.75); // A5

                    PlayWaveBuffer(tone1);
                    await Task.Delay(50);
                    PlayWaveBuffer(tone2);
                }
                catch
                {
                    try
                    {
                        Console.Beep(660, 100);
                        Console.Beep(880, 160);
                    }
                    catch
                    {
                        SystemSounds.Asterisk.Play();
                    }
                }
            });
        }

        /// <summary>
        /// Starts looping the alarm sound until stopped.
        /// </summary>
        public void StartAlarmLoop(AlarmSoundType soundType = AlarmSoundType.ClassicDigital)
        {
            if (_isPlaying) return;
            _isPlaying = true;
            _cts = new CancellationTokenSource();
            var source = _cts;
            var token = source.Token;

            Task.Run(async () =>
            {
                try
                {
                    byte[] primaryBeep = GenerateWaveform(1150, 100, 0.9);
                    byte[] secondaryBeep = GenerateWaveform(1450, 100, 0.9);

                    while (!token.IsCancellationRequested)
                    {
                        switch (soundType)
                        {
                            case AlarmSoundType.FastChirp:
                                for (int i = 0; i < 2 && !token.IsCancellationRequested; i++)
                                {
                                    PlayWaveBuffer(primaryBeep);
                                    await Task.Delay(40, token).ConfigureAwait(false);
                                    PlayWaveBuffer(secondaryBeep);
                                    await Task.Delay(80, token).ConfigureAwait(false);
                                }
                                await Task.Delay(400, token).ConfigureAwait(false);
                                break;

                            case AlarmSoundType.HighAlert:
                                PlayWaveBuffer(secondaryBeep);
                                await Task.Delay(150, token).ConfigureAwait(false);
                                PlayWaveBuffer(secondaryBeep);
                                await Task.Delay(350, token).ConfigureAwait(false);
                                break;

                            case AlarmSoundType.ClassicDigital:
                            default:
                                for (int i = 0; i < 4 && !token.IsCancellationRequested; i++)
                                {
                                    PlayWaveBuffer(primaryBeep);
                                    await Task.Delay(70, token).ConfigureAwait(false);
                                }
                                await Task.Delay(550, token).ConfigureAwait(false);
                                break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopped
                }
                catch
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            Console.Beep(1000, 150);
                            await Task.Delay(150, token).ConfigureAwait(false);
                        }
                    }
                    catch { }
                }
                finally
                {
                    if (ReferenceEquals(_cts, source)) _isPlaying = false;
                    source.Dispose();
                }
            });
        }

        private static void PlayWaveBuffer(byte[] waveData)
        {
            try
            {
                using var ms = new MemoryStream(waveData);
                using var player = new SoundPlayer(ms);
                player.PlaySync();
            }
            catch
            {
                try { Console.Beep(1100, 100); }
                catch { SystemSounds.Beep.Play(); }
            }
        }

        public void StopAlarm()
        {
            _isPlaying = false;
            try
            {
                _cts?.Cancel();

            }
            catch { }
            finally
            {
                _cts = null;
            }
        }
    }
}
