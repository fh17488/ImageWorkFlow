# Objective

Build a state machine that is triggered when an image is uploaded to an S3 bucket. In response it should invoke two Lambda functions in parallel. These functions should be written in C#. The first Lambda function identifies objects in the image using the AWS Rekognition service and creates a record in a DynamoDB table with the name and the confidence of each identified object. The second Lambda function generates a thumbnail and uploads it to another S3 bucket.

# Components

1. State Machine
2. Lambda function for image recognition
3. Lambda function for generating thumbnails
4. S3 bucket for images
5. S3 bucket for thumbnails
6. DynamoDB table
7. CloudWatch rule
8. CloudTrail for CloudWatch rule
9. S3 bucket for CloudTrail

## State Machine

The type of state machine is express. It calls the Lambda functions in parallel and exits. Figure 1 illustrates this state machine.

 ## CloudWatch Rule

A rule will be configured to trigger the state machine when an object is uploaded to the S3 bucket for images. However, for this the PutObject API must be logged by CloudTrail. To configure this rule follow the instructions at the URL: [https://docs.aws.amazon.com/step-functions/latest/dg/tutorial-cloudwatch-events-s3.html](https://docs.aws.amazon.com/step-functions/latest/dg/tutorial-cloudwatch-events-s3.html).

The JSON object passed to the State Machine by the rule is given in the file &#39;event\_obfuscated.json&#39;. Some strings have been replaced by the token &#39;XXYZ&#39; for security.

## Lambda function for image recognition

This function is written in C#. It is based on a function available in the AWS repository. The function expects images with .png, .jpg or .jpeg extension. It will detect objects in the images using the AWS Rekognition service and will capture labels that AWS Rekognition identifies with a confidence of at least 90%. This value for minimum confidence can be overridden using the environment variable &#39;MinConfidence&#39;. The labels and corresponding confidence of those labels are written to a DynamoDB table. The default name of this DynamoDB table is &#39;test\_tbl&#39; and this name can be overridden using the environment variable &#39;DynamoDBTableName&#39;. Note that an external library has been included to parse the event object generated by the CloudWatch rule. The name of this library is Json.Net library, version 1.0.18, having a footprint of less than 65KB on disk.

The IAM permissions associated with this function should include the ability to invoke the GetObject API from the S3 bucket for images, PutItem API on the DynamoDB table and the permissions that are part of the managed policy &#39;AWSLambdaBasicExecutionRole&#39;.

## Lambda function for generating thumbnails

This function is written in C#. It is based on a function available in the AWS repository. The function expects images with .png, .jpg or .jpeg extension. It will create a thumbnail of size 100x100 pixels in the S3 bucket for thumbnails. The default name of this bucket is &#39;thumbnailtarget3005&#39; and this can be overridden using the environment variable &#39;TargetBucketName&#39;. Two external libraries have been included: the first to generate thumbnails and the second to parse the event object generated by the CloudWatch rule. The first library is GrapeCity.Documents.Imaging version 3.0.0.415 and the second library is Json.Net version 1.0.18.

The IAM permissions associated with this function should include the ability to invoke the GetObject API from the S3 bucket for images, the PutObject API to the S3 bucket for thumbnails and the permissions that are part of the managed policy &#39;AWSLambdaBasicExecutionRole&#39;.
