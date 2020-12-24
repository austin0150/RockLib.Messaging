﻿#if !NET451
using Confluent.Kafka;

namespace RockLib.Messaging.Kafka.DependencyInjection
{
    /// <summary>
    /// Defines the settings for creating instances of <see cref="KafkaReceiver"/>.
    /// </summary>
    public class KafkaReceiverOptions : ConsumerConfig
    {
        /// <summary>
        /// Gets or sets the topic to subscribe to.
        /// </summary>
        public string Topic { get; set; }
    }
}
#endif