using FluentAssertions;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Confluent.Kafka;
using Moq;
using RockLib.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using RockLib.Messaging.Kafka.Exceptions;

namespace RockLib.Messaging.Kafka.Tests
{
    public class KafkaReceiverTests
    {
        [Fact(DisplayName = "KafkaReceiver constructor 1 sets appropriate properties")]
        public void KafkaReceiverConstructor1HappyPath()
        {
            var name = "name";
            var topic = "topic";
            var groupId = "groupId";
            var servers = "bootstrapServers";
            var enableOffsetRestore = true;
            var autoOffsetReset = AutoOffsetReset.Error;
            var receiver = new KafkaReceiver(name, topic, groupId, servers, enableOffsetRestore, autoOffsetReset);

            receiver.Name.Should().Be(name);
            receiver.Topic.Should().Be(topic);
            receiver.GroupId.Should().Be(groupId);
            receiver.BootstrapServers.Should().Be(servers);
            receiver.EnableAutoOffsetStore.Should().Be(enableOffsetRestore);
            receiver.AutoOffsetReset.Should().Be(autoOffsetReset);
            receiver.Consumer.Should().NotBeNull();
        }

        [Fact(DisplayName = "KafkaReceiver constructor 1 throws on null topic")]
        public void KafkaReceiverConstructor1SadPath1()
        {
            Action action = () => new KafkaReceiver("name", null, "groupId", "servers");
            action.Should().Throw<ArgumentNullException>();
        }

        [Fact(DisplayName = "KafkaReceiver constructor 1 throws on null groupId")]
        public void KafkaReceiverConstructor1SadPath2()
        {
            Action action = () => new KafkaReceiver("name", "topic", null, "servers");
            action.Should().Throw<ArgumentNullException>();
        }

        [Fact(DisplayName = "KafkaReceiver constructor 1 throws on null bootstrapServers")]
        public void KafkaReceiverConstructor1SadPath3()
        {
            Action action = () => new KafkaReceiver("name", "topic", "groupId", null);
            action.Should().Throw<ArgumentNullException>();
        }

        [Fact(DisplayName = "KafkaReceiver constructor 2 sets appropriate properties")]
        public void KafkaReceiverConstructor2HappyPath()
        {
            var name = "name";
            var topic = "topic";
            var groupId = "groupId";
            var servers = "bootstrapServers";
            var enableOffsetRestore = true;
            var schemaIdRequired = true;
            var autoOffsetReset = AutoOffsetReset.Error;
            var consumer = new ConsumerConfig()
            {
                GroupId = groupId,
                BootstrapServers = servers,
                EnableAutoOffsetStore = enableOffsetRestore,
                AutoOffsetReset = autoOffsetReset
            };

            using (var receiver = new KafkaReceiver(name, topic, consumer, schemaIdRequired: schemaIdRequired))
            {
                receiver.Name.Should().Be(name);
                receiver.Topic.Should().Be(topic);
                receiver.GroupId.Should().Be(groupId);
                receiver.BootstrapServers.Should().Be(servers);
                receiver.EnableAutoOffsetStore.Should().Be(enableOffsetRestore);
                receiver.AutoOffsetReset.Should().Be(autoOffsetReset);
                receiver.Consumer.Should().NotBeNull();

                var unlockedReceiver = receiver.Unlock();
                Assert.Equal(schemaIdRequired, unlockedReceiver._schemaIdRequired);
            }
        }

        [Fact(DisplayName = "KafkaReceiver constructor 2 throws on null topic")]
        public void KafkaReceiverConstructor2SadPath1()
        {
            Action action = () => new KafkaReceiver("name", null, new ConsumerConfig());
            action.Should().Throw<ArgumentNullException>();
        }

        [Fact(DisplayName = "KafkaReceiver constructor 2 throws on null consumer")]
        public void KafkaReceiverConstructor2SadPath2()
        {
            Action action = () => new KafkaReceiver("name", "topic", null);
            action.Should().Throw<ArgumentNullException>();
        }

