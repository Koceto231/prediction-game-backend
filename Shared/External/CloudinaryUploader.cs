using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace BPFL.API.Shared.External
{
    /// <summary>
    /// Uploads raw image bytes to Cloudinary and returns a permanent public URL.
    /// </summary>
    public class CloudinaryUploader
    {
        private readonly Cloudinary                  _cloudinary;
        private readonly ILogger<CloudinaryUploader> _logger;

        public CloudinaryUploader(IConfiguration configuration, ILogger<CloudinaryUploader> logger)
        {
            var cloudName  = configuration["Cloudinary:CloudName"]  ?? "";
            var apiKey     = configuration["Cloudinary:ApiKey"]     ?? "";
            var apiSecret  = configuration["Cloudinary:ApiSecret"]  ?? "";

            var account   = new Account(cloudName, apiKey, apiSecret);
            _cloudinary   = new Cloudinary(account) { Api = { Secure = true } };
            _logger       = logger;
        }

        /// <summary>
        /// Upload PNG bytes to Cloudinary folder "bpfl-news".
        /// Returns the secure URL, or null on failure.
        /// </summary>
        public async Task<string?> UploadAsync(
            byte[] imageBytes,
            string publicId,
            CancellationToken ct = default)
        {
            try
            {
                using var stream = new MemoryStream(imageBytes);
                var uploadParams = new ImageUploadParams
                {
                    File      = new FileDescription(publicId, stream),
                    PublicId  = $"bpfl-news/{publicId}",
                    Overwrite = true,
                };

                var result = await _cloudinary.UploadAsync(uploadParams, ct);

                if (result.Error != null)
                {
                    _logger.LogError("Cloudinary upload error: {Msg}", result.Error.Message);
                    throw new Exception($"Cloudinary: {result.Error.Message}");
                }

                var url = result.SecureUrl?.ToString();
                if (string.IsNullOrEmpty(url))
                    throw new Exception("Cloudinary returned no URL.");

                return url;
            }
            catch (Exception ex) when (ex.Message.StartsWith("Cloudinary"))
            {
                throw; // already formatted, propagate as-is
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CloudinaryUploader.UploadAsync failed for {PublicId}", publicId);
                throw new Exception($"Cloudinary exception: {ex.Message}", ex);
            }
        }
    }
}
