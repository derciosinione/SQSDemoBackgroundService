using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SQSDemoBackgroundService
{
    public class SqsBackGroundService : BackgroundService
    {
        private readonly IConfiguration _configuration;

        public SqsBackGroundService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var awsSqsClient = new AmazonSQSClient(RegionEndpoint.USEast1);

            var awsSqsConfigurations = _configuration.GetSection("AwsSqsConfigurations");
            var queueName = awsSqsConfigurations.GetValue<string>("QueueName");
            var queueUrl = await GetQueueUrl(awsSqsClient, queueName);
            
            await Start(awsSqsClient, queueUrl, stoppingToken);
        }

        private async Task Start(AmazonSQSClient awsSqsClient, string queueUrl, CancellationToken stoppingToken)
        {
            Console.WriteLine($"Starting polling queue at {queueUrl}");

            while (!stoppingToken.IsCancellationRequested)
            {
                var messages = await ReceiveMessageAsync(awsSqsClient, queueUrl, 10, 10);

                if (messages.Any())
                {
                    Console.WriteLine($"{messages.Count} messages received");

                    foreach (var msg in messages)
                    {
                        var result = ProcessMessage(msg);

                        if (result)
                        {
                            Console.WriteLine($"{msg.MessageId} processed with success");
                            // await DeleteMessageAsync(awsSqsClient, queueUrl, msg.ReceiptHandle);
                        }
                    }
                }
            }
        }
        
        private static bool ProcessMessage(Message msg)
        {
            Console.WriteLine(msg.Body);
            return true;
        }
        private static async Task<List<Message>> ReceiveMessageAsync(IAmazonSQS client, string queueUrl, int waitTime = 0, int maxMessages = 1)
        {
            var request = new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                WaitTimeSeconds = waitTime,
                MaxNumberOfMessages = maxMessages
            };

            var messages = await client.ReceiveMessageAsync(request);
            
            return messages.Messages;
        }

        private static async Task DeleteMessageAsync(IAmazonSQS client, string queueUrl, string id)
        {
            var request = new DeleteMessageRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = id
            };

            await client.DeleteMessageAsync(request);
        }
        
        private static async Task<string> GetQueueUrl(IAmazonSQS client, string queueName)
        {
            try
            {
                var response = await client.GetQueueUrlAsync(new GetQueueUrlRequest()
                {
                    QueueName = queueName
                });

                return response.QueueUrl;
            }
            catch (QueueDoesNotExistException)
            {
                Console.WriteLine($"---> The Queue {queueName} does not existis!!!");

                var response = await client.CreateQueueAsync(new CreateQueueRequest()
                {
                    QueueName = queueName
                });
                
                Console.WriteLine($"---> The Queue {queueName} has been created!!!");
                return response.QueueUrl;
            }
        }
    }
}