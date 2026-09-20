namespace UserProfile.Api.Messaging;

internal sealed record MessageHandlerRegistration(
    Type MessageType,
    string SubscriptionId);
