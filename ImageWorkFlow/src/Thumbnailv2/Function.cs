using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Util;
using Amazon.S3.Model;

using Newtonsoft.Json.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Thumbnailv2
{
    public class Function
    {
        /// <summary>
        /// The default table name used for the DynamoDB table.
        /// </summary>
        public const String DEFAULT_TARGET_BUCKET = "rekognition3005";

        /// <summary>
        /// The name of the DynamoDB table configured for use with this function.
        /// </summary>
        public const String TARGET_BUCKET_ENVIRONMENT_VARIABLE_NAME = "TargetBucketName";

        String TargetBucket { get; set; } = DEFAULT_TARGET_BUCKET;

        HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { ".png", ".jpg", ".jpeg" };

        IAmazonS3 S3Client { get; set; }

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            S3Client = new AmazonS3Client();
            var environmentTargetBucketName = System.Environment.GetEnvironmentVariable(TARGET_BUCKET_ENVIRONMENT_VARIABLE_NAME);
            if (!string.IsNullOrWhiteSpace(environmentTargetBucketName))
            {
                this.TargetBucket = environmentTargetBucketName;
                Console.WriteLine($"Setting target bucket to {this.TargetBucket}");
            }
            else
            {
                Console.WriteLine($"Using default target bucket {this.TargetBucket}");
            }
        }

        /// <summary>
        /// Constructs an instance with a preconfigured S3 client. This can be used for testing the outside of the Lambda environment.
        /// </summary>
        /// <param name="s3Client"></param>
        public Function(IAmazonS3 s3Client)
        {
            this.S3Client = s3Client;
        }

        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
        /// to respond to S3 notifications.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(JObject input, ILambdaContext context)
        {
            String bucket = (string)input["detail"]["requestParameters"]["bucketName"];
            String key = (string)input["detail"]["requestParameters"]["key"];

            if (!SupportedImageTypes.Contains(Path.GetExtension(key)))
            {
                Console.WriteLine($"Object {bucket}:{key} is not a supported image type");
                return;
            }

            try
            {
                var rs = await this.S3Client.GetObjectMetadataAsync(
                    bucket,
                    key);
                
                if (rs.Headers.ContentType.StartsWith("image/"))
                {
                    using (GetObjectResponse response = await S3Client.GetObjectAsync(
                        bucket,
                        key))
                    {
                        using (Stream responseStream = response.ResponseStream)
                        {
                            using (StreamReader reader = new StreamReader(responseStream))
                            {
                                using (var memstream = new MemoryStream())
                                {
                                    var buffer = new byte[512];
                                    var bytesRead = default(int);
                                    while ((bytesRead = reader.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                                        memstream.Write(buffer, 0, bytesRead);
                                    // Perform image manipulation 
                                    Stream transformedImage = GcImagingOperations.GetConvertedImage(memstream.ToArray());
                                    PutObjectRequest putRequest = new PutObjectRequest()
                                    {
                                        BucketName = this.TargetBucket,
                                        Key = $"thumbnail-{key}",
                                        ContentType = rs.Headers.ContentType,
                                        InputStream = transformedImage
                                    };
                                    
                                    await S3Client.PutObjectAsync(putRequest);
                                }
                            }
                        }
                    }
                }
                return;
            }
            catch (Exception e)
            {
                throw;
            }
        }
    }
}
