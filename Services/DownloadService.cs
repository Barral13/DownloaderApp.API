using System.Text.RegularExpressions;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace DownloaderApp.API.Services;

public class DownloaderService
{
    private readonly YoutubeClient _youtubeClient;

    public DownloaderService(YoutubeClient youtubeClient)
    {
        _youtubeClient = youtubeClient ?? throw new ArgumentNullException(nameof(youtubeClient));
    }

    public async Task<(Stream stream, string title)> DownloadVideoAsync(string url)
    {
        var (videoId, video) = await GetVideoInfoAsync(url);
        var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);

        var videoStream = streamManifest.GetVideoStreams().GetWithHighestBitrate()
                           ?? throw new InvalidOperationException("Não há streams de vídeo disponíveis.");
        var audioStream = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate()
                           ?? throw new InvalidOperationException("Não há streams de áudio disponíveis.");

        var videoStreamData = await _youtubeClient.Videos.Streams.GetAsync(videoStream);
        var audioStreamData = await _youtubeClient.Videos.Streams.GetAsync(audioStream);

        var combinedStream = new CombinedStream(videoStreamData, audioStreamData);
        return (combinedStream, $"{video.Title}.mp4");
    }
    public async Task<(Stream stream, string title)> DownloadAudioAsync(string url)
    {
        var (videoId, video) = await GetVideoInfoAsync(url);
        var audioStream = (await _youtubeClient.Videos.Streams.GetManifestAsync(videoId))
                            .GetAudioOnlyStreams().GetWithHighestBitrate()
                            ?? throw new InvalidOperationException("Não há streams de áudio disponíveis.");

        var audioStreamData = await _youtubeClient.Videos.Streams.GetAsync(audioStream);
        return (audioStreamData, $"{video.Title}.mp3");
    }

    private async Task<(string videoId, Video video)> GetVideoInfoAsync(string url)
    {
        var videoId = ExtractVideoId(url) ?? throw new ArgumentException("URL do vídeo inválida.");
        var video = await _youtubeClient.Videos.GetAsync(videoId);
        return (videoId, video);
    }

    private string? ExtractVideoId(string url)
    {
        var regex = new Regex(@"(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:[^\/\n\s]+\/\S+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\n]{11})");
        var match = regex.Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }
}

public class CombinedStream : Stream
{
    private readonly Stream _videoStream;
    private readonly Stream _audioStream;

    public CombinedStream(Stream videoStream, Stream audioStream)
    {
        _videoStream = videoStream ?? throw new ArgumentNullException(nameof(videoStream));
        _audioStream = audioStream ?? throw new ArgumentNullException(nameof(audioStream));
    }

    public override bool CanRead => true;
    public override bool CanWrite => false;
    public override bool CanSeek => false;

    public override long Length => _videoStream.Length + _audioStream.Length;

    public override long Position
    {
        get => _videoStream.Position + _audioStream.Position;
        set
        {
            _videoStream.Position = value;
            _audioStream.Position = value;
        }
    }
    public override void Flush()
    {
        _videoStream.Flush();
        _audioStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = 0;

        if (_videoStream.CanRead)
        {
            bytesRead = _videoStream.Read(buffer, offset, count);
        }

        if (bytesRead < count && _audioStream.CanRead)
        {
            bytesRead += _audioStream.Read(buffer, offset + bytesRead, count - bytesRead);
        }

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException("Seek não é suportado.");

    public override void SetLength(long value)
        => throw new NotSupportedException("SetLength não é suportado.");

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException("Write não é suportado.");
}
