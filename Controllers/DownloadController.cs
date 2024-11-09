using System.Text.RegularExpressions;
using DownloaderApp.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace DownloaderApp.API.Controllers;

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

    [HttpGet("status")]
    public IActionResult HealthCheck()
    {
        return Ok(new { Status = "API está funcionando" });
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
            _logger.LogWarning("Forneça uma URL válida: {Url}", url);
            return BadRequest("A URL fornecida é inválida. Por favor, forneça uma URL válida do YouTube.");
        }

        try
        {
            var (fileStream, fileName) = isVideo
                ? await _downloaderService.DownloadVideoAsync(url)
                : await _downloaderService.DownloadAudioAsync(url);

            if (fileStream == null)
            {
                _logger.LogError("Erro ao processar o arquivo.");
                return StatusCode(500, "Erro ao processar o arquivo.");
            }

            var contentType = isVideo ? "video/mp4" : "audio/mpeg";

            _logger.LogInformation("Download iniciado: {FileName}", fileName);

            return File(fileStream, contentType, fileName);
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