        [Fact(DisplayName = "KafkaReceiver Start starts the receiver and tracking threads")]
        public void KafkaReceiverStartHappyPath1()
        {
            var receiver = new KafkaReceiver("name", "one_topic", "groupId", "servers");

            var consumerMock = new Mock<IConsumer<string, byte[]>>();
            consumerMock.Setup(c => c.Subscribe(It.IsAny<string>()));

            var unlockedReceiver = receiver.Unlock();
            unlockedReceiver._consumer = new Lazy<IConsumer<string, byte[]>>(() => consumerMock.Object);
            unlockedReceiver.Start();

            consumerMock.Verify(cm => cm.Subscribe("one_topic"), Times.Once);

            unlockedReceiver.Dispose();
        }

        [Fact(DisplayName = "KafkaReceiver Dispose stops the receiver and tracking threads")]
        public void KafkaReceiverDisposeHappyPath()
        {
            var receiver = new KafkaReceiver("name", "one_topic", "groupId", "servers");

            var consumerMock = new Mock<IConsumer<string, byte[]>>();
            consumerMock.Setup(cm => cm.Close());
            consumerMock.Setup(cm => cm.Dispose());

            var unlockedReceiver = receiver.Unlock();
            unlockedReceiver._consumer = new Lazy<IConsumer<string, byte[]>>(() => consumerMock.Object);
            unlockedReceiver.Start();

            unlockedReceiver.Dispose();

            consumerMock.Verify(cm => cm.Close(), Times.Once);
            consumerMock.Verify(cm => cm.Dispose(), Times.Once);
        }

        [Fact(DisplayName = "KafkaReceiver receives message from consumer")]
        public void KafkaReceiverHappyPath()
        {
            var message = new Message<string, byte[]>() { Value = Encoding.UTF8.GetBytes("This is the expected message!") };
            var result = new ConsumeResult<string, byte[]>() { Message = message };

            var consumerMock = new Mock<IConsumer<string, byte[]>>();
            consumerMock.Setup(c => c.Subscribe(It.IsAny<string>()));
            consumerMock.Setup(c => c.Consume(It.IsAny<CancellationToken>())).Returns(result);

            var waitHandle = new AutoResetEvent(false);

            string receivedMessage = null;

            using (var receiver = new KafkaReceiver("NAME", "TOPIC", "GROUPID", "SERVER"))
            {
                var unlockedReceiver = receiver.Unlock();
                unlockedReceiver._consumer = new Lazy<IConsumer<string, byte[]>>(() => consumerMock.Object);

                receiver.Start(m =>
                {
                    receivedMessage = m.StringPayload;
                    waitHandle.Set();
                    return Task.CompletedTask;
                });

                waitHandle.WaitOne();
            }

            consumerMock.Verify(m => m.Consume(It.IsAny<CancellationToken>()));

            receivedMessage.Should().Be("This is the expected message!");
        }
        
        [Fact(DisplayName = "KafkaReceiver receives message with schema ID from consumer")]
        public void KafkaReceiverSchemaIdHappyPath()
        {
            const int schemaId = 100;
            const string msg = "This is the expected message!";
            byte[] BuildBuffer()
            {
                var buffer = new byte[] { 0 };
                return buffer.Concat(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(schemaId)))
                    .Concat(Encoding.UTF8.GetBytes(msg))
                    .ToArray();
            }

            var message = new Message<string, byte[]>() { Value = BuildBuffer() };
            var result = new ConsumeResult<string, byte[]>() { Message = message };

            var consumerMock = new Mock<IConsumer<string, byte[]>>();
            consumerMock.Setup(c => c.Subscribe(It.IsAny<string>()));
            consumerMock.Setup(c => c.Consume(It.IsAny<CancellationToken>())).Returns(result);

            var waitHandle = new AutoResetEvent(false);

            string receivedMessage = null;
            var parsedSchemaId = -1;

