using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using MusicPlayer.Models;
using TagLib;

namespace MusicPlayer.Services
{
    public class PlaylistService
    {
        private readonly string _playlistPath;

        public PlaylistService()
        {
            // Get the directory where the executable is located
            var exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
            _playlistPath = Path.Combine(exeDirectory, "playlist.json");
        }

        public void SavePlaylist(List<Song> songs)
        {
            var libraryData = songs.Select(s => new
            {
                FilePath = s.FilePath,
                Title = s.Title,
                Artist = s.Artist,
                Album = s.Album,
                Duration = s.Duration.TotalSeconds
            }).ToList();

            var json = JsonSerializer.Serialize(libraryData, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(_playlistPath, json);
        }

        public List<Song> LoadPlaylist()
        {
            if (!System.IO.File.Exists(_playlistPath))
                return new List<Song>();

            try
            {
                var json = System.IO.File.ReadAllText(_playlistPath);
                var libraryData = JsonSerializer.Deserialize<List<dynamic>>(json);
                var songs = new List<Song>();

                if (libraryData != null)
                {
                    foreach (var item in libraryData)
                    {
                        var element = (JsonElement)item;
                        var filePath = element.GetProperty("FilePath").GetString();
                        
                        if (System.IO.File.Exists(filePath))
                        {
                            try
                            {
                                var tagFile = TagLib.File.Create(filePath);
                                var song = new Song
                                {
                                    FilePath = filePath,
                                    Title = element.GetProperty("Title").GetString() ?? "Unknown Title",
                                    Artist = element.GetProperty("Artist").GetString() ?? "Unknown Artist",
                                    Album = element.GetProperty("Album").GetString() ?? "Unknown Album",
                                    Duration = TimeSpan.FromSeconds(element.GetProperty("Duration").GetDouble()),
                                    AlbumArt = LoadAlbumArt(tagFile)
                                };
                                songs.Add(song);
                            }
                            catch (Exception) { /* ignore */ }
                        }
                    }
                }
                return songs;
            }
            catch (Exception)
            {
                return new List<Song>();
            }
        }
        public List<Song> LoadMusic(string folderPath)
        {
            var songs = new List<Song>();
            // Focus on commonly supported formats
            var supportedFiles = new[] { ".mp3", ".wav", ".flac", ".m4a", ".ogg", ".oga", ".aac", ".wma" };

            foreach (var file in Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => supportedFiles.Contains(Path.GetExtension(f).ToLower())))
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
                    songs.Add(song);
                }
                catch (Exception)
                {
                    // Ignore files that can't be loaded
                }
            }
            return songs;
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
                    bitmap.Freeze(); // Important for performance and cross-thread access
                    return bitmap;
                }
            }
            return null;
        }

        public List<LyricLine> ParseLyrics(Song song)
        {
            // 1. Check for external .lrc file first (preferred)
            var lrcPath = Path.ChangeExtension(song.FilePath, ".lrc");
            if (System.IO.File.Exists(lrcPath))
            {
                return ParseLrc(System.IO.File.ReadAllText(lrcPath, Encoding.UTF8));
            }

            // 2. Check for external .srt file
            var srtPath = Path.ChangeExtension(song.FilePath, ".srt");
            if (System.IO.File.Exists(srtPath))
            {
                return ParseSrt(System.IO.File.ReadAllText(srtPath, Encoding.UTF8));
            }

            // 3. Check for embedded lyrics
            try
            {
                var tagFile = TagLib.File.Create(song.FilePath);
                if (!string.IsNullOrEmpty(tagFile.Tag.Lyrics))
                {
                    // Try to parse as LRC format first, then as plain text
                    var lrcLyrics = ParseLrc(tagFile.Tag.Lyrics);
                    if (lrcLyrics.Any())
                    {
                        return lrcLyrics;
                    }
                }
            }
            catch (Exception) { /* ignore */ }

            return new List<LyricLine>();
        }

        private List<LyricLine> ParseSrt(string srtContent)
        {
            var lyrics = new List<LyricLine>();
            
            // Split by double newlines to separate subtitle blocks
            var blocks = srtContent.Split(new string[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var block in blocks)
            {
                var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length >= 3)
                {
                    // Parse time line (line 1 after sequence number)
                    var timeLine = lines[1];
                    var timeMatch = Regex.Match(timeLine, @"(\d{2}):(\d{2}):(\d{2}),(\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2}),(\d{3})");
                    
                    if (timeMatch.Success)
                    {
                        var startTime = new TimeSpan(0, 
                            int.Parse(timeMatch.Groups[1].Value), 
                            int.Parse(timeMatch.Groups[2].Value), 
                            int.Parse(timeMatch.Groups[3].Value))
                            .Add(TimeSpan.FromMilliseconds(int.Parse(timeMatch.Groups[4].Value)));
                        
                        // Combine all text lines (starting from line 2)
                        var textLines = lines.Skip(2).ToArray();
                        var text = string.Join("\n", textLines).Trim();
                        
                        if (!string.IsNullOrEmpty(text))
                        {
                            lyrics.Add(new LyricLine { Time = startTime, Text = text });
                        }
                    }
                }
            }
            
            return lyrics.OrderBy(l => l.Time).ToList();
        }

        private List<LyricLine> ParseLrc(string lrcContent)
        {
            var lyrics = new List<LyricLine>();
            var lines = lrcContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var tempLyrics = new Dictionary<TimeSpan, List<string>>();

            foreach (var line in lines)
            {
                // Support multiple time stamps in one line [00:12.34][00:56.78]歌词
                var timeMatches = Regex.Matches(line, @"\[(\d{2}):(\d{2})\.(\d{2,3})\]");
                var textMatch = Regex.Match(line, @"\[[\d:.]+\](.*)$");
                
                if (timeMatches.Count > 0 && textMatch.Success)
                {
                    var text = textMatch.Groups[1].Value.Trim();
                    
                    // Skip empty text and metadata lines
                    if (string.IsNullOrEmpty(text) || text.StartsWith("["))
                        continue;
                    
                    foreach (Match timeMatch in timeMatches)
                    {
                        var minutes = int.Parse(timeMatch.Groups[1].Value);
                        var seconds = int.Parse(timeMatch.Groups[2].Value);
                        var millisStr = timeMatch.Groups[3].Value;
                        
                        // Handle both 2-digit and 3-digit milliseconds
                        var milliseconds = millisStr.Length == 2 ? 
                            int.Parse(millisStr) * 10 : 
                            int.Parse(millisStr);
                        
                        var time = new TimeSpan(0, 0, minutes, seconds, milliseconds);
                        
                        // Group lyrics by time stamp
                        if (!tempLyrics.ContainsKey(time))
                        {
                            tempLyrics[time] = new List<string>();
                        }
                        tempLyrics[time].Add(text);
                    }
                }
            }

            // Convert grouped lyrics to LyricLine objects
            foreach (var kvp in tempLyrics.OrderBy(x => x.Key))
            {
                var time = kvp.Key;
                var texts = kvp.Value;
                
                // If multiple texts exist for the same timestamp, treat as bilingual
                if (texts.Count > 1)
                {
                    // Combine as bilingual lyrics (original + translation)
                    var combinedText = string.Join("\n", texts);
                    lyrics.Add(new LyricLine { Time = time, Text = combinedText });
                }
                else
                {
                    // Single text line
                    var text = texts[0];
                    
                    // Check for bilingual lyrics in single line (contains separators)
                    if (text.Contains("｜") || text.Contains("|"))
                    {
                        var separator = text.Contains("｜") ? '｜' : '|';
                        var parts = text.Split(new[] { separator }, 2);
                        if (parts.Length == 2)
                        {
                            var combinedText = parts[0].Trim() + "\n" + parts[1].Trim();
                            lyrics.Add(new LyricLine { Time = time, Text = combinedText });
                        }
                        else
                        {
                            lyrics.Add(new LyricLine { Time = time, Text = text });
                        }
                    }
                    else
                    {
                        lyrics.Add(new LyricLine { Time = time, Text = text });
                    }
                }
            }
            
            return lyrics;
        }
    }
}
