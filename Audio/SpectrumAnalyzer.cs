using System;
using NAudio.Wave;
using NAudio.Dsp;

namespace MusicPlayer.Audio
{
    public class SpectrumAnalyzer : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _channels;
        private readonly int _sampleRate;
        private readonly int _fftLength;
        private readonly Complex[] _fftBuffer;
        private readonly float[] _lastSpectrum;
        private readonly float[] _window;
        private int _fftPos;
        private readonly object _lock = new object();

        public WaveFormat WaveFormat => _source.WaveFormat;
        public float[] GetSpectrum() 
        { 
            lock (_lock)
            {
                return (float[])_lastSpectrum.Clone();
            }
        }

        public SpectrumAnalyzer(ISampleProvider source, int fftLength = 4096)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            _sampleRate = source.WaveFormat.SampleRate;
            _fftLength = fftLength;
            _fftBuffer = new Complex[fftLength];
            _lastSpectrum = new float[fftLength / 2];
            
            // Create Hann window for better frequency resolution
            _window = new float[fftLength];
            for (int i = 0; i < fftLength; i++)
            {
                _window[i] = 0.5f * (1 - (float)Math.Cos(2 * Math.PI * i / (fftLength - 1)));
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);

            // Process audio data for spectrum analysis
            for (int i = 0; i < samplesRead; i += _channels)
            {
                // Mix channels to mono
                float sample = 0;
                for (int channel = 0; channel < _channels; channel++)
                {
                    if (i + channel < samplesRead)
                        sample += buffer[offset + i + channel];
                }
                sample /= _channels;

                // Add to FFT buffer
                _fftBuffer[_fftPos] = new Complex { X = sample * _window[_fftPos], Y = 0 };
                _fftPos++;

                // Perform FFT when buffer is full
                if (_fftPos >= _fftLength)
                {
                    _fftPos = 0;
                    ProcessFFT();
                }
            }

            return samplesRead;
        }

        private void ProcessFFT()
        {
            // Perform FFT
            var fftData = new Complex[_fftLength];
            Array.Copy(_fftBuffer, fftData, _fftLength);
            
            // Use a simple FFT implementation
            PerformFFT(fftData);

            // Calculate magnitude spectrum
            lock (_lock)
            {
                for (int i = 0; i < _lastSpectrum.Length; i++)
                {
                    _lastSpectrum[i] = (float)Math.Sqrt(fftData[i].X * fftData[i].X + fftData[i].Y * fftData[i].Y);
                }
            }
        }

        private void PerformFFT(Complex[] buffer)
        {
            int n = buffer.Length;
            if (n <= 1) return;

            // Bit-reversal permutation
            int j = 0;
            for (int i = 1; i < n; i++)
            {
                int bit = n >> 1;
                while (j >= bit)
                {
                    j -= bit;
                    bit >>= 1;
                }
                j += bit;
                if (i < j)
                {
                    (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
                }
            }

            // Cooley-Tukey FFT
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                Complex wlen = new Complex { X = (float)Math.Cos(ang), Y = (float)Math.Sin(ang) };
                for (int i = 0; i < n; i += len)
                {
                    Complex w = new Complex { X = 1, Y = 0 };
                    for (int k = 0; k < len / 2; k++)
                    {
                        Complex u = buffer[i + k];
                        Complex v = ComplexMultiply(buffer[i + k + len / 2], w);
                        buffer[i + k] = ComplexAdd(u, v);
                        buffer[i + k + len / 2] = ComplexSubtract(u, v);
                        w = ComplexMultiply(w, wlen);
                    }
                }
            }
        }

        private Complex ComplexAdd(Complex a, Complex b)
        {
            return new Complex { X = a.X + b.X, Y = a.Y + b.Y };
        }

        private Complex ComplexSubtract(Complex a, Complex b)
        {
            return new Complex { X = a.X - b.X, Y = a.Y - b.Y };
        }

        private Complex ComplexMultiply(Complex a, Complex b)
        {
            return new Complex 
            { 
                X = a.X * b.X - a.Y * b.Y, 
                Y = a.X * b.Y + a.Y * b.X 
            };
        }
    }
}