            using (var receiver = new KafkaReceiver("NAME", "TOPIC", "GROUPID", "SERVER", schemaIdRequired: true))
            {
                var unlockedReceiver = receiver.Unlock();
                unlockedReceiver._consumer = new Lazy<IConsumer<string, byte[]>>(() => consumerMock.Object);

                receiver.Start(m =>
                {
                    receivedMessage = m.StringPayload;
                    parsedSchemaId = int.Parse(m.Headers[Constants.KafkaSchemaIdHeader].ToString());
                    waitHandle.Set();
                    return Task.CompletedTask;
                });

                waitHandle.WaitOne();
            }

            consumerMock.Verify(m => m.Consume(It.IsAny<CancellationToken>()));

            receivedMessage.Should().Be(msg);
            parsedSchemaId.Should().Be(schemaId);
        }
        
        [Fact(DisplayName = "KafkaReceiver receives short message from consumer")]
        public void KafkaReceiverSchemaIdExceptionForShortMessage()
        {
            var message = new Message<string, byte[]>() { Value = new byte[4]{ 1, 2, 3, 4 } };
            var result = new ConsumeResult<string, byte[]>() { Message = message };

            var consumerMock = new Mock<IConsumer<string, byte[]>>();
            consumerMock.Setup(c => c.Subscribe(It.IsAny<string>()));
            consumerMock.Setup(c => c.Consume(It.IsAny<CancellationToken>())).Returns(result);

            var waitHandle = new AutoResetEvent(false);

            using (var receiver = new KafkaReceiver("NAME", "TOPIC", "GROUPID", "SERVER", schemaIdRequired: true))
            {
                var unlockedReceiver = receiver.Unlock();
                unlockedReceiver._consumer = new Lazy<IConsumer<string, byte[]>>(() => consumerMock.Object);

                receiver.Start(m =>
                {
                    var ex = Assert.Throws<InvalidMessageException>(() => m.Headers[Constants.KafkaKeyHeader].ToString());
                    Assert.StartsWith("Expected payload greater than", ex.Message);
                    
                    ex = Assert.Throws<InvalidMessageException>(() => m.StringPayload);
                    Assert.StartsWith("Expected payload greater than", ex.Message);
                    waitHandle.Set();
                    return Task.CompletedTask;
                });

                waitHandle.WaitOne();
            }

            consumerMock.Verify(m => m.Consume(It.IsAny<CancellationToken>()));
        }
        
        [Fact(DisplayName = "KafkaReceiver receives invalid schema data frame")]
        public void KafkaReceiverSchemaIdExceptionForInvalidSchemaDataFrame()
        {
            var message = new Message<string, byte[]>() { Value = new byte[6]{ 1, 1, 2, 3, 4, 5 } };
            var result = new ConsumeResult<string, byte[]>() { Message = message };

            var consumerMock = new Mock<IConsumer<string, byte[]>>();
            consumerMock.Setup(c => c.Subscribe(It.IsAny<string>()));
            consumerMock.Setup(c => c.Consume(It.IsAny<CancellationToken>())).Returns(result);

            var waitHandle = new AutoResetEvent(false);

            using (var receiver = new KafkaReceiver("NAME", "TOPIC", "GROUPID", "SERVER", schemaIdRequired: true))
            {
                var unlockedReceiver = receiver.Unlock();
                unlockedReceiver._consumer = new Lazy<IConsumer<string, byte[]>>(() => consumerMock.Object);

                receiver.Start(m =>
                {
                    var ex = Assert.Throws<InvalidMessageException>(() => m.Headers[Constants.KafkaKeyHeader].ToString());
                    Assert.StartsWith("Expected schema registry data frame", ex.Message);
                    
                    ex = Assert.Throws<InvalidMessageException>(() => m.StringPayload);
                    Assert.StartsWith("Expected schema registry data frame", ex.Message);
                    
                    waitHandle.Set();
                    return Task.CompletedTask;
                });

                waitHandle.WaitOne();
            }

            consumerMock.Verify(m => m.Consume(It.IsAny<CancellationToken>()));
        }
    }
}
