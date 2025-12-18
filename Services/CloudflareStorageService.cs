using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;

namespace MusicWeb.Services;

public class CloudflareStorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _configuration;

    public CloudflareStorageService(IAmazonS3 s3Client, IConfiguration configuration)
    {
        _s3Client = s3Client;
        _configuration = configuration;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string folder, string? subFolder = null)
    {
        var bucketName = _configuration["CloudflareR2:BucketName"];
        var publicDomain = _configuration["CloudflareR2:PublicDomain"];
        
        // Sanitize filename
        var safeFileName = Path.GetFileNameWithoutExtension(fileName)
            .Replace(" ", "-")
            .ToLower() + Path.GetExtension(fileName);
            
        // Construct Key: folder/subFolder/guid_filename OR folder/guid_filename
        var key = string.IsNullOrEmpty(subFolder) 
            ? $"{folder}/{Guid.NewGuid()}_{safeFileName}" 
            : $"{folder}/{subFolder}/{Guid.NewGuid()}_{safeFileName}";

        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = fileStream,
            Key = key,
            BucketName = bucketName,
            DisablePayloadSigning = true // Recommended for R2 compatibility
        };

        var fileTransferUtility = new TransferUtility(_s3Client);
        await fileTransferUtility.UploadAsync(uploadRequest);

        // Construct public URL
        return $"{publicDomain}/{key}";
    }
    public async Task DeleteFileAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl)) return;

        var bucketName = _configuration["CloudflareR2:BucketName"];
        var publicDomain = _configuration["CloudflareR2:PublicDomain"];

        // Extract Key from URL
        // URL: https://public-domain.com/folder/key
        // Key: folder/key
        
        // Remove Protocol (https://) if present in domain config or url logic
        // Simple strategy: Replace PublicDomain with empty string
        
        string key = fileUrl.Replace(publicDomain + "/", "");
        
        try 
        {
            await _s3Client.DeleteObjectAsync(bucketName, key);
        }
        catch (AmazonS3Exception e)
        {
            // Log error or ignore if not found
            Console.WriteLine($"Error deleting file from R2: {e.Message}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error deleting file: {e.Message}");
        }
    }
}
