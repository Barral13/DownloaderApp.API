using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace DownloaderApp.API.Services
{
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

            // Usando FFmpeg para combinar o vídeo e o áudio em um arquivo final
            var outputFilePath = Path.Combine(Path.GetTempPath(), $"{video.Title}.mp4");
            var combined = await CombineVideoAndAudioWithFFmpeg(videoStreamData, audioStreamData, outputFilePath);

            // Retorna o arquivo combinado como stream
            var fileStream = new FileStream(combined, FileMode.Open, FileAccess.Read);
            return (fileStream, $"{video.Title}.mp4");
        }

        public async Task<(Stream stream, string title)> DownloadAudioAsync(string url)
        {
            var (videoId, video) = await GetVideoInfoAsync(url);
            var audioStream = (await _youtubeClient.Videos.Streams.GetManifestAsync(videoId))
                                .GetAudioOnlyStreams().GetWithHighestBitrate()
                                ?? throw new InvalidOperationException("Não há streams de áudio disponíveis.");

            var audioStreamData = await _youtubeClient.Videos.Streams.GetAsync(audioStream);

            // Converte o Stream para um array de bytes
            var audioBytes = await StreamToByteArrayAsync(audioStreamData);

            // Salva o áudio em um arquivo temporário
            var filePath = Path.Combine(Path.GetTempPath(), $"{video.Title}.mp3");
            await File.WriteAllBytesAsync(filePath, audioBytes);

            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return (fileStream, $"{video.Title}.mp3");
        }

        private async Task<byte[]> StreamToByteArrayAsync(Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }


        private async Task<string> CombineVideoAndAudioWithFFmpeg(Stream videoStream, Stream audioStream, string outputFilePath)
        {
            // Salvando os fluxos em arquivos temporários
            var videoFilePath = Path.Combine(Path.GetTempPath(), "video.mp4");
            var audioFilePath = Path.Combine(Path.GetTempPath(), "audio.mp3");

            using (var videoFile = new FileStream(videoFilePath, FileMode.Create, FileAccess.Write))
            using (var audioFile = new FileStream(audioFilePath, FileMode.Create, FileAccess.Write))
            {
                await videoStream.CopyToAsync(videoFile);
                await audioStream.CopyToAsync(audioFile);
            }

            // Caminho relativo para o FFmpeg na pasta 'ffmpeg/bin/ffmpeg.exe'
            var ffmpegPath = Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg", "ben", "ffmpeg.exe");

            // Verifica se o FFmpeg existe no caminho
            if (!File.Exists(ffmpegPath))
            {
                throw new FileNotFoundException("FFmpeg não encontrado no caminho especificado.", ffmpegPath);
            }

            // Combinando vídeo e áudio usando FFmpeg
            var arguments = $"-i \"{videoFilePath}\" -i \"{audioFilePath}\" -c:v copy -c:a aac -strict experimental \"{outputFilePath}\"";
            var processStartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            var process = Process.Start(processStartInfo);

            if (process == null)
            {
                throw new InvalidOperationException("Não foi possível iniciar o processo do FFmpeg.");
            }

            process.WaitForExit();

            // Limpar arquivos temporários
            File.Delete(videoFilePath);
            File.Delete(audioFilePath);

            return outputFilePath;
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
}
