using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MusicPlayer.Models;
using MusicPlayer.Services;
using MusicPlayer.Audio;
using NAudio.Wave;
using NAudio.Dsp;
using NAudio.Vorbis;

namespace MusicPlayer.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly PlaylistService _playlistService;
        private WaveOutEvent? _waveOutEvent;
        private AudioFileReader? _audioFileReader;
        private DispatcherTimer _timer;
        private readonly int _fftLength = 4096;
        private readonly NAudio.Dsp.Complex[] _fftBuffer;
        private readonly float[] _spectrumDataArray;
        private SpectrumAnalyzer? _spectrumAnalyzer;
        private readonly object _audioLock = new object();

        private Song? _currentSong;
        public Song? CurrentSong
        {
            get => _currentSong;
            set
            {
                bool wasPlaying = IsPlaying;
                _currentSong = value;
                OnPropertyChanged();
                LoadAndPlaySong();
                
                // If music was playing before, start playing the new song
                if (wasPlaying && _waveOutEvent != null)
                {
                    StartPlayback();
                }
            }
        }

        private ObservableCollection<Song> _playlist = new ObservableCollection<Song>();
        public ObservableCollection<Song> Playlist
        {
            get => _playlist;
            set
            {
                _playlist = value;
                OnPropertyChanged();
                UpdateFilteredPlaylist();
            }
        }

        private ICollectionView? _filteredPlaylist;
        public ICollectionView? FilteredPlaylist
        {
            get => _filteredPlaylist;
            set
            {
                _filteredPlaylist = value;
                OnPropertyChanged();
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        private ObservableCollection<LyricLine> _lyrics = new ObservableCollection<LyricLine>();
        public ObservableCollection<LyricLine> Lyrics
        {
            get => _lyrics;
            set
            {
                _lyrics = value;
                OnPropertyChanged();
            }
        }

        private double _currentPosition;
        public double CurrentPosition
        {
            get => _currentPosition;
            set
            {
                if (Math.Abs(_currentPosition - value) > 1) // Prevent loop from slider update
                {
                    if (_audioFileReader != null)
                    {
                        _audioFileReader.CurrentTime = TimeSpan.FromSeconds(value);
                    }
                }
                _currentPosition = value;
                OnPropertyChanged();
            }
        }

        private double _maxPosition;
        public double MaxPosition
        {
            get => _maxPosition;
            set { _maxPosition = value; OnPropertyChanged(); }
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnPropertyChanged(); }
        }

        private LyricLine? _currentLyricLine;
        public LyricLine? CurrentLyricLine
        {
            get => _currentLyricLine;
            set { _currentLyricLine = value; OnPropertyChanged(); }
        }

        private float _volume = 0.5f;
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                if (_waveOutEvent != null)
                {
                    _waveOutEvent.Volume = value;
                }
                OnPropertyChanged();
            }
        }

        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                if (_waveOutEvent != null)
                {
                    _waveOutEvent.Volume = _isMuted ? 0 : _volume;
                }
                OnPropertyChanged();
            }
        }

        private string _currentTimeText = "00:00";
        public string CurrentTimeText
        {
            get => _currentTimeText;
            set { _currentTimeText = value; OnPropertyChanged(); }
        }

        private string _totalTimeText = "00:00";
        public string TotalTimeText
        {
            get => _totalTimeText;
            set { _totalTimeText = value; OnPropertyChanged(); }
        }

        private ObservableCollection<double> _spectrumData = new ObservableCollection<double>();
        public ObservableCollection<double> SpectrumData
        {
            get => _spectrumData;
            set { _spectrumData = value; OnPropertyChanged(); }
        }

        private bool _isPlaylistCollapsed = false;
        public bool IsPlaylistCollapsed
        {
            get => _isPlaylistCollapsed;
            set { _isPlaylistCollapsed = value; OnPropertyChanged(); }
        }

        public enum PlayMode
        {
            Normal,
            RepeatOne,
            RepeatAll,
            Shuffle
        }

        private PlayMode _currentPlayMode = PlayMode.Normal;
        public PlayMode CurrentPlayMode
        {
            get => _currentPlayMode;
            set { _currentPlayMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(PlayModeText)); }
        }

        public string PlayModeText
        {
            get
            {
                return CurrentPlayMode switch
                {
                    PlayMode.Normal => "Normal",
                    PlayMode.RepeatOne => "Repeat One",
                    PlayMode.RepeatAll => "Repeat All",
                    PlayMode.Shuffle => "Shuffle",
                    _ => "Normal"
                };
            }
        }

        public ICommand PlayPauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand AddMusicCommand { get; }
        public ICommand MuteCommand { get; }
        public ICommand TogglePlayModeCommand { get; }
        public ICommand TogglePlaylistCommand { get; }

        public MainViewModel()
        {
            _playlistService = new PlaylistService();
            _fftBuffer = new NAudio.Dsp.Complex[_fftLength];
            _spectrumDataArray = new float[_fftLength / 2];
            
            PlayPauseCommand = new RelayCommand(PlayPause);
            StopCommand = new RelayCommand(Stop);
            NextCommand = new RelayCommand(Next);
            PreviousCommand = new RelayCommand(Previous);
            AddMusicCommand = new RelayCommand(AddMusic);
            MuteCommand = new RelayCommand(ToggleMute);
            TogglePlayModeCommand = new RelayCommand(TogglePlayMode);
            TogglePlaylistCommand = new RelayCommand(TogglePlaylist);

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(30); // Update every 30ms for smooth visualization
            _timer.Tick += Timer_Tick;

            // Initialize spectrum data with 32 frequency bands
            for (int i = 0; i < 32; i++)
            {
                SpectrumData.Add(0);
            }

            // Load saved library on startup
            LoadSavedLibrary();
        }

        private void LoadSavedLibrary()
        {
            var savedSongs = _playlistService.LoadPlaylist();
            foreach (var song in savedSongs)
            {
                Playlist.Add(song);
            }
            UpdateFilteredPlaylist();
        }

        private void UpdateFilteredPlaylist()
        {
            FilteredPlaylist = CollectionViewSource.GetDefaultView(Playlist);
            if (FilteredPlaylist != null)
            {
                FilteredPlaylist.Filter = FilterSongs;
                
                // Ensure CurrentSong binding is maintained even when filtered
                if (CurrentSong != null)
                {
                    // Force property change notification to refresh UI bindings
                    OnPropertyChanged(nameof(CurrentSong));
                }
            }
        }

        private bool FilterSongs(object obj)
        {
            if (obj is Song song)
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                    return true;

                return song.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       song.Artist.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       song.Album.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private void ApplyFilter()
        {
            FilteredPlaylist?.Refresh();
            
            // Ensure CurrentSong display is preserved during search
            if (CurrentSong != null)
            {
                OnPropertyChanged(nameof(CurrentSong));
            }
        }

        private void AddMusic()
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Audio Files|*.mp3;*.wav;*.flac;*.m4a;*.ogg;*.oga;*.aac;*.wma|" +
                        "MP3 Files|*.mp3|" +
                        "WAV Files|*.wav|" +
                        "FLAC Files|*.flac|" +
                        "M4A Files|*.m4a|" +
                        "OGG Files|*.ogg;*.oga|" +
                        "AAC Files|*.aac|" +
                        "WMA Files|*.wma|" +
                        "All Files|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    try
                    {
                        var tagFile = TagLib.File.Create(file);
                        var song = new Song
                        {
                            FilePath = file,
                            Title = string.IsNullOrEmpty(tagFile.Tag.Title) ? Path.GetFileNameWithoutExtension(file) : tagFile.Tag.Title,
                            Artist = string.IsNullOrEmpty(tagFile.Tag.FirstPerformer) ? "Unknown Artist" : tagFile.Tag.FirstPerformer,
                            Album = string.IsNullOrEmpty(tagFile.Tag.Album) ? "Unknown Album" : tagFile.Tag.Album,
                            Duration = tagFile.Properties.Duration,
                            AlbumArt = LoadAlbumArt(tagFile)
                        };
                        Playlist.Add(song);
                    }
                    catch (Exception) { /* ignore */ }
                }
                
                // Save the updated library and update filtered view
                _playlistService.SavePlaylist(Playlist.ToList());
                UpdateFilteredPlaylist();
            }
        }

        private void LoadAndPlaySong()
        {
            if (CurrentSong == null) return;

            Stop(); // Stop previous song

            // Create appropriate audio reader based on file extension
            _audioFileReader = CreateAudioFileReader(CurrentSong.FilePath);
            if (_audioFileReader == null) return;

            // Create spectrum analyzer
            _spectrumAnalyzer = new SpectrumAnalyzer(_audioFileReader, _fftLength);

            _waveOutEvent = new WaveOutEvent();
            _waveOutEvent.Init(_spectrumAnalyzer);
            _waveOutEvent.Volume = IsMuted ? 0 : Volume;
            
            // Subscribe to PlaybackStopped event for auto-next
            _waveOutEvent.PlaybackStopped += (sender, e) =>
            {
                // Check if we reached the end of the song (not manually stopped)
                if (_audioFileReader?.CurrentTime >= _audioFileReader?.TotalTime && IsPlaying)
                {
                    // Song finished, play next based on mode
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Next();
                    });
                }
            };
            
            MaxPosition = _audioFileReader.TotalTime.TotalSeconds;
            CurrentPosition = 0;
            TotalTimeText = FormatTime(_audioFileReader.TotalTime);

            Lyrics = new ObservableCollection<LyricLine>(_playlistService.ParseLyrics(CurrentSong));

            // Don't automatically start playing - wait for user to click play
        }

        private AudioFileReader? CreateAudioFileReader(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLower();
                
                if (extension == ".ogg" || extension == ".oga")
                {
                    // Use NAudio.Vorbis for OGG files
                    var vorbisReader = new VorbisWaveReader(filePath);
                    // Create a temporary WAV file or use a different approach
                    // For now, we'll create an adapter
                    return new VorbisAudioFileReader(vorbisReader);
                }
                else
                {
                    // Use standard AudioFileReader for other formats
                    return new AudioFileReader(filePath);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading audio file: {ex.Message}", "Audio Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return null;
            }
        }

        private void PlayPause()
        {
            if (_waveOutEvent == null || _audioFileReader == null)
            {
                if (Playlist.Any() && CurrentSong != null)
                {
                    LoadAndPlaySong();
                    if (_waveOutEvent != null)
                    {
                        _waveOutEvent.Play();
                        _timer.Start();
                        IsPlaying = true;
                    }
                }
                return;
            }

            if (_waveOutEvent.PlaybackState == PlaybackState.Playing)
            {
                _waveOutEvent.Pause();
                _timer.Stop();
                IsPlaying = false;
            }
            else
            {
                _waveOutEvent.Play();
                _timer.Start();
                IsPlaying = true;
            }
        }

        private void Stop()
        {
            _waveOutEvent?.Stop();
            _waveOutEvent?.Dispose();
            _waveOutEvent = null;
            _audioFileReader?.Dispose();
            _audioFileReader = null;
            _spectrumAnalyzer = null;
            _timer.Stop();
            IsPlaying = false;
            CurrentPosition = 0;
            
            // Clear spectrum data
            for (int i = 0; i < SpectrumData.Count; i++)
            {
                SpectrumData[i] = 0;
            }
        }

        private void Next()
        {
            if (CurrentSong == null || !Playlist.Any()) return;
            
            bool wasPlaying = IsPlaying;
            int currentIndex = Playlist.IndexOf(CurrentSong);
            
            switch (CurrentPlayMode)
            {
                case PlayMode.Normal:
                    if (currentIndex < Playlist.Count - 1)
                    {
                        CurrentSong = Playlist[currentIndex + 1];
                        if (wasPlaying) StartPlayback();
                    }
                    break;
                case PlayMode.RepeatOne:
                    // Stay on the same song
                    LoadAndPlaySong();
                    if (wasPlaying) StartPlayback();
                    break;
                case PlayMode.RepeatAll:
                    currentIndex = (currentIndex + 1) % Playlist.Count;
                    CurrentSong = Playlist[currentIndex];
                    if (wasPlaying) StartPlayback();
                    break;
                case PlayMode.Shuffle:
                    var random = new Random();
                    var randomIndex = random.Next(Playlist.Count);
                    CurrentSong = Playlist[randomIndex];
                    if (wasPlaying) StartPlayback();
                    break;
            }
        }

        private void Previous()
        {
            if (CurrentSong == null || !Playlist.Any()) return;
            
            bool wasPlaying = IsPlaying;
            int currentIndex = Playlist.IndexOf(CurrentSong);
            
            switch (CurrentPlayMode)
            {
                case PlayMode.Normal:
                    if (currentIndex > 0)
                    {
                        CurrentSong = Playlist[currentIndex - 1];
                        if (wasPlaying) StartPlayback();
                    }
                    break;
                case PlayMode.RepeatOne:
                    // Stay on the same song
                    LoadAndPlaySong();
                    if (wasPlaying) StartPlayback();
                    break;
                case PlayMode.RepeatAll:
                    currentIndex = currentIndex > 0 ? currentIndex - 1 : Playlist.Count - 1;
                    CurrentSong = Playlist[currentIndex];
                    if (wasPlaying) StartPlayback();
                    break;
                case PlayMode.Shuffle:
                    var random = new Random();
                    var randomIndex = random.Next(Playlist.Count);
                    CurrentSong = Playlist[randomIndex];
                    if (wasPlaying) StartPlayback();
                    break;
            }
        }

        private void StartPlayback()
        {
            if (_waveOutEvent != null && _audioFileReader != null)
            {
                _waveOutEvent.Play();
                _timer.Start();
                IsPlaying = true;
            }
        }

        private void TogglePlayMode()
        {
            CurrentPlayMode = CurrentPlayMode switch
            {
                PlayMode.Normal => PlayMode.RepeatOne,
                PlayMode.RepeatOne => PlayMode.RepeatAll,
                PlayMode.RepeatAll => PlayMode.Shuffle,
                PlayMode.Shuffle => PlayMode.Normal,
                _ => PlayMode.Normal
            };
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_audioFileReader != null)
            {
                _currentPosition = _audioFileReader.CurrentTime.TotalSeconds; // Update private field
                OnPropertyChanged(nameof(CurrentPosition)); // Notify UI
                CurrentTimeText = FormatTime(_audioFileReader.CurrentTime);

                if (Lyrics.Any())
                {
                    var currentLine = Lyrics.LastOrDefault(l => l.Time <= _audioFileReader.CurrentTime);
                    CurrentLyricLine = currentLine;
                }

                // Update spectrum visualization with real audio data
                if (IsPlaying)
                {
                    UpdateSpectrumData();
                }
            }
        }

        private void UpdateSpectrumData()
        {
            if (_spectrumAnalyzer == null) return;

            try
            {
                // Get the current spectrum data
                var spectrum = _spectrumAnalyzer.GetSpectrum();
                
                // Group frequencies into 32 bands for visualization
                int bandsCount = SpectrumData.Count;
                int samplesPerBand = Math.Max(1, spectrum.Length / bandsCount);
                
                for (int i = 0; i < bandsCount; i++)
                {
                    float bandValue = 0;
                    int startIndex = i * samplesPerBand;
                    int endIndex = Math.Min(startIndex + samplesPerBand, spectrum.Length);
                    
                    // Average the values in this frequency band
                    int count = 0;
                    for (int j = startIndex; j < endIndex; j++)
                    {
                        // Apply frequency weighting (emphasize mid frequencies)
                        float weight = 1.0f;
                        if (j < spectrum.Length / 8) weight = 0.5f; // Reduce low frequencies
                        else if (j > spectrum.Length * 3 / 4) weight = 0.3f; // Reduce high frequencies
                        
                        bandValue += spectrum[j] * weight;
                        count++;
                    }
                    
                    if (count > 0)
                    {
                        bandValue /= count;
                    }
                    
                    // Apply logarithmic scaling and normalize
                    bandValue = (float)(Math.Log10(1 + bandValue * 9999) / 4.0); // Log scale 0-1
                    
                    // Smooth the transitions for better visual effect
                    var currentValue = SpectrumData[i];
                    var smoothedValue = currentValue * 0.7 + bandValue * 0.3; // Smooth transition
                    
                    // Apply minimum threshold for visual effect
                    smoothedValue = Math.Max(0.02, smoothedValue);
                    
                    SpectrumData[i] = Math.Max(0, Math.Min(1, smoothedValue));
                }
            }
            catch
            {
                // Fallback: gradually fade out spectrum if there's an error
                for (int i = 0; i < SpectrumData.Count; i++)
                {
                    SpectrumData[i] = Math.Max(0.02, SpectrumData[i] * 0.95); // Fade out slowly
                }
            }
        }

        private void ToggleMute()
        {
            IsMuted = !IsMuted;
        }

        private void TogglePlaylist()
        {
            IsPlaylistCollapsed = !IsPlaylistCollapsed;
        }

        private string FormatTime(TimeSpan time)
        {
            return time.ToString(@"mm\:ss");
        }
        
        private BitmapImage? LoadAlbumArt(TagLib.File tagFile)
        {
            if (tagFile.Tag.Pictures.Length > 0)
            {
                var picture = tagFile.Tag.Pictures[0];
                using (var ms = new MemoryStream(picture.Data.Data))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            return null;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public event EventHandler? CanExecuteChanged;
        public RelayCommand(Action execute) { _execute = execute; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
