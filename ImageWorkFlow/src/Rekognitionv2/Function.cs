using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.CloudWatchEvents.ScheduledEvents;


using Amazon.Rekognition;
using Amazon.Rekognition.Model;

using Amazon.S3;
using Amazon.S3.Model;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;

using Newtonsoft.Json.Linq;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Rekognitionv2
{
    public class Function
    {
        /// <summary>
        /// The default minimum confidence used for detecting labels.
        /// </summary>
        public const float DEFAULT_MIN_CONFIDENCE = 90f;

        /// <summary>
        /// The name of the environment variable to set which will override the default minimum confidence level.
        /// </summary>
        public const string MIN_CONFIDENCE_ENVIRONMENT_VARIABLE_NAME = "MinConfidence";

        /// <summary>
        /// The default table name used for the DynamoDB table.
        /// </summary>
        public const string DEFAULT_DYNAMODB_TABLE = "test_tbl";

        /// <summary>
        /// The name of the DynamoDB table configured for use with this function.
        /// </summary>
        public const string DYNAMODB_TABLE_ENVIRONMENT_VARIABLE_NAME = "DynamoDBTableName";


        IAmazonDynamoDB DynamoDBClient { get; }

        IAmazonRekognition RekognitionClient { get; }

        float MinConfidence { get; set; } = DEFAULT_MIN_CONFIDENCE;

        string DynamoDBTableName { get; set; } = DEFAULT_DYNAMODB_TABLE;

        HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { ".png", ".jpg", ".jpeg" };

        /// <summary>
        /// Default constructor used by AWS Lambda to construct the function. Credentials and Region information will
        /// be set by the running Lambda environment.
        /// 
        /// This constuctor will also search for the environment variable overriding the default minimum confidence level
        /// for label detection.
        /// </summary>
        public Function()
        {
            this.DynamoDBClient = new AmazonDynamoDBClient();
            this.RekognitionClient = new AmazonRekognitionClient();

            var environmentMinConfidence = System.Environment.GetEnvironmentVariable(MIN_CONFIDENCE_ENVIRONMENT_VARIABLE_NAME);
            var environmentDynamoDBTableName = System.Environment.GetEnvironmentVariable(DYNAMODB_TABLE_ENVIRONMENT_VARIABLE_NAME);
            if (!string.IsNullOrWhiteSpace(environmentMinConfidence))
            {
                float value;
                if (float.TryParse(environmentMinConfidence, out value))
                {
                    this.MinConfidence = value;
                    Console.WriteLine($"Setting minimum confidence to {this.MinConfidence}");
                }
                else
                {
                    Console.WriteLine($"Failed to parse value {environmentMinConfidence} for minimum confidence. Reverting back to default of {this.MinConfidence}");
                }
            }
            else
            {
                Console.WriteLine($"Using default minimum confidence of {this.MinConfidence}");
            }

            if (!string.IsNullOrWhiteSpace(environmentDynamoDBTableName))
            {
                this.DynamoDBTableName = environmentDynamoDBTableName;
                Console.WriteLine($"Setting minimum confidence to {this.DynamoDBTableName}");
            }
            else
            {
                Console.WriteLine($"Using default minimum confidence of {this.DynamoDBTableName}");
            }
        }

        /// <summary>
        /// Constructor used for testing which will pass in the already configured service clients.
        /// </summary>
        /// <param name="dynamoDBClient"></param>
        /// <param name="rekognitionClient"></param>
        /// <param name="minConfidence"></param>
        public Function(IAmazonDynamoDB dynamoDBClient, IAmazonRekognition rekognitionClient, float minConfidence)
        {
            this.DynamoDBClient = dynamoDBClient;
            this.RekognitionClient = rekognitionClient;
            this.MinConfidence = minConfidence;
        }

        /// <summary>
        /// A function for responding to CloudWatch events. It will determine if the object is an image and use Amazon Rekognition
        /// to detect labels and add the labels in a DynamoDBTable.
        /// </summary>
        /// <param name="input"></param>
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

            Console.WriteLine($"Looking for labels in image {bucket}:{key}");
            var detectResponses = await this.RekognitionClient.DetectLabelsAsync(new DetectLabelsRequest
            {
                MinConfidence = MinConfidence,
                Image = new Image
                {
                    S3Object = new Amazon.Rekognition.Model.S3Object
                    {
                        Bucket = bucket,
                        Name = key
                    }
                }
            });

            var item = new Dictionary<string, AttributeValue>();
            item.Add("Id", new AttributeValue { S = Guid.NewGuid().ToString() });
            item.Add("ImageName", new AttributeValue { S = key });
            int counter = 1;
            foreach (var label in detectResponses.Labels)
            {
                Console.WriteLine($"\tFound Label {label.Name} with confidence {label.Confidence}");
                item.Add("Label" + counter, new AttributeValue { S = label.Name });
                item.Add("Confidence" + counter, new AttributeValue { N = label.Confidence.ToString() });
                counter++;
            }

            var request = new PutItemRequest(this.DynamoDBTableName, item);
            await this.DynamoDBClient.PutItemAsync(request);

            return;
        }
    }
}
