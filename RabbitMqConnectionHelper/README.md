This is a small library originally designed for internal use.

It provides a couple of classes that make working with RabbitMQ easier.

## RabbitMqConnectionProvider

The connection provider makes it easier to re-use a single RabbitMQ connection across an application. 
This makes it easier to conform with the recommendation to keep the number of connections low and just create multiple channels.

To use this class just create an instance of it once then use CreateChannelAsync() to create channel instances. This method will wait while the connection is established, if one isn't already.


## MessagePublisher

This class is designed to make it simpler to publish topic based JSON messages. It automatically establishes a publishing channel using the provided RabbitMQ connection provider.