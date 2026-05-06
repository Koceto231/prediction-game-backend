using BPFL.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPFL.API.Features.News
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsController : ControllerBase
    {
        private readonly NewsService _newsService;

        public NewsController(NewsService newsService) => _newsService = newsService;

        /// <summary>GET /api/News?type=MatchPreview&amp;take=10</summary>
        [HttpGet]
        public async Task<IActionResult> GetLatest(
            [FromQuery] NewsType? type,
            [FromQuery] int take = 20,
            CancellationToken ct = default)
        {
            var articles = await _newsService.GetLatestAsync(type, Math.Clamp(take, 1, 50), ct);
            return Ok(articles);
        }

        /// <summary>GET /api/News/{id}</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var article = await _newsService.GetByIdAsync(id, ct);
            return article == null ? NotFound() : Ok(article);
        }

        /// <summary>POST /api/News/preview/{matchId}  [Admin only]</summary>
        [HttpPost("preview/{matchId:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GeneratePreview(int matchId, CancellationToken ct)
        {
            try
            {
                var article = await _newsService.GenerateMatchPreviewAsync(matchId, ct);
                return article == null
                    ? Ok(new { message = "Article already exists for this match." })
                    : Ok(article);
            }
            catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex){ return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>POST /api/News/report/{matchId}  [Admin only]</summary>
        [HttpPost("report/{matchId:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateReport(int matchId, CancellationToken ct)
        {
            try
            {
                var article = await _newsService.GenerateMatchReportAsync(matchId, ct);
                return article == null
                    ? Ok(new { message = "Report already exists for this match." })
                    : Ok(article);
            }
            catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex){ return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>POST /api/News/summary/{leagueCode}  [Admin only]</summary>
        [HttpPost("summary/{leagueCode}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateSummary(string leagueCode, CancellationToken ct)
        {
            try
            {
                var article = await _newsService.GenerateLeagueSummaryAsync(leagueCode.ToUpper(), ct);
                return article == null
                    ? Ok(new { message = "Summary already generated today." })
                    : Ok(article);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>POST /api/News/backfill-images  [Admin only] — generates images for articles that have none</summary>
        [HttpPost("backfill-images")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BackfillImages(
            [FromQuery] bool force = false, CancellationToken ct = default)
        {
            var result = await _newsService.BackfillImagesAsync(force, ct);
            return Ok(result);
        }
    }
}
