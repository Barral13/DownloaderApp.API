using System.Diagnostics;
using System.Text.RegularExpressions;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace DownloaderApp.API.Services;

public class DownloaderService
{
    private readonly YoutubeClient _youtubeClient;
    private readonly string _ffmpegPath;

    public DownloaderService(YoutubeClient youtubeClient)
    {
        _youtubeClient = youtubeClient;
        _ffmpegPath = Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg", "ben", "ffmpeg.exe");
    }

    public async Task<string> DownloadVideoAsync(string url)
    {
        var (videoId, video) = await GetVideoInfoAsync(url);
        var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);

        var videoStream = streamManifest.GetVideoStreams().GetWithHighestBitrate()
                           ?? throw new InvalidOperationException("Não há streams de vídeo disponíveis.");
        var audioStream = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate()
                           ?? throw new InvalidOperationException("Não há streams de áudio disponíveis.");

        var sanitizedTitle = SanitizeFileName(video.Title);
        var downloadsPath = GetDownloadsPath();
        var videoFilePath = GetUniqueFileName(Path.Combine(downloadsPath, $"{sanitizedTitle}_video.mp4"));
        var audioFilePath = GetUniqueFileName(Path.Combine(downloadsPath, $"{sanitizedTitle}_audio.mp3"));

        await Task.WhenAll(
            _youtubeClient.Videos.Streams.DownloadAsync(videoStream, videoFilePath).AsTask(),
            _youtubeClient.Videos.Streams.DownloadAsync(audioStream, audioFilePath).AsTask()
        );

        var outputFilePath = GetUniqueFileName(Path.Combine(downloadsPath, $"{sanitizedTitle}.mp4"));
        await CombineVideoAndAudioAsync(videoFilePath, audioFilePath, outputFilePath);

        CleanupFiles(videoFilePath, audioFilePath);

        return outputFilePath;
    }

    public async Task<string> DownloadAudioAsync(string url)
    {
        var (videoId, video) = await GetVideoInfoAsync(url);
        var audioStream = (await _youtubeClient.Videos.Streams.GetManifestAsync(videoId))
                            .GetAudioOnlyStreams().GetWithHighestBitrate()
                            ?? throw new InvalidOperationException("Não há streams de áudio disponíveis.");

        var sanitizedTitle = SanitizeFileName(video.Title);
        var downloadsPath = GetDownloadsPath();
        var audioFilePath = GetUniqueFileName(Path.Combine(downloadsPath, $"{sanitizedTitle}.mp3"));

        await _youtubeClient.Videos.Streams.DownloadAsync(audioStream, audioFilePath);
        return audioFilePath;
    }

    private async Task<(string videoId, Video video)> GetVideoInfoAsync(string url)
    {
        var videoId = ExtractVideoId(url) ?? throw new ArgumentException("URL do vídeo inválido.");
        var video = await _youtubeClient.Videos.GetAsync(videoId);
        return (videoId, video);
    }

    private string? ExtractVideoId(string url)
    {
        var regex = new Regex(@"(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:[^\/\n\s]+\/\S+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\n]{11})");
        var match = regex.Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    private string GetDownloadsPath()
    {
        var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (downloadsPath == null)
        {
            throw new InvalidOperationException("Caminho de downloads não encontrado.");
        }
        return downloadsPath;
    }

    private string GetUniqueFileName(string filePath)
    {
        int counter = 1;
        var directory = Path.GetDirectoryName(filePath);
        if (directory == null)
        {
            throw new InvalidOperationException("O diretório do caminho do arquivo não pode ser nulo.");
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        while (File.Exists(filePath))
        {
            filePath = Path.Combine(directory, $"{fileNameWithoutExtension} ({counter}){extension}");
            counter++;
        }

        return filePath;
    }

    private string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }

    private async Task CombineVideoAndAudioAsync(string videoFilePath, string audioFilePath, string outputFilePath)
    {
        if (!File.Exists(_ffmpegPath))
            throw new FileNotFoundException("FFmpeg não encontrado.", _ffmpegPath);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"-i \"{videoFilePath}\" -i \"{audioFilePath}\" -c:v copy -c:a aac -strict experimental \"{outputFilePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = processStartInfo };
        process.Start();
        await Task.WhenAll(process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync());
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException("Erro ao combinar vídeo e áudio.");
    }

    private void CleanupFiles(params string[] filePaths)
    {
        foreach (var filePath in filePaths)
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }
}