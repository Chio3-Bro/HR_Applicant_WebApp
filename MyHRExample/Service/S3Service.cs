using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace MyHRExample.Service
{
    public class S3Service : IDisposable, IAnalysisStorage
    {
        private readonly IConfiguration _configuration;
        private IAmazonS3? _client;

        public S3Service(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string Required(string name)
        {
            var value = _configuration[name];

            if (string.IsNullOrWhiteSpace(value))
                throw new S3ConfigurationException(name);

            return value.Trim();
        }

        private string _bucketName => Required("AWS:BucketName");

        private IAmazonS3 _s3 => _client ??= new AmazonS3Client(
            Required("AWS:AccessKey"),
            Required("AWS:SecretKey"),
            RegionEndpoint.GetBySystemName(Required("AWS:Region"))
        );

        public void Dispose()
        {
            _client?.Dispose();
        }


        // ================================
        // UPLOAD FILE
        // ================================

        public async Task UploadFileAsync(
            Stream stream,
            string objectKey)
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = stream
            };

            await _s3.PutObjectAsync(request);
        }


        // ================================
        // GET FILES
        // ================================

        public async Task<List<S3Object>> GetFilesAsync(
            string? prefix = null)
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            var files = new List<S3Object>();
            ListObjectsV2Response response;
            do
            {
                response = await _s3.ListObjectsV2Async(request);
                if (response.S3Objects != null) files.AddRange(response.S3Objects);
                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated == true);
            return files;
        }


        // ================================
        // GET TEXT FILE CONTENT
        // ================================

        public async Task<string> GetFileContentAsync(
            string objectKey)
        {
            using var response =
                await _s3.GetObjectAsync(
                    _bucketName,
                    objectKey);

            using var reader =
                new StreamReader(response.ResponseStream);

            return await reader.ReadToEndAsync();
        }

        public async Task<string?> ReadResultAsync(string key)
        {
            try { return await GetFileContentAsync(key); }
            catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
            { return null; }
        }


        // ================================
        // DOWNLOAD FILE
        // ================================

        public async Task DownloadFileAsync(
            string objectKey,
            string destinationPath)
        {
            var response =
                await _s3.GetObjectAsync(
                    _bucketName,
                    objectKey);

            await response.WriteResponseStreamToFileAsync(
                destinationPath,
                false,
                default);
        }


        // ================================
        // DELETE FILE
        // ================================

        public async Task DeleteFileAsync(
            string objectKey)
        {
            await _s3.DeleteObjectAsync(
                _bucketName,
                objectKey);
        }
    }
}
