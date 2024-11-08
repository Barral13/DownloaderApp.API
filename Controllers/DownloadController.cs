using DownloaderApp.API.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace DownloaderApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DownloadController : ControllerBase
    {
        private readonly DownloaderService _downloaderService;
        private readonly ILogger<DownloadController> _logger;

        public DownloadController(DownloaderService downloaderService, ILogger<DownloadController> logger)
        {
            _downloaderService = downloaderService;
            _logger = logger;
        }

        [HttpGet("video")]
        public async Task<IActionResult> DownloadVideo([FromQuery] string url)
        {
            return await DownloadMediaAsync(url, true);
        }

        [HttpGet("audio")]
        public async Task<IActionResult> DownloadAudio([FromQuery] string url)
        {
            return await DownloadMediaAsync(url, false);
        }

        private async Task<IActionResult> DownloadMediaAsync(string url, bool isVideo)
        {
            if (string.IsNullOrWhiteSpace(url) || !IsValidUrl(url))
            {
                _logger.LogWarning("URL inv�lida fornecida: {Url}", url);
                return BadRequest("A URL deve ser v�lida.");
            }

            try
            {
                var filePath = isVideo
                    ? await _downloaderService.DownloadVideoAsync(url)
                    : await _downloaderService.DownloadAudioAsync(url);

                var contentType = isVideo ? "video/mp4" : "audio/mpeg";
                var fileName = Path.GetFileName(filePath);

                _logger.LogInformation("Arquivo {FileName} preparado para download.", fileName);
                return PhysicalFile(filePath, contentType, fileName);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Erro ao processar o pedido: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao processar o pedido: {Message}", ex.Message);
                return StatusCode(500, $"Erro ao processar o pedido: {ex.Message}");
            }
        }

        private bool IsValidUrl(string url)
        {
            var regex = new Regex(@"(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:[^\/\n\s]+\/\S+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\n]{11})");
            return regex.IsMatch(url);
        }
    }
}