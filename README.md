# NetKit.Chat.Client

Softeq.NetKit.Chat.Client is a client that allows to quickly test NetKit.Chat Nuget. 
Service tests the following management types:
1. Channels
2. Messages
3. Members

# Getting Started

## Configure

1. Update appsettings.json:

```{
  "Chat": {
    "Url": "[chat app service url]"
  },
  "Auth": {
    "Url": "[auth app service url]",
    "UserName": "[first user email that exist in Auth database]",
    "Password": "[first user's password]",
    "InvitedUserName": "[second user email that exist in Auth database]"
  }
}
```
2. Configure your notification hub for iOS push notifications. 

3. Run Softeq.NetKit.Chat.SignalRClient.Sample console app. 

## About

This project is maintained by Softeq Development Corp.

We specialize in .NET core applications.

## Contributing

We welcome any contributions.

## License

The NetKit.Chat.Client project is available for free use, as described by the [LICENSE](/LICENSE) (MIT).
