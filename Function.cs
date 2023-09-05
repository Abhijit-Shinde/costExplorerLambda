using Amazon.CostExplorer.Model;
using Amazon.CostExplorer;
using Amazon.Lambda.Core;
using Amazon.SimpleNotificationService;
using System.Globalization;
using Amazon.SimpleNotificationService.Model;
using Amazon.Lambda.Serialization.SystemTextJson;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace costExplorerLambda;

public class Function
{
    private string? response;
    private string? totalCost;
    DateTime currentDate = DateTime.Now;
    public async Task FunctionHandler(ILambdaContext context)
    {
        context.Logger.LogInformation("Starting..");

        var costExplorerClient = new AmazonCostExplorerClient();
        var snsClient = new AmazonSimpleNotificationServiceClient();

        // Calculate the start and end of the previous week (Monday to Sunday)
        DateTime startDate = currentDate.Date.AddDays(-(int)currentDate.DayOfWeek - 6);
        DateTime endDate = startDate.AddDays(6).AddHours(23).AddMinutes(59).AddSeconds(59);

        try
        {
            var request = await costExplorerClient.GetCostAndUsageAsync(
                new GetCostAndUsageRequest
                {
                    TimePeriod = new DateInterval
                    {
                        Start = startDate.ToString("yyyy-MM-dd"),
                        End = endDate.ToString("yyyy-MM-dd")
                    },
                    Granularity = Granularity.MONTHLY,
                    Metrics = new List<string> { "UnblendedCost" }
                });

            response = request.ResultsByTime[0].Total["UnblendedCost"].Amount;
            totalCost = decimal.Parse(response).ToString("C", CultureInfo.CreateSpecificCulture("en-US"));

            try
            {
                var res=await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = Environment.GetEnvironmentVariable("topicARN"),
                    Subject = "Weekly AWS Account Usage",
                    Message = $"Resource Usage Cost: {totalCost}",
                });

                context.Logger.LogInformation($"SNS {res.HttpStatusCode} Message Sent..");

            }
            catch (AmazonSimpleNotificationServiceException ex)
            {
                context.Logger.LogCritical($"Error while Publishing Message :{ex.InnerException?.Message}");
            }
        }
        catch (AmazonCostExplorerException ex)
        {
            context.Logger.LogCritical($"Error while getting UsageCost :{ex.InnerException?.Message}");
        }
    }
}
