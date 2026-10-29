namespace FSH.Framework.Storage.S3;

public sealed class S3StorageOptions
{
    public string? Bucket { get; set; }
    public string? Region { get; set; }
    public string? Prefix { get; set; }
    public bool PublicRead { get; set; } = true;
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Custom S3 endpoint URL. Set this to point at MinIO or any other S3-compatible
    /// service (e.g. "http://localhost:9000"). Leave empty to target AWS S3.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// The endpoint the browser reaches the storage at, when it differs from
    /// <see cref="ServiceUrl"/>, e.g. the front end's hostname that proxies /storage.
    /// Presigned URLs are signed for this host. Leave empty to sign for <see cref="ServiceUrl"/>.
    /// </summary>
    public string? PublicServiceUrl { get; set; }

    /// <summary>
    /// Explicit access key. When either <see cref="AccessKey"/> or <see cref="SecretKey"/>
    /// is empty, the AWS SDK's ambient credential chain is used instead.
    /// </summary>
    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    /// <summary>
    /// Required for MinIO and most non-AWS S3-compatible services (they do not support
    /// virtual-hosted-style subdomains). Ignored when <see cref="ServiceUrl"/> is empty.
    /// </summary>
    public bool ForcePathStyle { get; set; }
}
