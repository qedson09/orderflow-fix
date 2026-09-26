using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OrderFlow.Infrastructure.Messaging;

public static class MessagingRegistration
{
    public static IServiceCollection AddKafkaCommon(this IServiceCollection services, IConfiguration config) =>
        services.AddSingleton(KafkaSettings.From(config)).AddSingleton<KafkaTopicManager>();
}
